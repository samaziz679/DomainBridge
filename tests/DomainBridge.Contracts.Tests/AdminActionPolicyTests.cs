using DomainBridge.Contracts;
using Xunit;

namespace DomainBridge.Contracts.Tests;

public sealed class AdminActionPolicyTests
{
    [Fact]
    public void Elevated_action_requires_a_matching_version_signature_and_approval()
    {
        var request = new AdminActionExecutionRequest(
            "windows-update-reset",
            "1.0",
            new Dictionary<string, string>(),
            HasVerifiedSignature: true,
            HasApproval: true);

        var decision = AdminActionPolicy.Evaluate(request);

        Assert.True(decision.IsAllowed);
        Assert.Equal(MaintenanceRiskLevel.Elevated, decision.Manifest!.RiskLevel);
    }

    [Theory]
    [InlineData(false, true, "signature_required")]
    [InlineData(true, false, "approval_required")]
    public void Elevated_action_is_denied_when_required_controls_are_missing(
        bool hasVerifiedSignature,
        bool hasApproval,
        string expectedReason)
    {
        var decision = AdminActionPolicy.Evaluate(new AdminActionExecutionRequest(
            "windows-update-reset", "1.0", new Dictionary<string, string>(), hasVerifiedSignature, hasApproval));

        Assert.False(decision.IsAllowed);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Fact]
    public void Policy_rejects_unknown_parameters_and_unknown_actions()
    {
        var unknownParameter = AdminActionPolicy.Evaluate(new AdminActionExecutionRequest(
            "windows-update-reset", "1.0", new Dictionary<string, string> { ["script"] = "arbitrary" }, true, true));
        var unknownAction = AdminActionPolicy.Evaluate(new AdminActionExecutionRequest(
            "Invoke-Expression", "1.0", new Dictionary<string, string>(), true, true));

        Assert.Equal("unsupported_parameter", unknownParameter.Reason);
        Assert.Equal("action_not_registered", unknownAction.Reason);
    }
}
