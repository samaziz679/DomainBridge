using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class ConsoleReadEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ConsoleReadEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Overview_returns_read_only_fleet_and_audit_metrics()
    {
        await _client.PostAsJsonAsync("/api/v1/enrollments", new
        {
            agentId = "console-device-001",
            certificateThumbprint = "A1B2C3",
            agentVersion = "agent/1.0"
        });

        await _client.PostAsJsonAsync("/api/v1/inventory", new
        {
            deviceId = "console-device-001",
            operatingSystem = "Windows 11",
            architecture = "X64",
            agentVersion = "agent/1.0",
            capturedAtUtc = "2026-11-11T00:00:00+00:00"
        });

        var response = await _client.GetAsync("/api/v1/console/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("enrolledAgents", payload);
        Assert.Contains("auditEvents", payload);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Agents_returns_technical_metadata_without_profile_data()
    {
        var response = await _client.GetAsync("/api/v1/console/agents?limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("profile", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Audit_rejects_out_of_range_limits()
    {
        var response = await _client.GetAsync("/api/v1/console/audit?limit=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Remediation_catalog_exposes_policy_metadata_without_scripts()
    {
        var response = await _client.GetAsync("/api/v1/console/remediations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("windows-update-reset", payload);
        Assert.Contains("requiresApproval", payload);
        Assert.DoesNotContain("Invoke-Expression", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell.exe", payload, StringComparison.OrdinalIgnoreCase);
    }
}
