using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class AgentCertificatePolicy
{
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    public static AgentCertificateValidationResult Validate(
        X509Certificate2? certificate,
        string? claimedThumbprint,
        DateTimeOffset? now = null)
    {
        if (certificate is null)
        {
            return AgentCertificateValidationResult.Failure(AgentCertificateError.CertificateRequired);
        }

        var evaluatedAt = now ?? DateTimeOffset.UtcNow;
        if (evaluatedAt < certificate.NotBefore || evaluatedAt > certificate.NotAfter)
        {
            return AgentCertificateValidationResult.Failure(AgentCertificateError.CertificateOutsideValidityPeriod);
        }

        if (!HasClientAuthenticationUsage(certificate))
        {
            return AgentCertificateValidationResult.Failure(AgentCertificateError.ClientAuthenticationUsageRequired);
        }

        if (!string.Equals(
                certificate.Thumbprint,
                claimedThumbprint?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return AgentCertificateValidationResult.Failure(AgentCertificateError.ThumbprintMismatch);
        }

        return AgentCertificateValidationResult.Success();
    }

    private static bool HasClientAuthenticationUsage(X509Certificate2 certificate) =>
        certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(extension => extension.EnhancedKeyUsages.Cast<Oid>())
            .Any(usage => usage.Value == ClientAuthenticationOid);
}

public enum AgentCertificateError
{
    None = 0,
    CertificateRequired,
    CertificateOutsideValidityPeriod,
    ClientAuthenticationUsageRequired,
    ThumbprintMismatch
}

public sealed record AgentCertificateValidationResult(AgentCertificateError Error)
{
    public bool IsSuccess => Error == AgentCertificateError.None;

    public static AgentCertificateValidationResult Success() =>
        new(AgentCertificateError.None);

    public static AgentCertificateValidationResult Failure(AgentCertificateError error) =>
        new(error);
}
