using DomainBridge.Contracts;

namespace DomainBridge.Agent;

/// <summary>
/// Runs approved jobs through the local allowlist and reports only normalised outcomes.
/// The processor never accepts script content or bypasses the runner policy.
/// </summary>
public sealed class MaintenanceJobProcessor(
    IMaintenanceJobSource jobSource,
    MaintenanceActionRunner runner,
    IMaintenanceResultReporter resultReporter)
{
    public async Task<MaintenanceProcessingResult> ProcessAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("A device identifier is required.", nameof(deviceId));
        }

        var jobs = await jobSource.GetApprovedJobsAsync(deviceId.Trim(), cancellationToken);
        var processedCount = 0;
        var reportedCount = 0;

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = runner.Run(job.RemediationId);
            processedCount++;

            if (await resultReporter.ReportAsync(
                job.CampaignId,
                deviceId.Trim(),
                execution.Outcome,
                cancellationToken))
            {
                reportedCount++;
            }
        }

        return new MaintenanceProcessingResult(processedCount, reportedCount);
    }
}

public sealed record MaintenanceProcessingResult(int ProcessedCount, int ReportedCount);
