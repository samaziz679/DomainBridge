namespace DomainBridge.Contracts;

/// <summary>
/// Risk classification for administrator-controlled operations.
/// Elevated and critical operations require verifiable controls before an agent may run them.
/// </summary>
public enum MaintenanceRiskLevel
{
    Observation = 0,
    Standard = 1,
    Elevated = 2,
    Critical = 3
}

/// <summary>
/// Versioned administrative action metadata. The action implementation is deliberately
/// not embedded in the manifest; it is resolved locally from a signed agent package.
/// </summary>
public sealed record AdminActionManifest(
    string Id,
    string Version,
    MaintenanceRiskLevel RiskLevel,
    IReadOnlySet<string> AllowedParameterNames,
    bool RequiresVerifiedSignature,
    bool RequiresApproval);

public sealed record AdminActionExecutionRequest(
    string ActionId,
    string ActionVersion,
    IReadOnlyDictionary<string, string> Parameters,
    bool HasVerifiedSignature,
    bool HasApproval);

public sealed record AdminActionDecision(
    bool IsAllowed,
    string Reason,
    AdminActionManifest? Manifest);

public static class AdminActionPolicy
{
    private static readonly IReadOnlyList<AdminActionManifest> RegisteredActions =
    [
        new(
            "windows-update-reset",
            "1.0",
            MaintenanceRiskLevel.Elevated,
            new HashSet<string>(StringComparer.Ordinal),
            RequiresVerifiedSignature: true,
            RequiresApproval: true),
        new(
            "group-policy-refresh",
            "1.0",
            MaintenanceRiskLevel.Standard,
            new HashSet<string>(StringComparer.Ordinal),
            RequiresVerifiedSignature: true,
            RequiresApproval: true)
    ];

    public static AdminActionDecision Evaluate(AdminActionExecutionRequest request)
    {
        var candidates = RegisteredActions
            .Where(manifest => string.Equals(manifest.Id, request.ActionId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length == 0)
        {
            return new(false, "action_not_registered", null);
        }

        var manifest = candidates.SingleOrDefault(candidate =>
            string.Equals(candidate.Version, request.ActionVersion, StringComparison.Ordinal));
        if (manifest is null)
        {
            return new(false, "version_not_registered", null);
        }

        if (request.Parameters.Keys.Any(parameter =>
            string.IsNullOrWhiteSpace(parameter) || !manifest.AllowedParameterNames.Contains(parameter)))
        {
            return new(false, "unsupported_parameter", manifest);
        }

        if (manifest.RequiresVerifiedSignature && !request.HasVerifiedSignature)
        {
            return new(false, "signature_required", manifest);
        }

        if (manifest.RequiresApproval && !request.HasApproval)
        {
            return new(false, "approval_required", manifest);
        }

        return new(true, "allowed", manifest);
    }
}
