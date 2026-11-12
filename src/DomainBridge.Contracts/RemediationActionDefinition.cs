namespace DomainBridge.Contracts;

/// <summary>
/// Policy metadata for a supported agent remediation.
/// Script bodies and arbitrary command content are intentionally excluded.
/// </summary>
public sealed record RemediationActionDefinition(
    string Id,
    string DisplayName,
    string Description,
    bool RequiresApproval,
    bool MayRequireRestart);

public static class ApprovedRemediationCatalog
{
    public static readonly IReadOnlyList<RemediationActionDefinition> All =
    [
        new(
            "network-diagnostics",
            "Diagnostiquer la connectivité réseau",
            "Collecte les informations réseau et teste les services requis sans modifier le poste.",
            RequiresApproval: false,
            MayRequireRestart: false),
        new(
            "windows-update-reset",
            "Réinitialiser les composants Windows Update",
            "Répare les composants de mise à jour Windows selon une procédure approuvée.",
            RequiresApproval: true,
            MayRequireRestart: true),
        new(
            "group-policy-refresh",
            "Actualiser les stratégies de groupe",
            "Déclenche une actualisation contrôlée des stratégies de groupe.",
            RequiresApproval: true,
            MayRequireRestart: false),
        new(
            "defender-health-check",
            "Vérifier Microsoft Defender",
            "Collecte l’état de protection Microsoft Defender sans modifier sa configuration.",
            RequiresApproval: false,
            MayRequireRestart: false)
    ];

    public static RemediationActionDefinition? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return All.SingleOrDefault(action =>
            string.Equals(action.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
