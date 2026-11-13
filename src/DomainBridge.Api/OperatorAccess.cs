using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public static class OperatorRoles
{
    public const string Preparer = "Preparer";
    public const string Approver = "Approver";
    public const string Technician = "Technician";
    public const string Reader = "Reader";

    /// <summary>Claim type carrying a DomainBridge role. Only this API issues it.</summary>
    public const string ClaimType = "domainbridge:role";

    public static readonly IReadOnlyList<string> All = [Preparer, Approver, Technician, Reader];
}

public static class OperatorPolicies
{
    /// <summary>Any operator role may read.</summary>
    public const string Read = "Operator.Read";
    public const string Prepare = "Operator.Prepare";
    public const string Approve = "Operator.Approve";
    public const string Technician = "Operator.Technician";
}

/// <summary>
/// Maps Active Directory groups to operator roles. Groups are named by security
/// identifier, never by display name: a name can exist in both domains, a SID cannot.
/// List the groups of the source and of the target domain while both are in use.
/// </summary>
public sealed partial class OperatorAccessOptions
{
    public const string SectionName = "OperatorAccess";

    /// <summary>
    /// Forces operator authentication even in development and test hosts.
    /// It can only tighten the default: production always requires it.
    /// </summary>
    public bool Enforce { get; init; }

    public IReadOnlyCollection<string> PreparerGroupSids { get; init; } = [];

    public IReadOnlyCollection<string> ApproverGroupSids { get; init; } = [];

    public IReadOnlyCollection<string> TechnicianGroupSids { get; init; } = [];

    public IReadOnlyCollection<string> ReaderGroupSids { get; init; } = [];

    private IEnumerable<(string Role, string Sid)> Entries() =>
        PreparerGroupSids.Select(sid => (OperatorRoles.Preparer, sid))
            .Concat(ApproverGroupSids.Select(sid => (OperatorRoles.Approver, sid)))
            .Concat(TechnicianGroupSids.Select(sid => (OperatorRoles.Technician, sid)))
            .Concat(ReaderGroupSids.Select(sid => (OperatorRoles.Reader, sid)));

    public bool HasAnyGroup() => Entries().Any();

    /// <summary>Configured values that are not well-formed security identifiers.</summary>
    public IReadOnlyCollection<string> InvalidEntries() =>
        Entries().Select(entry => entry.Sid).Where(sid => sid is null || !SidPattern().IsMatch(sid)).ToArray();

    public IReadOnlySet<string> RolesFor(IEnumerable<string> groupSids)
    {
        ArgumentNullException.ThrowIfNull(groupSids);
        var memberOf = groupSids.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Entries()
            .Where(entry => entry.Sid is not null && memberOf.Contains(entry.Sid))
            .Select(entry => entry.Role)
            .ToHashSet(StringComparer.Ordinal);
    }

    [GeneratedRegex(@"^S-1-\d+(-\d+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex SidPattern();
}

/// <summary>
/// Turns the group memberships of an authenticated Windows account into
/// DomainBridge role claims. An account in no configured group gets no role.
/// </summary>
public sealed class OperatorRoleClaimsTransformation(IOptions<OperatorAccessOptions> options) : IClaimsTransformation
{
    private const string Issuer = "DomainBridge";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true ||
            principal.Claims.Any(claim => claim.Type == OperatorRoles.ClaimType && claim.Issuer != Issuer))
        {
            // Never trust a role claim this API did not issue.
            return Task.FromResult(Strip(principal));
        }

        if (principal.Claims.Any(claim => claim.Type == OperatorRoles.ClaimType))
        {
            return Task.FromResult(principal);
        }

        var groupSids = principal.Claims
            .Where(claim => claim.Type is ClaimTypes.GroupSid or ClaimTypes.PrimaryGroupSid)
            .Select(claim => claim.Value);
        var roles = options.Value.RolesFor(groupSids);
        if (roles.Count == 0)
        {
            return Task.FromResult(principal);
        }

        var transformed = new ClaimsPrincipal(principal.Identities);
        transformed.AddIdentity(new ClaimsIdentity(
            roles.Select(role => new Claim(OperatorRoles.ClaimType, role, ClaimValueTypes.String, Issuer))));

        return Task.FromResult(transformed);
    }

    private static ClaimsPrincipal Strip(ClaimsPrincipal principal)
    {
        foreach (var identity in principal.Identities)
        {
            foreach (var claim in identity.FindAll(OperatorRoles.ClaimType).Where(claim => claim.Issuer != Issuer).ToArray())
            {
                identity.TryRemoveClaim(claim);
            }
        }

        return principal;
    }
}
