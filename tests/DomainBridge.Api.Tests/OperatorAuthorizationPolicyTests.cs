using System.Security.Claims;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class OperatorAuthorizationPolicyTests
{
    [Fact]
    public void Evaluate_accepts_an_authenticated_operator_with_the_required_entra_role()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", "DomainBridge.Admin")],
            authenticationType: "Test"));

        var result = OperatorAuthorizationPolicy.Evaluate(principal, "DomainBridge.Admin");

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public void Evaluate_rejects_an_unauthenticated_or_wrong_role_operator()
    {
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity());
        var wrongRole = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "DomainBridge.Reader")],
            authenticationType: "Test"));

        Assert.Equal(OperatorAuthorizationError.AuthenticationRequired,
            OperatorAuthorizationPolicy.Evaluate(unauthenticated, "DomainBridge.Admin").Error);
        Assert.Equal(OperatorAuthorizationError.RequiredRoleMissing,
            OperatorAuthorizationPolicy.Evaluate(wrongRole, "DomainBridge.Admin").Error);
    }
}
