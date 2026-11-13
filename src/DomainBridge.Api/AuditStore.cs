using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

/// <summary>
/// One immutable fact: who did what, to which subject, with which outcome.
/// <see cref="PreviousHash"/> and <see cref="Hash"/> are set by the store and link
/// each event to the one before it, so that removing or editing an event is detectable.
/// </summary>
public sealed record AuditEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string SubjectId,
    string Actor = AuditActors.System,
    string Outcome = AuditOutcomes.Success,
    string? TaskId = null,
    string? PreviousHash = null,
    string? Hash = null);

public static class AuditActors
{
    public const string System = "system";
    public const string UnauthenticatedAgent = "agent:unauthenticated";

    public static string Agent(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId) ? UnauthenticatedAgent : $"agent:{deviceId}";
}

public static class AuditOutcomes
{
    public const string Success = "success";
    public const string Denied = "denied";
    public const string Failure = "failure";
}

public static class AuditChain
{
    /// <summary>Returns the event linked to its predecessor and sealed with its own hash.</summary>
    public static AuditEvent Seal(AuditEvent auditEvent, string? previousHash)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        var linked = auditEvent with { PreviousHash = previousHash, Hash = null };
        return linked with { Hash = ComputeHash(linked) };
    }

    public static string ComputeHash(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        // Length-prefixed fields: no value can imitate a field boundary.
        // Time is hashed at microsecond precision, the precision every store keeps.
        string[] fields =
        [
            auditEvent.PreviousHash ?? string.Empty,
            auditEvent.EventId.ToString("N"),
            (auditEvent.OccurredAtUtc.UtcTicks / 10).ToString(CultureInfo.InvariantCulture),
            auditEvent.Action,
            auditEvent.SubjectId,
            auditEvent.Actor,
            auditEvent.Outcome,
            auditEvent.TaskId ?? string.Empty
        ];
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            builder.Append(field.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    /// Checks events given in append order. Returns -1 when the chain is intact,
    /// otherwise the index of the first event that was altered, reordered, or that
    /// follows a removed event.
    /// </summary>
    public static int FindFirstBrokenLink(IReadOnlyList<AuditEvent> eventsInAppendOrder)
    {
        ArgumentNullException.ThrowIfNull(eventsInAppendOrder);

        string? previousHash = null;
        for (var index = 0; index < eventsInAppendOrder.Count; index++)
        {
            var current = eventsInAppendOrder[index];
            if (!string.Equals(current.PreviousHash, previousHash, StringComparison.Ordinal) ||
                !string.Equals(current.Hash, ComputeHash(current), StringComparison.Ordinal))
            {
                return index;
            }

            previousHash = current.Hash;
        }

        return -1;
    }
}

public interface IAuditStore
{
    /// <summary>Every event, in append order.</summary>
    IReadOnlyCollection<AuditEvent> Events { get; }

    int Count { get; }

    IReadOnlyCollection<AuditEvent> GetRecent(int limit);

    void Append(AuditEvent auditEvent);
}

public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly List<AuditEvent> _events = [];
    private readonly Lock _gate = new();

    public IReadOnlyCollection<AuditEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    public void Append(AuditEvent auditEvent)
    {
        lock (_gate)
        {
            _events.Add(AuditChain.Seal(auditEvent, _events.Count == 0 ? null : _events[^1].Hash));
        }
    }

    public IReadOnlyCollection<AuditEvent> GetRecent(int limit)
    {
        lock (_gate)
        {
            return _events.AsEnumerable().Reverse().Take(limit).ToArray();
        }
    }
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Requires a PostgreSQL instance; to be covered by integration tests in tranche 2.4.")]
public sealed class PostgresAuditStore(NpgsqlDataSource dataSource) : IAuditStore
{
    private const string Columns =
        "event_id, occurred_at_utc, action, subject_id, actor, outcome, task_id, previous_hash, hash";

    public IReadOnlyCollection<AuditEvent> Events =>
        Read($"SELECT {Columns} FROM audit_events ORDER BY sequence ASC;", null);

    public int Count
    {
        get
        {
            using var connection = dataSource.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM audit_events;";
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    public void Append(AuditEvent auditEvent)
    {
        using var connection = dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();

        // One writer at a time: the chain needs a single, unambiguous predecessor.
        using (var guard = connection.CreateCommand())
        {
            guard.Transaction = transaction;
            guard.CommandText = "LOCK TABLE audit_events IN SHARE ROW EXCLUSIVE MODE;";
            guard.ExecuteNonQuery();
        }

        string? previousHash;
        using (var last = connection.CreateCommand())
        {
            last.Transaction = transaction;
            last.CommandText = "SELECT hash FROM audit_events ORDER BY sequence DESC LIMIT 1;";
            previousHash = last.ExecuteScalar() as string;
        }

        var stored = AuditChain.Seal(auditEvent, previousHash);
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = $"""
                INSERT INTO audit_events ({Columns})
                VALUES (@event_id, @occurred_at_utc, @action, @subject_id, @actor, @outcome, @task_id, @previous_hash, @hash);
                """;
            insert.Parameters.AddWithValue("event_id", stored.EventId);
            insert.Parameters.AddWithValue("occurred_at_utc", stored.OccurredAtUtc);
            insert.Parameters.AddWithValue("action", stored.Action);
            insert.Parameters.AddWithValue("subject_id", stored.SubjectId);
            insert.Parameters.AddWithValue("actor", stored.Actor);
            insert.Parameters.AddWithValue("outcome", stored.Outcome);
            insert.Parameters.AddWithValue("task_id", (object?)stored.TaskId ?? DBNull.Value);
            insert.Parameters.AddWithValue("previous_hash", (object?)stored.PreviousHash ?? DBNull.Value);
            insert.Parameters.AddWithValue("hash", stored.Hash!);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyCollection<AuditEvent> GetRecent(int limit) =>
        Read($"SELECT {Columns} FROM audit_events ORDER BY sequence DESC LIMIT @limit;", limit);

    private AuditEvent[] Read(string sql, int? limit)
    {
        using var connection = dataSource.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (limit.HasValue)
        {
            command.Parameters.AddWithValue("limit", limit.Value);
        }

        using var reader = command.ExecuteReader();
        var events = new List<AuditEvent>();
        while (reader.Read())
        {
            events.Add(new AuditEvent(
                reader.GetGuid(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return [.. events];
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
