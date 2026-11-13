using System.Globalization;
using DomainBridge.Contracts;
using Microsoft.Data.Sqlite;

/// <summary>
/// Where the server keeps its state. On the administrator workstation this is a
/// single embedded database file; no database server has to be installed.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Full path of the database file. Empty selects the default location.</summary>
    public string? DatabasePath { get; init; }

    public static string DefaultDatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DomainBridge",
            "Server",
            "domainbridge.db");
}

public static class StorageSelector
{
    /// <summary>
    /// Returns the database path to use, or null for volatile in-memory stores.
    /// Only a development or test host may run without durable storage.
    /// </summary>
    public static string? ResolveDatabasePath(bool isDevelopmentOrTest, StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            return Path.GetFullPath(options.DatabasePath);
        }

        return isDevelopmentOrTest ? null : StorageOptions.DefaultDatabasePath;
    }
}

/// <summary>
/// Owns the embedded database file and its schema. Schema changes are numbered
/// migrations applied in order; the applied number is kept in the file itself.
/// </summary>
public sealed class SqliteDatabase
{
    private static readonly string[] Migrations =
    [
        """
        CREATE TABLE audit_events (
            sequence INTEGER PRIMARY KEY AUTOINCREMENT,
            event_id TEXT NOT NULL UNIQUE,
            occurred_at_utc_ticks INTEGER NOT NULL,
            action TEXT NOT NULL CHECK (length(action) BETWEEN 1 AND 128),
            subject_id TEXT NOT NULL CHECK (length(subject_id) BETWEEN 1 AND 256),
            actor TEXT NOT NULL CHECK (length(actor) BETWEEN 1 AND 256),
            outcome TEXT NOT NULL CHECK (length(outcome) BETWEEN 1 AND 32),
            task_id TEXT NULL CHECK (task_id IS NULL OR length(task_id) BETWEEN 1 AND 128),
            previous_hash TEXT NULL CHECK (previous_hash IS NULL OR length(previous_hash) = 64),
            hash TEXT NOT NULL CHECK (length(hash) = 64)
        );

        CREATE TRIGGER audit_events_no_update BEFORE UPDATE ON audit_events
        BEGIN
            SELECT RAISE(ABORT, 'audit_events is append-only');
        END;

        CREATE TRIGGER audit_events_no_delete BEFORE DELETE ON audit_events
        BEGIN
            SELECT RAISE(ABORT, 'audit_events is append-only');
        END;

        CREATE TABLE device_inventory (
            device_id TEXT PRIMARY KEY COLLATE NOCASE CHECK (length(device_id) BETWEEN 1 AND 256),
            operating_system TEXT NOT NULL CHECK (length(operating_system) BETWEEN 1 AND 256),
            architecture TEXT NOT NULL CHECK (length(architecture) BETWEEN 1 AND 64),
            agent_version TEXT NOT NULL CHECK (length(agent_version) BETWEEN 1 AND 128),
            captured_at_utc_ticks INTEGER NOT NULL
        );
        """
    ];

    private readonly string _connectionString;

    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    public string DatabasePath { get; }

