using System.Collections.Concurrent;
using Npgsql;

public sealed record AuditEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string SubjectId);

public interface IAuditStore
{
    IReadOnlyCollection<AuditEvent> Events { get; }

    int Count { get; }

    IReadOnlyCollection<AuditEvent> GetRecent(int limit);

    void Append(AuditEvent auditEvent);
}

public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public IReadOnlyCollection<AuditEvent> Events => _events.ToArray();

    public int Count => _events.Count;

    public void Append(AuditEvent auditEvent)
    {
        _events.Enqueue(auditEvent);
    }

    public IReadOnlyCollection<AuditEvent> GetRecent(int limit) =>
        _events.OrderByDescending(auditEvent => auditEvent.OccurredAtUtc).Take(limit).ToArray();
}

public sealed class PostgresAuditStore(NpgsqlDataSource dataSource) : IAuditStore
{
    public IReadOnlyCollection<AuditEvent> Events =>
        GetRecent(int.MaxValue);

    public int Count
    {
        get
        {
            using var connection = dataSource.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM audit_events;";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public void Append(AuditEvent auditEvent)
    {
        using var connection = dataSource.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit_events (event_id, occurred_at_utc, action, subject_id)
            VALUES (@event_id, @occurred_at_utc, @action, @subject_id);
            """;
        command.Parameters.AddWithValue("event_id", auditEvent.EventId);
        command.Parameters.AddWithValue("occurred_at_utc", auditEvent.OccurredAtUtc);
        command.Parameters.AddWithValue("action", auditEvent.Action);
        command.Parameters.AddWithValue("subject_id", auditEvent.SubjectId);
        command.ExecuteNonQuery();
    }

    public IReadOnlyCollection<AuditEvent> GetRecent(int limit)
    {
        using var connection = dataSource.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT event_id, occurred_at_utc, action, subject_id
            FROM audit_events
            ORDER BY occurred_at_utc DESC
            LIMIT @limit;
            """;
        command.Parameters.AddWithValue("limit", limit);
        using var reader = command.ExecuteReader();
        var events = new List<AuditEvent>();
        while (reader.Read())
        {
            events.Add(new AuditEvent(
                reader.GetGuid(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return events;
    }
}

public enum AuditStorageMode
{
    InMemory = 0,
    Postgres
}

public static class AuditStorageSelector
{
    public static AuditStorageMode Select(
        bool isDevelopmentOrTest,
        string? auditConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(auditConnectionString))
        {
            return AuditStorageMode.Postgres;
        }

        if (isDevelopmentOrTest)
        {
            return AuditStorageMode.InMemory;
        }

        throw new InvalidOperationException(
            "A durable ConnectionStrings:Audit PostgreSQL connection is required outside development and test.");
    }
}
