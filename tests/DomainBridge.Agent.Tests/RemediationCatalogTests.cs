using DomainBridge.Agent;
using Xunit;

namespace DomainBridge.Agent.Tests;

public sealed class RemediationCatalogTests
{
    [Fact]
    public void Catalog_exposes_only_known_non_arbitrary_maintenance_actions()
    {
        var actions = RemediationCatalog.All;

        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Id));
            Assert.False(string.IsNullOrWhiteSpace(action.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(action.Description));
            Assert.DoesNotContain("powershell", action.Id, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Catalog_marks_state_changing_actions_as_requiring_approval()
    {
        var action = RemediationCatalog.Find("windows-update-reset");

        Assert.NotNull(action);
        Assert.True(action!.RequiresApproval);
        Assert.True(action.MayRequireRestart);
    }

    [Fact]
    public void Catalog_does_not_resolve_unknown_or_free_form_commands()
    {
        Assert.Null(RemediationCatalog.Find("Invoke-Expression arbitrary-command"));
        Assert.Null(RemediationCatalog.Find(""));
    }
}
