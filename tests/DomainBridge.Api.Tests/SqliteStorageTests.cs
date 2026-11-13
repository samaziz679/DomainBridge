using DomainBridge.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class SqliteStorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "domainbridge-tests-" + Guid.NewGuid().ToString("N"));

    private string DatabasePath => Path.Combine(_directory, "nested", "domainbridge.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SqliteDatabase NewDatabase()
    {
        var database = new SqliteDatabase(DatabasePath);
        database.Initialize();
        return database;
    }

    private static AuditEvent NewEvent(string action, string? taskId = null) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, action, "SPARE01", AuditActors.Agent("SPARE01"), AuditOutcomes.Success, taskId);

    [Fact]
    public void Initialize_creates_the_file_and_is_safe_to_repeat()
    {
        var database = NewDatabase();

        Assert.True(File.Exists(DatabasePath));
        Assert.Equal(SqliteDatabase.CurrentSchemaVersion, database.Initialize());
        Assert.Equal(SqliteDatabase.CurrentSchemaVersion, database.ReadSchemaVersion());
    }

    [Fact]
    public void A_database_from_a_newer_build_is_refused()
    {
        var database = NewDatabase();
        using (var connection = database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version = 999;";
            command.ExecuteNonQuery();
        }

        Assert.Throws<InvalidOperationException>(() => database.Initialize());
    }

    [Fact]
    public void Audit_events_survive_a_restart_with_an_intact_chain()
    {
        var first = new SqliteAuditStore(NewDatabase());
        first.Append(NewEvent("a"));
        first.Append(NewEvent("b", taskId: "task-1"));

        var afterRestart = new SqliteAuditStore(NewDatabase());
        afterRestart.Append(NewEvent("c"));

        var events = afterRestart.Events.ToArray();
        Assert.Equal(["a", "b", "c"], events.Select(auditEvent => auditEvent.Action));
        Assert.Equal("task-1", events[1].TaskId);
        Assert.Equal("agent:SPARE01", events[1].Actor);
        Assert.Equal(-1, AuditChain.FindFirstBrokenLink(events));
        Assert.Equal(3, afterRestart.Count);
        Assert.Equal(["c", "b"], afterRestart.GetRecent(2).Select(auditEvent => auditEvent.Action));
    }

    [Theory]
    [InlineData("UPDATE audit_events SET actor = 'agent:SOMEONE-ELSE';")]
    [InlineData("DELETE FROM audit_events;")]
    public void The_database_itself_refuses_to_change_or_remove_an_audit_event(string sql)
    {
        var database = NewDatabase();
        new SqliteAuditStore(database).Append(NewEvent("a"));

        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var failure = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        Assert.Contains("append-only", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_event_delivered_twice_is_recorded_once()
    {
        var store = new SqliteAuditStore(NewDatabase());
        var auditEvent = NewEvent("a");

        store.Append(auditEvent);
        store.Append(auditEvent);

        Assert.Equal(1, store.Count);
        Assert.Equal(-1, AuditChain.FindFirstBrokenLink(store.Events.ToArray()));
    }

    [Fact]
    public async Task Concurrent_appends_keep_the_stored_chain_intact()
    {
        var database = NewDatabase();

        await Task.WhenAll(Enumerable.Range(0, 40).Select(index =>
            Task.Run(() => new SqliteAuditStore(database).Append(NewEvent($"a{index}")))));

        var events = new SqliteAuditStore(database).Events.ToArray();
        Assert.Equal(40, events.Length);
        Assert.Equal(-1, AuditChain.FindFirstBrokenLink(events));
    }

    [Fact]
    public void Inventory_keeps_one_row_per_device_whatever_the_letter_case()
    {
        var store = new SqliteInventoryStore(NewDatabase());
        var earlier = new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

        store.Upsert(new DeviceInventoryReport("SPARE01", "Windows 11", "X64", "agent/1.0", earlier));
        store.Upsert(new DeviceInventoryReport("spare01", "Windows 11", "X64", "agent/1.1", earlier.AddHours(1)));
        store.Upsert(new DeviceInventoryReport("SPARE02", "Windows 10", "X64", "agent/1.0", earlier.AddHours(2)));

        var devices = new SqliteInventoryStore(NewDatabase()).GetRecent(10).ToArray();
        Assert.Equal(["SPARE02", "SPARE01"], devices.Select(device => device.DeviceId));
        Assert.Equal("agent/1.1", devices[1].AgentVersion);
        Assert.Equal(earlier.AddHours(1), devices[1].CapturedAtUtc);
    }

    [Fact]
    public void Only_development_and_test_hosts_may_run_without_durable_storage()
    {
        Assert.Null(StorageSelector.ResolveDatabasePath(isDevelopmentOrTest: true, new StorageOptions()));
        Assert.Equal(
            StorageOptions.DefaultDatabasePath,
            StorageSelector.ResolveDatabasePath(isDevelopmentOrTest: false, new StorageOptions()));
        Assert.Equal(
            Path.GetFullPath(DatabasePath),
            StorageSelector.ResolveDatabasePath(isDevelopmentOrTest: true, new StorageOptions { DatabasePath = DatabasePath }));
    }

    [Fact]
    public async Task The_api_keeps_inventory_and_audit_across_restarts()
    {
        async Task ReportAsync()
        {
            await using var host = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseSetting("Storage:DatabasePath", DatabasePath));
            using var client = host.CreateClient();
            var response = await client.PostAsync("/api/v1/inventory", new StringContent(
                """{"deviceId":"SPARE01","operatingSystem":"Windows 11","architecture":"X64","agentVersion":"agent/1.0","capturedAtUtc":"2026-11-13T00:00:00+00:00"}""",
                System.Text.Encoding.UTF8,
                "application/json"));
            response.EnsureSuccessStatusCode();
        }

        await ReportAsync();
        await ReportAsync();

        await using var restarted = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Storage:DatabasePath", DatabasePath));
        _ = restarted.CreateClient();
        var audit = restarted.Services.GetRequiredService<IAuditStore>();
        var inventory = restarted.Services.GetRequiredService<IInventoryStore>();

        Assert.IsType<SqliteAuditStore>(audit);
        Assert.Equal(2, audit.Count);
        Assert.Equal(-1, AuditChain.FindFirstBrokenLink(audit.Events.ToArray()));
        Assert.Single(inventory.GetRecent(10));
    }
}
