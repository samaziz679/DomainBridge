using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class MaintenanceCampaignEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MaintenanceCampaignEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_requires_an_approved_remediation_and_target_device()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/maintenance/campaigns", new
        {
            remediationId = "Invoke-Expression arbitrary-command",
            targetDeviceIds = Array.Empty<string>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Campaign_must_be_approved_before_it_can_be_listed_as_approved()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/maintenance/campaigns", new
        {
            remediationId = "windows-update-reset",
            targetDeviceIds = new[] { "lab-device-001" }
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var campaign = await created.Content.ReadFromJsonAsync<MaintenanceCampaignResponse>();
        Assert.NotNull(campaign);
        Assert.Equal("draft", campaign!.Status);
        Assert.True(campaign.RequiresApproval);

        var approval = await _client.PostAsync($"/api/v1/maintenance/campaigns/{campaign.CampaignId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        var approved = await approval.Content.ReadFromJsonAsync<MaintenanceCampaignResponse>();
        Assert.NotNull(approved);
        Assert.Equal("approved", approved!.Status);
    }

    public sealed record MaintenanceCampaignResponse(Guid CampaignId, string Status, bool RequiresApproval);
}
