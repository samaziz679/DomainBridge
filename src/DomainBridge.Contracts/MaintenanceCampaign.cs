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
