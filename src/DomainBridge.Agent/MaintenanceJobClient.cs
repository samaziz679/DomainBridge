using System.Net.Http.Json;
using DomainBridge.Contracts;

namespace DomainBridge.Agent;

/// <summary>
/// Retrieves policy-only maintenance tasks after the API has approved a campaign.
/// This client does not execute the returned action.
/// </summary>
public sealed class MaintenanceJobClient(HttpClient httpClient)
{
    public async Task<IReadOnlyCollection<MaintenanceJob>> GetApprovedJobsAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("A device identifier is required.", nameof(deviceId));
        }

        var escapedDeviceId = Uri.EscapeDataString(deviceId.Trim());
        using var response = await httpClient.GetAsync(
            $"/api/v1/agents/{escapedDeviceId}/maintenance-jobs",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MaintenanceJob[]>(cancellationToken: cancellationToken)
            ?? [];
    }
}
