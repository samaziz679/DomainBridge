using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class AuditStorageSelectorTests
{
    [Fact]
    public void Select_prefers_postgres_when_an_audit_connection_is_configured()
    {
        var mode = AuditStorageSelector.Select(
            isDevelopmentOrTest: false,
            auditConnectionString: "Host=database;Database=domainbridge;Username=app");

        Assert.Equal(AuditStorageMode.Postgres, mode);
    }

    [Fact]
    public void Select_allows_memory_only_for_development_or_tests()
    {
        var mode = AuditStorageSelector.Select(
            isDevelopmentOrTest: true,
            auditConnectionString: null);

        Assert.Equal(AuditStorageMode.InMemory, mode);
    }

    [Fact]
    public void Select_rejects_production_without_a_durable_audit_connection()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AuditStorageSelector.Select(
                isDevelopmentOrTest: false,
                auditConnectionString: null));
    }
}
