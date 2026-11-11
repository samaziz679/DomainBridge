using DomainBridge.DirectoryConnector;
using Xunit;

namespace DomainBridge.DirectoryConnector.Tests;

public sealed class SimulatedDirectoryConnectorTests
{
    [Fact]
    public void Plan_creates_a_non_executable_mapping_between_domains()
    {
        var connector = new SimulatedDirectoryConnector("domain-a.example", "domain-b.example");

        var plan = connector.Plan("alex.martin");

        Assert.Equal("domain-a.example", plan.Source.Domain);
        Assert.Equal("domain-b.example", plan.Target.Domain);
        Assert.Equal("alex.martin", plan.Source.AccountName);
        Assert.Equal("alex.martin", plan.Target.AccountName);
        Assert.Equal(MigrationPlanStatus.Simulated, plan.Status);
    }

    [Fact]
    public void Constructor_rejects_identical_source_and_target_domains()
    {
        Assert.Throws<ArgumentException>(() =>
            new SimulatedDirectoryConnector("domain-a.example", "DOMAIN-A.EXAMPLE"));
    }

    [Fact]
    public void Plan_rejects_a_missing_account_name()
    {
        var connector = new SimulatedDirectoryConnector("domain-a.example", "domain-b.example");

        Assert.Throws<ArgumentException>(() => connector.Plan(" "));
    }
}
