namespace DomainBridge.Contracts;

public enum MaintenanceCampaignStatus
{
    Draft = 0,
    Approved = 1
}

/// <summary>
/// A laboratory-only maintenance campaign. It contains policy references and targets,
/// never script bodies, credentials, or executable command text.
/// </summary>
public sealed record MaintenanceCampaign(
    Guid CampaignId,
    string RemediationId,
    IReadOnlyCollection<string> TargetDeviceIds,
    MaintenanceCampaignStatus Status,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// A policy-only task offered to an enrolled agent after campaign approval.
/// The payload deliberately carries no executable content.
/// </summary>
public sealed record MaintenanceJob(
    Guid CampaignId,
    string RemediationId,
    bool RequiresApproval,
    bool MayRequireRestart);

public enum MaintenanceJobOutcome
{
    Succeeded = 0,
    Failed = 1,
    RebootRequired = 2
}

/// <summary>
/// Structured outcome evidence submitted by an agent. Free-form output and logs
/// are intentionally excluded from the initial transport contract.
/// </summary>
public sealed record MaintenanceJobResult(
    Guid CampaignId,
    string DeviceId,
    MaintenanceJobOutcome Outcome,
    DateTimeOffset ReportedAtUtc);
