using DomainBridge.Contracts;

namespace DomainBridge.Agent;

/// <summary>
/// Approved maintenance actions exposed by the agent.
/// This catalog deliberately contains identifiers and policy metadata only;
/// it never accepts or executes arbitrary scripts.
/// </summary>
public static class RemediationCatalog
{
    public static IReadOnlyList<RemediationActionDefinition> All => ApprovedRemediationCatalog.All;

    public static RemediationActionDefinition? Find(string? id) => ApprovedRemediationCatalog.Find(id);
}
