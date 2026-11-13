using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DomainBridge.Agent.Tests;

public sealed class AgentServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData("https://admin-pc.lab-a.test:8443/", 60, 0)]
    [InlineData("http://admin-pc.lab-a.test/", 60, 1)]
    [InlineData("admin-pc", 60, 1)]
    [InlineData(null, 60, 1)]
    [InlineData("https://admin-pc.lab-a.test/", 0, 1)]
    [InlineData("https://admin-pc.lab-a.test/", 1441, 1)]
    [InlineData("ftp://admin-pc/", 0, 2)]
    public void Options_require_an_https_server_and_a_sane_interval(string? serverUrl, int interval, int expectedProblems)
    {
        var options = new AgentOptions { ServerUrl = serverUrl, InventoryIntervalMinutes = interval };

        Assert.Equal(expectedProblems, options.Validate().Count);
    }

    [Fact]
    public void Selector_picks_the_valid_machine_certificate_that_expires_last()
    {
        using var authority = CreateAuthority();
        using var shorter = Issue(authority, "SPARE01", Now.AddDays(30));
        using var longer = Issue(authority, "SPARE01", Now.AddDays(90));

        var selected = AgentCertificateSelector.Select([shorter, longer], "spare01", Now);

        Assert.Equal(longer.Thumbprint, selected?.Thumbprint);
    }

    [Fact]
    public void Selector_ignores_certificates_that_cannot_identify_this_machine()
    {
        using var authority = CreateAuthority();
        using var otherMachine = Issue(authority, "OTHER-PC", Now.AddDays(30));
        using var expired = Issue(authority, "SPARE01", Now.AddDays(30));
        using var noClientUsage = Issue(authority, "SPARE01", Now.AddDays(30), clientAuthentication: false);
        using var withoutKey = X509CertificateLoader.LoadCertificate(Issue(authority, "SPARE01", Now.AddDays(30)).RawData);
        using var selfSigned = CreateSelfSigned("SPARE01");

        Assert.Null(AgentCertificateSelector.Select([otherMachine, noClientUsage, withoutKey, selfSigned], "SPARE01", Now));
        Assert.Null(AgentCertificateSelector.Select([expired], "SPARE01", Now.AddDays(31)));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 8)]
    [InlineData(7, 60)]
    [InlineData(500, 60)]
    public void Retry_backs_off_from_one_minute_and_never_exceeds_the_interval(int failures, int expectedMinutes)
    {
        var delay = RetrySchedule.NextDelay(failures, TimeSpan.FromMinutes(60));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, InventoryCycleOutcome.Accepted)]
    [InlineData(HttpStatusCode.Forbidden, InventoryCycleOutcome.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, InventoryCycleOutcome.Rejected)]
    public async Task Cycle_reports_what_the_server_answered(HttpStatusCode status, InventoryCycleOutcome expected)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://admin-pc.lab-a.test/") };
        var cycle = new InventoryCycle(new InventorySubmissionClient(client), "1.2.3");

        var outcome = await cycle.RunOnceAsync(CancellationToken.None);

        Assert.Equal(expected, outcome);
        Assert.Contains(Environment.MachineName, handler.LastBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.2.3", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cycle_survives_an_unreachable_server()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://admin-pc.lab-a.test/") };
        var cycle = new InventoryCycle(new InventorySubmissionClient(client), "1.2.3");

        Assert.Equal(InventoryCycleOutcome.Unreachable, await cycle.RunOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Worker_keeps_running_through_failures_and_stops_when_asked()
    {
        var cycle = new ScriptedCycle(InventoryCycleOutcome.Unreachable, InventoryCycleOutcome.Rejected, InventoryCycleOutcome.Accepted);
        var time = new ImmediateTimeProvider();
        using var worker = new AgentWorker(
            cycle,
            new AgentOptions { ServerUrl = "https://admin-pc.lab-a.test/", InventoryIntervalMinutes = 60 },
            NullLogger<AgentWorker>.Instance,
            time);

        await worker.StartAsync(CancellationToken.None);
        await cycle.Exhausted.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(cycle.Rounds >= 3);
        Assert.Equal(
            [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(60)],
            time.RequestedDelays.Take(3));
    }

    [Fact]
    public async Task Agent_waits_until_settings_and_certificate_are_present()
    {
        var attempts = 0;
        var ready = new ScriptedCycle(InventoryCycleOutcome.Accepted);
        var cycle = new ReadinessAwareCycle(() => ++attempts < 3 ? null : ready);

        var outcomes = new[]
        {
            await cycle.RunOnceAsync(CancellationToken.None),
            await cycle.RunOnceAsync(CancellationToken.None),
            await cycle.RunOnceAsync(CancellationToken.None),
            await cycle.RunOnceAsync(CancellationToken.None)
        };

        Assert.Equal(
            [InventoryCycleOutcome.NotReady, InventoryCycleOutcome.NotReady, InventoryCycleOutcome.Accepted, InventoryCycleOutcome.Accepted],
            outcomes);
        Assert.Equal(3, attempts);
    }

    private static X509Certificate2 CreateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=DomainBridge Test Agent CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(Now.AddDays(-1), Now.AddYears(2));
    }

    private static X509Certificate2 Issue(
        X509Certificate2 authority,
        string deviceName,
        DateTimeOffset notAfter,
        bool clientAuthentication = true)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={deviceName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (clientAuthentication)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.2") }, critical: false));
        }

        using var issued = request.Create(authority, Now.AddMinutes(-5), notAfter, RandomNumberGenerator.GetBytes(12));
        return issued.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateSelfSigned(string deviceName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={deviceName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.2") }, critical: false));
        return request.CreateSelfSigned(Now.AddMinutes(-5), Now.AddDays(30));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private sealed class ScriptedCycle(params InventoryCycleOutcome[] script) : IInventoryCycle
    {
        private readonly TaskCompletionSource _exhausted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _rounds;

        public int Rounds => _rounds;

        public Task Exhausted => _exhausted.Task;

        public Task<InventoryCycleOutcome> RunOnceAsync(CancellationToken cancellationToken)
        {
            var round = Interlocked.Increment(ref _rounds);
            if (round >= script.Length)
            {
                _exhausted.TrySetResult();
            }

            return Task.FromResult(script[Math.Min(round, script.Length) - 1]);
        }
    }

    /// <summary>Time that does not wait: every delay elapses at once and is recorded.</summary>
    private sealed class ImmediateTimeProvider : TimeProvider
    {
        private readonly List<TimeSpan> _delays = [];

        public IReadOnlyList<TimeSpan> RequestedDelays
        {
            get
            {
                lock (_delays)
                {
                    return _delays.ToArray();
                }
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (_delays)
            {
                _delays.Add(dueTime);
            }

            return base.CreateTimer(callback, state, TimeSpan.FromMilliseconds(1), period);
        }
    }
}
