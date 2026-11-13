using System.Collections.Concurrent;
using DomainBridge.Contracts;
using Npgsql;

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

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Requires a PostgreSQL instance; to be covered by integration tests in tranche 2.4.")]
public sealed class PostgresInventoryStore(NpgsqlDataSource dataSource) : IInventoryStore
{
    public void Upsert(DeviceInventoryReport report)
    {
        using var connection = dataSource.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO device_inventory (device_id, operating_system, architecture, agent_version, captured_at_utc)
            VALUES (@device_id, @operating_system, @architecture, @agent_version, @captured_at_utc)
            ON CONFLICT (device_id) DO UPDATE SET
                operating_system = EXCLUDED.operating_system,
                architecture = EXCLUDED.architecture,
                agent_version = EXCLUDED.agent_version,
                captured_at_utc = EXCLUDED.captured_at_utc;
            """;
        command.Parameters.AddWithValue("device_id", report.DeviceId);
        command.Parameters.AddWithValue("operating_system", report.OperatingSystem);
        command.Parameters.AddWithValue("architecture", report.Architecture);
        command.Parameters.AddWithValue("agent_version", report.AgentVersion);
        command.Parameters.AddWithValue("captured_at_utc", report.CapturedAtUtc);
        command.ExecuteNonQuery();
    }

    public IReadOnlyCollection<ManagedDevice> GetRecent(int limit)
    {
        using var connection = dataSource.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT device_id, operating_system, architecture, agent_version, captured_at_utc
            FROM device_inventory
            ORDER BY captured_at_utc DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("limit", limit);
        using var reader = command.ExecuteReader();
        var devices = new List<ManagedDevice>();
        while (reader.Read())
        {
            devices.Add(new ManagedDevice(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return devices;
    }
}
