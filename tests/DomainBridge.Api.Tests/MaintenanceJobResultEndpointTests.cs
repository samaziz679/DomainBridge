using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class MaintenanceJobResultEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MaintenanceJobResultEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Result_is_accepted_only_for_an_approved_targeted_campaign()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/maintenance/campaigns", new
        {
            remediationId = "network-diagnostics",
            targetDeviceIds = new[] { "lab-device-003" }
        });
        var campaign = await created.Content.ReadFromJsonAsync<CampaignResponse>();
        Assert.NotNull(campaign);

        var beforeApproval = await _client.PostAsJsonAsync(
            $"/api/v1/maintenance/campaigns/{campaign!.CampaignId}/results",
            new { deviceId = "lab-device-003", outcome = "succeeded" });
        Assert.Equal(HttpStatusCode.Conflict, beforeApproval.StatusCode);

        await _client.PostAsync($"/api/v1/maintenance/campaigns/{campaign.CampaignId}/approve", null);
        var accepted = await _client.PostAsJsonAsync(
            $"/api/v1/maintenance/campaigns/{campaign.CampaignId}/results",
            new { deviceId = "lab-device-003", outcome = "succeeded" });

        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
    }

    [Fact]
    public async Task Result_rejects_unknown_outcomes_and_non_target_devices()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/maintenance/campaigns", new
        {
            remediationId = "network-diagnostics",
            targetDeviceIds = new[] { "lab-device-004" }
        });
        var campaign = await created.Content.ReadFromJsonAsync<CampaignResponse>();
        Assert.NotNull(campaign);
        await _client.PostAsync($"/api/v1/maintenance/campaigns/{campaign!.CampaignId}/approve", null);

        var invalidOutcome = await _client.PostAsJsonAsync(
            $"/api/v1/maintenance/campaigns/{campaign.CampaignId}/results",
            new { deviceId = "lab-device-004", outcome = "ran arbitrary script" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidOutcome.StatusCode);

        var wrongDevice = await _client.PostAsJsonAsync(
            $"/api/v1/maintenance/campaigns/{campaign.CampaignId}/results",
            new { deviceId = "lab-device-other", outcome = "succeeded" });
        Assert.Equal(HttpStatusCode.Forbidden, wrongDevice.StatusCode);
    }

    public sealed record CampaignResponse(Guid CampaignId);
}
