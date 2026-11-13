using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class MaintenanceCampaignReadEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MaintenanceCampaignReadEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Campaigns_lists_drafts_without_exposing_scripts_or_secrets()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/maintenance/campaigns", new
        {
            remediationId = "windows-update-reset",
            targetDeviceIds = new[] { "pilot-001" }
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var response = await _client.GetAsync("/api/v1/maintenance/campaigns");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("windows-update-reset", payload);
        Assert.Contains("draft", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
    }
}
