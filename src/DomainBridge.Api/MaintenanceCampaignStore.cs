using System.Collections.Concurrent;
using DomainBridge.Contracts;

public interface IMaintenanceCampaignStore
{
    MaintenanceCampaign Create(string remediationId, IReadOnlyCollection<string> targetDeviceIds);

    MaintenanceCampaign? Find(Guid campaignId);

    MaintenanceCampaign? Approve(Guid campaignId);

    IReadOnlyCollection<MaintenanceCampaign> GetApprovedForDevice(string deviceId);
}

public sealed class InMemoryMaintenanceCampaignStore : IMaintenanceCampaignStore
{
    private readonly ConcurrentDictionary<Guid, MaintenanceCampaign> _campaigns = new();

    public MaintenanceCampaign Create(string remediationId, IReadOnlyCollection<string> targetDeviceIds)
    {
        var campaign = new MaintenanceCampaign(
            Guid.NewGuid(),
            remediationId,
            targetDeviceIds,
            MaintenanceCampaignStatus.Draft,
            DateTimeOffset.UtcNow);
        _campaigns[campaign.CampaignId] = campaign;
        return campaign;
    }

    public MaintenanceCampaign? Find(Guid campaignId) =>
        _campaigns.TryGetValue(campaignId, out var campaign) ? campaign : null;

    public MaintenanceCampaign? Approve(Guid campaignId)
    {
        while (_campaigns.TryGetValue(campaignId, out var campaign))
        {
            if (campaign.Status == MaintenanceCampaignStatus.Approved)
            {
                return campaign;
            }

            var approved = campaign with { Status = MaintenanceCampaignStatus.Approved };
            if (_campaigns.TryUpdate(campaignId, approved, campaign))
            {
                return approved;
            }
        }

        return null;
    }

    public IReadOnlyCollection<MaintenanceCampaign> GetApprovedForDevice(string deviceId) =>
        _campaigns.Values
            .Where(campaign =>
                campaign.Status == MaintenanceCampaignStatus.Approved &&
                campaign.TargetDeviceIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase))
            .OrderBy(campaign => campaign.CreatedAtUtc)
            .ToArray();
}

public interface IMaintenanceJobResultStore
{
    void Append(MaintenanceJobResult result);

    IReadOnlyCollection<MaintenanceJobResult> GetForCampaign(Guid campaignId);
}

public sealed class InMemoryMaintenanceJobResultStore : IMaintenanceJobResultStore
{
    private readonly ConcurrentQueue<MaintenanceJobResult> _results = new();

    public void Append(MaintenanceJobResult result) => _results.Enqueue(result);

    public IReadOnlyCollection<MaintenanceJobResult> GetForCampaign(Guid campaignId) =>
        _results.Where(result => result.CampaignId == campaignId).ToArray();
}
