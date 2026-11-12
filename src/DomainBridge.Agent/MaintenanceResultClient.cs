using System.Net;
using System.Net.Http.Json;
using DomainBridge.Contracts;

namespace DomainBridge.Agent;

/// <summary>
/// Sends a normalized maintenance result to the control plane.
/// Free-form command output is intentionally not part of this transport.
/// </summary>
public interface IMaintenanceResultReporter
{
    Task<bool> ReportAsync(
        Guid campaignId,
        string deviceId,
        MaintenanceJobOutcome outcome,
        CancellationToken cancellationToken = default);
}

public sealed class MaintenanceResultClient(HttpClient httpClient) : IMaintenanceResultReporter
{
    public async Task<bool> ReportAsync(
        Guid campaignId,
        string deviceId,
        MaintenanceJobOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("A campaign identifier is required.", nameof(campaignId));
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("A device identifier is required.", nameof(deviceId));
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        using var response = await httpClient.PostAsJsonAsync(
            $"/api/v1/maintenance/campaigns/{campaignId}/results",
            new MaintenanceJobResultSubmission(deviceId.Trim(), outcome.ToString().ToLowerInvariant()),
            cancellationToken);

        return response.StatusCode == HttpStatusCode.Accepted;
    }
}

public sealed record MaintenanceJobResultSubmission(string DeviceId, string Outcome);
