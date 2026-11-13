using System.Collections.Concurrent;
using DomainBridge.Contracts;

public sealed record ManagedDevice(
    string DeviceId,
    string OperatingSystem,
    string Architecture,
    string AgentVersion,
    DateTimeOffset CapturedAtUtc);

public interface IInventoryStore
{
    void Upsert(DeviceInventoryReport report);

    IReadOnlyCollection<ManagedDevice> GetRecent(int limit);
}

public sealed class InMemoryInventoryStore : IInventoryStore
{
    private readonly ConcurrentDictionary<string, ManagedDevice> _devices = new(StringComparer.OrdinalIgnoreCase);

    public void Upsert(DeviceInventoryReport report)
    {
        _devices[report.DeviceId] = new ManagedDevice(
            report.DeviceId,
            report.OperatingSystem,
            report.Architecture,
            report.AgentVersion,
            report.CapturedAtUtc);
    }

    public IReadOnlyCollection<ManagedDevice> GetRecent(int limit) =>
        _devices.Values.OrderByDescending(device => device.CapturedAtUtc).Take(limit).ToArray();
}
