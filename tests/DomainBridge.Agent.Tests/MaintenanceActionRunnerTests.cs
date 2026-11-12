using DomainBridge.Agent;
using DomainBridge.Contracts;
using Xunit;

namespace DomainBridge.Agent.Tests;

public sealed class MaintenanceActionRunnerTests
{
    [Fact]
    public void Runner_executes_network_diagnostics_as_a_read_only_check()
    {
        var runner = new MaintenanceActionRunner(new StubSignals(networkAvailable: true, defenderHealthy: true));

        var result = runner.Run("network-diagnostics");

        Assert.Equal(MaintenanceJobOutcome.Succeeded, result.Outcome);
        Assert.Equal("network_available", result.EvidenceCode);
        Assert.False(result.MayRequireRestart);
    }

    [Fact]
    public void Runner_reports_a_failed_defender_health_check_without_free_form_output()
    {
        var runner = new MaintenanceActionRunner(new StubSignals(networkAvailable: true, defenderHealthy: false));

        var result = runner.Run("defender-health-check");

        Assert.Equal(MaintenanceJobOutcome.Failed, result.Outcome);
        Assert.Equal("defender_not_healthy", result.EvidenceCode);
        Assert.DoesNotContain("password", result.EvidenceCode, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runner_refuses_unknown_and_state_changing_actions()
    {
        var runner = new MaintenanceActionRunner(new StubSignals(networkAvailable: true, defenderHealthy: true));

        Assert.Equal("action_not_allowed", runner.Run("Invoke-Expression arbitrary-command").EvidenceCode);
        Assert.Equal("action_not_allowed", runner.Run("windows-update-reset").EvidenceCode);
    }

    private sealed class StubSignals(bool networkAvailable, bool defenderHealthy) : IMaintenanceSignals
    {
        public bool IsNetworkAvailable() => networkAvailable;
        public bool IsDefenderHealthy() => defenderHealthy;
    }
}
