using DomainBridge.Contracts;

namespace DomainBridge.Agent;

/// <summary>
/// Executes only explicitly allowlisted, non-destructive diagnostics.
/// It deliberately accepts remediation identifiers, never command lines or scripts.
/// </summary>
public sealed class MaintenanceActionRunner(IMaintenanceSignals signals)
{
    public MaintenanceExecutionResult Run(string? remediationId)
    {
        return remediationId switch
        {
            "network-diagnostics" => signals.IsNetworkAvailable()
                ? new(remediationId, MaintenanceJobOutcome.Succeeded, "network_available", false)
                : new(remediationId, MaintenanceJobOutcome.Failed, "network_unavailable", false),
            "defender-health-check" => signals.IsDefenderHealthy()
                ? new(remediationId, MaintenanceJobOutcome.Succeeded, "defender_healthy", false)
                : new(remediationId, MaintenanceJobOutcome.Failed, "defender_not_healthy", false),
            _ => new(remediationId ?? string.Empty, MaintenanceJobOutcome.Failed, "action_not_allowed", false)
        };
    }
}

/// <summary>
/// Host-supplied Windows health signals. Keeping this boundary explicit prevents
/// the runner from issuing unrestricted shell commands.
/// </summary>
public interface IMaintenanceSignals
{
    bool IsNetworkAvailable();

    bool IsDefenderHealthy();
}

public sealed record MaintenanceExecutionResult(
    string RemediationId,
    MaintenanceJobOutcome Outcome,
    string EvidenceCode,
    bool MayRequireRestart);