    /// <summary>Number of the newest migration this build knows.</summary>
    public static int CurrentSchemaVersion => Migrations.Length;

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        Execute(connection, null, "PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;");
        return connection;
    }

    /// <summary>Creates the file if needed and brings its schema up to date. Safe to call again.</summary>
    public int Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

        using var connection = Open();
        Execute(connection, null, "PRAGMA journal_mode = WAL;");
        var version = ReadSchemaVersion(connection);
        if (version > Migrations.Length)
        {
            throw new InvalidOperationException(
                $"The database schema (version {version}) is newer than this build supports (version {Migrations.Length}).");
        }

        for (var index = version; index < Migrations.Length; index++)
        {
            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, Migrations[index]);
            Execute(connection, transaction, $"PRAGMA user_version = {(index + 1).ToString(CultureInfo.InvariantCulture)};");
            transaction.Commit();
        }

        return ReadSchemaVersion(connection);
    }

    public int ReadSchemaVersion()
    {
        using var connection = Open();
        return ReadSchemaVersion(connection);
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

public sealed class SqliteAuditStore(SqliteDatabase database) : IAuditStore
{
    private const string Columns =
        "event_id, occurred_at_utc_ticks, action, subject_id, actor, outcome, task_id, previous_hash, hash";

    public IReadOnlyCollection<AuditEvent> Events =>
        Read($"SELECT {Columns} FROM audit_events ORDER BY sequence ASC;", null);

    public int Count
    {
        get
        {
            using var connection = database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM audit_events;";
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    public void Append(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        using var connection = database.Open();

        // An immediate transaction takes the write lock up front: one writer at a
        // time, so every event has a single, unambiguous predecessor.
        using var transaction = connection.BeginTransaction(deferred: false);

        using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = "SELECT 1 FROM audit_events WHERE event_id = $event_id;";
            existing.Parameters.AddWithValue("$event_id", auditEvent.EventId.ToString("N"));
            if (existing.ExecuteScalar() is not null)
            {
                // Same event delivered twice: recording it once is the correct outcome.
                return;
            }
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
                VALUES ($event_id, $ticks, $action, $subject_id, $actor, $outcome, $task_id, $previous_hash, $hash);
                """;
            insert.Parameters.AddWithValue("$event_id", stored.EventId.ToString("N"));
            insert.Parameters.AddWithValue("$ticks", stored.OccurredAtUtc.UtcTicks);
            insert.Parameters.AddWithValue("$action", stored.Action);
            insert.Parameters.AddWithValue("$subject_id", stored.SubjectId);
            insert.Parameters.AddWithValue("$actor", stored.Actor);
            insert.Parameters.AddWithValue("$outcome", stored.Outcome);
            insert.Parameters.AddWithValue("$task_id", (object?)stored.TaskId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$previous_hash", (object?)stored.PreviousHash ?? DBNull.Value);
            insert.Parameters.AddWithValue("$hash", stored.Hash!);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyCollection<AuditEvent> GetRecent(int limit) =>
        Read($"SELECT {Columns} FROM audit_events ORDER BY sequence DESC LIMIT $limit;", limit);

    private AuditEvent[] Read(string sql, int? limit)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (limit.HasValue)
        {
            command.Parameters.AddWithValue("$limit", limit.Value);
        }

        using var reader = command.ExecuteReader();
        var events = new List<AuditEvent>();
        while (reader.Read())
        {
            events.Add(new AuditEvent(
                Guid.ParseExact(reader.GetString(0), "N"),
                new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8)));
        }

        return [.. events];
    }
}

public sealed class SqliteInventoryStore(SqliteDatabase database) : IInventoryStore
{
    public void Upsert(DeviceInventoryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO device_inventory (device_id, operating_system, architecture, agent_version, captured_at_utc_ticks)
            VALUES ($device_id, $operating_system, $architecture, $agent_version, $ticks)
            ON CONFLICT (device_id) DO UPDATE SET
                operating_system = excluded.operating_system,
                architecture = excluded.architecture,
                agent_version = excluded.agent_version,
                captured_at_utc_ticks = excluded.captured_at_utc_ticks;
            """;
        command.Parameters.AddWithValue("$device_id", report.DeviceId);
        command.Parameters.AddWithValue("$operating_system", report.OperatingSystem);
        command.Parameters.AddWithValue("$architecture", report.Architecture);
        command.Parameters.AddWithValue("$agent_version", report.AgentVersion);
        command.Parameters.AddWithValue("$ticks", report.CapturedAtUtc.UtcTicks);
        command.ExecuteNonQuery();
    }

    public IReadOnlyCollection<ManagedDevice> GetRecent(int limit)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT device_id, operating_system, architecture, agent_version, captured_at_utc_ticks
            FROM device_inventory
            ORDER BY captured_at_utc_ticks DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader();
        var devices = new List<ManagedDevice>();
        while (reader.Read())
        {
            devices.Add(new ManagedDevice(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                new DateTimeOffset(reader.GetInt64(4), TimeSpan.Zero)));
        }

        return [.. devices];
    }
}
