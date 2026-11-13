using System.Security.Claims;

public static class OperatorAuthorizationPolicy
{
    private static readonly string[] RoleClaimTypes = [ClaimTypes.Role, "roles"];

    public static OperatorAuthorizationResult Evaluate(ClaimsPrincipal? principal, string? requiredRole)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return OperatorAuthorizationResult.Failure(OperatorAuthorizationError.AuthenticationRequired);
        }

        if (string.IsNullOrWhiteSpace(requiredRole))
        {
            return OperatorAuthorizationResult.Failure(OperatorAuthorizationError.RequiredRoleMissing);
        }

        var hasRole = principal.Claims.Any(claim =>
            RoleClaimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase) &&
            string.Equals(claim.Value, requiredRole.Trim(), StringComparison.OrdinalIgnoreCase));

        return hasRole
            ? OperatorAuthorizationResult.Success()
            : OperatorAuthorizationResult.Failure(OperatorAuthorizationError.RequiredRoleMissing);
    }
}

public enum OperatorAuthorizationError
{
    None = 0,
    AuthenticationRequired,
    RequiredRoleMissing
}

public sealed record OperatorAuthorizationResult(OperatorAuthorizationError Error)
{
    public bool IsAuthorized => Error == OperatorAuthorizationError.None;
    public static OperatorAuthorizationResult Success() => new(OperatorAuthorizationError.None);
    public static OperatorAuthorizationResult Failure(OperatorAuthorizationError error) => new(error);
}
