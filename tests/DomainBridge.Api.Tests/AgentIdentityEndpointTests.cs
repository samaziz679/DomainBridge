using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DomainBridge.Api.Tests;

/// <summary>
/// Scenario E10: an agent must not be able to act for another device.
/// The host enforces certificate identity; each request carries the certificate
/// chosen by the test, exactly where the TLS layer would have put it.
/// </summary>
public sealed class AgentIdentityEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly X509Certificate2 _authority = TestCertificates.CreateAuthority("CN=DomainBridge Test Agent CA");
    private readonly X509Certificate2 _rogueAuthority = TestCertificates.CreateAuthority("CN=Rogue CA");
    private readonly ClientCertificateHolder _holder = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AgentIdentityEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(host =>
        {
            host.UseSetting("AgentIdentity:Enforce", "true");
            host.UseSetting("AgentIdentity:TrustedIssuerThumbprints:0", _authority.Thumbprint);
            host.ConfigureServices(services =>
            {
                services.AddSingleton<IAgentIssuerChainReader>(
                    new X509AgentIssuerChainReader([_authority, _rogueAuthority]));
                services.AddSingleton<IStartupFilter>(new ClientCertificateStartupFilter(_holder));
            });
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _holder.Certificate?.Dispose();
        _authority.Dispose();
        _rogueAuthority.Dispose();
    }

    private static object Inventory(string deviceId) => new
    {
        deviceId,
        operatingSystem = "Windows 11",
        architecture = "X64",
        agentVersion = "agent/1.0",
        capturedAtUtc = "2026-11-13T00:00:00+00:00"
    };

    private IAuditStore Audit => _factory.Services.GetRequiredService<IAuditStore>();

    [Fact]
    public async Task Request_without_certificate_is_unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", Inventory("SPARE01"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Agent_reports_inventory_for_its_own_device()
    {
        _holder.Certificate = TestCertificates.IssueAgentCertificate(_authority, "SPARE01");

        var response = await _client.PostAsJsonAsync("/api/v1/inventory", Inventory("spare01"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var recorded = Audit.Events.Last(auditEvent => auditEvent.Action == "agent.inventory.reported");
        Assert.Equal("agent:SPARE01", recorded.Actor);
        Assert.Equal(AuditOutcomes.Success, recorded.Outcome);
    }

    [Fact]
    public async Task Agent_cannot_report_inventory_for_another_device()
    {
        _holder.Certificate = TestCertificates.IssueAgentCertificate(_authority, "SPARE01");

        var response = await _client.PostAsJsonAsync("/api/v1/inventory", Inventory("VICTIM-PC"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var inventory = _factory.Services.GetRequiredService<IInventoryStore>().GetRecent(100);
        Assert.DoesNotContain(inventory, device => device.DeviceId == "VICTIM-PC");
        var rejected = Audit.Events.Last(auditEvent => auditEvent.Action == "agent.identity.rejected");
        Assert.Equal("VICTIM-PC", rejected.SubjectId);
        Assert.Equal("agent:SPARE01", rejected.Actor);
        Assert.Equal(AuditOutcomes.Denied, rejected.Outcome);
    }

    [Fact]
    public async Task Certificate_from_an_unpinned_authority_is_unauthorized()
    {
        _holder.Certificate = TestCertificates.IssueAgentCertificate(_rogueAuthority, "SPARE01");

        var response = await _client.PostAsJsonAsync("/api/v1/inventory", Inventory("SPARE01"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrollment_must_name_the_presented_certificate()
    {
        var certificate = TestCertificates.IssueAgentCertificate(_authority, "SPARE01");
        _holder.Certificate = certificate;

        var honest = await _client.PostAsJsonAsync("/api/v1/enrollments", new
        {
            agentId = "SPARE01",
            certificateThumbprint = certificate.Thumbprint,
            agentVersion = "agent/1.0"
        });
        var otherThumbprint = await _client.PostAsJsonAsync("/api/v1/enrollments", new
        {
            agentId = "SPARE01",
            certificateThumbprint = _authority.Thumbprint,
            agentVersion = "agent/1.0"
        });
        var otherDevice = await _client.PostAsJsonAsync("/api/v1/enrollments", new
        {
            agentId = "VICTIM-PC",
            certificateThumbprint = certificate.Thumbprint,
            agentVersion = "agent/1.0"
        });

        Assert.Equal(HttpStatusCode.OK, honest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherThumbprint.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherDevice.StatusCode);
    }

    [Fact]
    public void Production_host_refuses_to_start_without_a_pinned_authority()
    {
        using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Production");
            host.UseSetting("ConnectionStrings:Audit", "Host=unused.invalid;Database=unused");
        });

        var failure = Assert.ThrowsAny<Exception>(() => production.CreateClient());

        Assert.Contains("AgentIdentity:TrustedIssuerThumbprints", failure.ToString(), StringComparison.Ordinal);
    }

    private sealed class ClientCertificateHolder
    {
        public X509Certificate2? Certificate { get; set; }
    }

    private sealed class ClientCertificateStartupFilter(ClientCertificateHolder holder) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.Connection.ClientCertificate = holder.Certificate;
                await continuation(context);
            });
            next(app);
        };
    }
}
