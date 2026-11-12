using DomainBridge.Agent;
using DomainBridge.Contracts;
using Xunit;

namespace DomainBridge.Agent.Tests;

public sealed class MaintenanceJobProcessorTests
{
    [Fact]
    public async Task Processor_runs_allowed_diagnostics_and_reports_each_normalized_outcome()
    {
        var campaignId = Guid.Parse("6c4a632d-13e2-452f-a0c8-e2993c6608f7");
        var source = new StubSource([
            new MaintenanceJob(campaignId, "network-diagnostics", false, false),
            new MaintenanceJob(Guid.NewGuid(), "windows-update-reset", true, true)
        ]);
        var reporter = new CapturingReporter();
        var runner = new MaintenanceActionRunner(new StubSignals(networkAvailable: true, defenderHealthy: true));
        var processor = new MaintenanceJobProcessor(source, runner, reporter);

        var result = await processor.ProcessAsync("lab-device-006");

        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(2, result.ReportedCount);
        Assert.Equal(MaintenanceJobOutcome.Succeeded, reporter.Results[campaignId]);
        Assert.Contains(reporter.Results.Values, outcome => outcome == MaintenanceJobOutcome.Failed);
    }

    [Fact]
    public async Task Processor_does_not_process_when_the_device_identifier_is_blank()
    {
        var processor = new MaintenanceJobProcessor(
            new StubSource([]),
            new MaintenanceActionRunner(new StubSignals(true, true)),
            new CapturingReporter());

        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync(" "));
    }

    private sealed class StubSignals(bool networkAvailable, bool defenderHealthy) : IMaintenanceSignals
    {
        public bool IsNetworkAvailable() => networkAvailable;
        public bool IsDefenderHealthy() => defenderHealthy;
    }

    private sealed class StubSource(IReadOnlyCollection<MaintenanceJob> jobs) : IMaintenanceJobSource
    {
        public Task<IReadOnlyCollection<MaintenanceJob>> GetApprovedJobsAsync(string deviceId, CancellationToken cancellationToken = default) => Task.FromResult(jobs);
    }

    private sealed class CapturingReporter : IMaintenanceResultReporter
    {
        public Dictionary<Guid, MaintenanceJobOutcome> Results { get; } = [];

        public Task<bool> ReportAsync(Guid campaignId, string deviceId, MaintenanceJobOutcome outcome, CancellationToken cancellationToken = default)
        {
            Results[campaignId] = outcome;
            return Task.FromResult(true);
        }
    }
}
