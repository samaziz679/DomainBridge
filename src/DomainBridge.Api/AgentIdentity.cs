using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Configuration of the authorities allowed to issue agent certificates.
/// Thumbprints are public values; no secret belongs in this section.
/// </summary>
public sealed class AgentIdentityOptions
{
    public const string SectionName = "AgentIdentity";

    /// <summary>
    /// Forces certificate identity even in development and test hosts.
    /// It can only tighten the default: production always requires a certificate.
    /// </summary>
    public bool Enforce { get; init; }

    public IReadOnlyCollection<string> TrustedIssuerThumbprints { get; init; } = [];

    public IReadOnlySet<string> NormalizedIssuerThumbprints() =>
        TrustedIssuerThumbprints
            .Where(thumbprint => !string.IsNullOrWhiteSpace(thumbprint))
            .Select(thumbprint => thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

public interface IAgentIssuerChainReader
{
    /// <summary>Thumbprints of the authorities above the certificate, excluding the certificate itself.</summary>
    IReadOnlyCollection<string> GetIssuerThumbprints(X509Certificate2 certificate);
}

/// <summary>
/// Reads the issuing chain of an agent certificate. It does not decide trust:
/// the TLS layer validates the chain, and <see cref="AgentIdentityResolver"/>
/// then requires one of these authorities to be pinned.
/// </summary>
public sealed class X509AgentIssuerChainReader(X509Certificate2Collection? additionalCertificates = null)
    : IAgentIssuerChainReader
{
    public IReadOnlyCollection<string> GetIssuerThumbprints(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        if (additionalCertificates is not null)
        {
            chain.ChainPolicy.ExtraStore.AddRange(additionalCertificates);
        }

        chain.Build(certificate);

        return chain.ChainElements
            .Skip(1)
            .Select(element => element.Certificate.Thumbprint)
            .ToArray();
    }
}

public enum AgentIdentityError
{
    None = 0,
    CertificateRequired,
    CertificateInvalid,
    UntrustedIssuer,
    DeviceNameMissing
}

public sealed record AgentIdentityResult(AgentIdentityError Error, string? DeviceId, string? Thumbprint)
{
    public bool IsSuccess => Error == AgentIdentityError.None;

    /// <summary>True when the caller presented no certificate on a host that does not require one.</summary>
    public bool IsAnonymous => IsSuccess && DeviceId is null;

    public static AgentIdentityResult Anonymous() => new(AgentIdentityError.None, null, null);

    public static AgentIdentityResult Success(string deviceId, string thumbprint) =>
        new(AgentIdentityError.None, deviceId, thumbprint);

    public static AgentIdentityResult Failure(AgentIdentityError error) => new(error, null, null);
}

/// <summary>
/// Derives the identity of an agent from its client certificate only.
/// Nothing in the request URL or body can change who the caller is.
/// </summary>
public static class AgentIdentityResolver
{
    public static AgentIdentityResult Resolve(
        X509Certificate2? certificate,
        bool required,
        AgentIdentityOptions options,
        IAgentIssuerChainReader chainReader,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(chainReader);

        if (certificate is null)
        {
            return required
                ? AgentIdentityResult.Failure(AgentIdentityError.CertificateRequired)
                : AgentIdentityResult.Anonymous();
        }

        if (!AgentCertificatePolicy.Validate(certificate, certificate.Thumbprint, now).IsSuccess)
        {
            return AgentIdentityResult.Failure(AgentIdentityError.CertificateInvalid);
        }

        var pinned = options.NormalizedIssuerThumbprints();
        if (required || pinned.Count > 0)
        {
            var issuers = chainReader.GetIssuerThumbprints(certificate);
            if (!issuers.Any(pinned.Contains))
            {
                return AgentIdentityResult.Failure(AgentIdentityError.UntrustedIssuer);
            }
        }

        var deviceId = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false)?.Trim();
        return string.IsNullOrEmpty(deviceId)
            ? AgentIdentityResult.Failure(AgentIdentityError.DeviceNameMissing)
            : AgentIdentityResult.Success(deviceId, certificate.Thumbprint);
    }
}
