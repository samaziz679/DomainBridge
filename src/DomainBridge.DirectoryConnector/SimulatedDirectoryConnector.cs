namespace DomainBridge.DirectoryConnector;

/// <summary>
/// Produces a deterministic migration plan for a synthetic laboratory only.
/// It has no LDAP, Active Directory, credential or workstation side effects.
/// </summary>
public sealed class SimulatedDirectoryConnector
{
    private readonly string _sourceDomain;
    private readonly string _targetDomain;

    public SimulatedDirectoryConnector(string sourceDomain, string targetDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDomain);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDomain);

        if (string.Equals(sourceDomain, targetDomain, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Source and target domains must differ.");
        }

        _sourceDomain = sourceDomain.Trim();
        _targetDomain = targetDomain.Trim();
    }

    public MigrationPlan Plan(string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        var normalizedAccount = accountName.Trim();
        return new MigrationPlan(
            new DirectoryIdentity(_sourceDomain, normalizedAccount),
            new DirectoryIdentity(_targetDomain, normalizedAccount),
            MigrationPlanStatus.Simulated);
    }
}

public sealed record DirectoryIdentity(string Domain, string AccountName);

public sealed record MigrationPlan(
    DirectoryIdentity Source,
    DirectoryIdentity Target,
    MigrationPlanStatus Status);

public enum MigrationPlanStatus
{
    Simulated = 0
}
