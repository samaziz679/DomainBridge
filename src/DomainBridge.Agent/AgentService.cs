using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DomainBridge.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DomainBridge.Agent;

/// <summary>
/// Settings of the agent service. None of them is a secret: the agent holds no
/// password, and its certificate stays in the Windows certificate store.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";
    public const string ServiceName = "DomainBridgeAgent";

    /// <summary>HTTPS address of the DomainBridge server, on the administrator workstation.</summary>
    public string? ServerUrl { get; init; }

    public int InventoryIntervalMinutes { get; init; } = 60;

    /// <summary>Problems that prevent the service from running; empty when the settings are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var server))
        {
            problems.Add("Agent:ServerUrl must be an absolute address.");
        }
        else if (server.Scheme != Uri.UriSchemeHttps)
        {
            problems.Add("Agent:ServerUrl must use HTTPS.");
        }

        if (InventoryIntervalMinutes is < 1 or > 1440)
        {
            problems.Add("Agent:InventoryIntervalMinutes must be between 1 and 1440.");
        }

        return problems;
    }
}

/// <summary>
/// Picks the certificate that identifies this machine to the server: a currently
/// valid client-authentication certificate, with its private key, named after the
/// machine. The server decides whether its issuer is trusted.
/// </summary>
public static class AgentCertificateSelector
{
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    public static X509Certificate2? Select(
        IEnumerable<X509Certificate2> candidates,
        string deviceName,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

        return candidates
            .Where(certificate => certificate.HasPrivateKey)
            .Where(certificate => now >= certificate.NotBefore && now <= certificate.NotAfter)
            .Where(certificate => string.Equals(
                certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false),
                deviceName,
                StringComparison.OrdinalIgnoreCase))
            .Where(certificate => !IsSelfSigned(certificate))
            .Where(HasClientAuthenticationUsage)
            .OrderByDescending(certificate => certificate.NotAfter)
            .FirstOrDefault();
    }

    private static bool IsSelfSigned(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData);

    private static bool HasClientAuthenticationUsage(X509Certificate2 certificate) =>
        certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(extension => extension.EnhancedKeyUsages.Cast<Oid>())
            .Any(usage => usage.Value == ClientAuthenticationOid);
}

public enum InventoryCycleOutcome
{
    Accepted = 0,
    Rejected,
    Unreachable
}

public interface IInventoryCycle
{
    Task<InventoryCycleOutcome> RunOnceAsync(CancellationToken cancellationToken);
}

/// <summary>One round: capture the read-only inventory of this machine and send it.</summary>
public sealed class InventoryCycle(InventorySubmissionClient client, string agentVersion) : IInventoryCycle
{
    public async Task<InventoryCycleOutcome> RunOnceAsync(CancellationToken cancellationToken)
    {
        var snapshot = DeviceInventorySnapshot.Capture(agentVersion);
        var report = new DeviceInventoryReport(
            snapshot.DeviceId,
            snapshot.OperatingSystem,
            snapshot.Architecture,
            snapshot.AgentVersion,
            snapshot.CapturedAtUtc);

        try
        {
            var result = await client.SubmitAsync(report, cancellationToken);
            return result.Accepted ? InventoryCycleOutcome.Accepted : InventoryCycleOutcome.Rejected;
        }
        catch (HttpRequestException)
        {
            return InventoryCycleOutcome.Unreachable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A request timeout, not a service stop.
            return InventoryCycleOutcome.Unreachable;
        }
    }
}

public static class RetrySchedule
{
    private static readonly TimeSpan FirstRetry = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Time to wait before the next round. After a success: the normal interval.
    /// After failures: one minute, doubling each time, never beyond the normal interval.
    /// </summary>
    public static TimeSpan NextDelay(int consecutiveFailures, TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(consecutiveFailures);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        if (consecutiveFailures == 0)
        {
            return interval;
        }

        var doublings = Math.Min(consecutiveFailures - 1, 20);
        var backoff = TimeSpan.FromTicks(FirstRetry.Ticks << doublings);
        return backoff < interval ? backoff : interval;
    }
}

/// <summary>
/// The service loop. It only ever calls out to the server; it opens no listener.
/// A server that is down or refuses the report never stops the service.
/// </summary>
public sealed partial class AgentWorker(
    IInventoryCycle cycle,
    AgentOptions options,
    ILogger<AgentWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.InventoryIntervalMinutes);
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var outcome = await cycle.RunOnceAsync(stoppingToken);
            consecutiveFailures = outcome == InventoryCycleOutcome.Accepted ? 0 : consecutiveFailures + 1;
            var delay = RetrySchedule.NextDelay(consecutiveFailures, interval);
            LogCycle(logger, outcome, delay.TotalMinutes);

            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Inventory round: {Outcome}. Next round in {Minutes} minute(s).")]
    private static partial void LogCycle(ILogger logger, InventoryCycleOutcome outcome, double minutes);
}
