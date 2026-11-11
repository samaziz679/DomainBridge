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

    void Append(AuditEvent auditEvent);
}

public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public IReadOnlyCollection<AuditEvent> Events => _events.ToArray();

    public void Append(AuditEvent auditEvent)
    {
        _events.Enqueue(auditEvent);
    }
}

public sealed class PostgresAuditStore(NpgsqlDataSource dataSource) : IAuditStore
{
    public IReadOnlyCollection<AuditEvent> Events =>
        throw new NotSupportedException("PostgreSQL audit events are queried through a read model, not the write store.");

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
