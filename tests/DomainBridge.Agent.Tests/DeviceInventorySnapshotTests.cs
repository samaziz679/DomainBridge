using DomainBridge.Agent;
using Xunit;

namespace DomainBridge.Agent.Tests;

public sealed class DeviceInventorySnapshotTests
{
    [Fact]
    public void Capture_creates_technical_device_metadata()
    {
        var snapshot = DeviceInventorySnapshot.Capture("agent/1.0");

        Assert.False(string.IsNullOrWhiteSpace(snapshot.DeviceId));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.OperatingSystem));
        Assert.Equal("agent/1.0", snapshot.AgentVersion);
    }

    [Fact]
    public void Snapshot_contract_contains_no_secret_or_user_profile_data()
    {
        var propertyNames = typeof(DeviceInventorySnapshot)
            .GetProperties()
            .Select(property => property.Name);

        Assert.DoesNotContain(propertyNames, name =>
            name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("profile", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("user", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Capture_rejects_a_missing_agent_version()
    {
        Assert.Throws<ArgumentException>(() => DeviceInventorySnapshot.Capture(" "));
    }

    [Fact]
    public void Inventory_command_writes_a_non_secret_snapshot_for_the_supported_argument()
    {
        using var output = new StringWriter();

        var handled = AgentCommand.TryRun(["--inventory-once"], output, "agent/1.0");

        Assert.True(handled);
        Assert.Contains("DeviceId", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("password", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inventory_command_does_not_handle_unknown_arguments()
    {
        using var output = new StringWriter();

        var handled = AgentCommand.TryRun(["--unsupported"], output, "agent/1.0");

        Assert.False(handled);
        Assert.Empty(output.ToString());
    }
}
