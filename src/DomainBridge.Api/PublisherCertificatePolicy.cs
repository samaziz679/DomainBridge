using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public static class PublisherCertificatePolicy
{
    private const string CodeSigningOid = "1.3.6.1.5.5.7.3.3";

    public static PublisherCertificateValidationResult Validate(
        X509Certificate2? certificate,
        IEnumerable<string>? trustedPublisherThumbprints,
        DateTimeOffset? now = null)
    {
        if (certificate is null)
        {
            return PublisherCertificateValidationResult.Failure(PublisherCertificateError.CertificateRequired);
        }

        var evaluatedAt = now ?? DateTimeOffset.UtcNow;
        if (evaluatedAt < certificate.NotBefore || evaluatedAt > certificate.NotAfter)
        {
            return PublisherCertificateValidationResult.Failure(
                PublisherCertificateError.CertificateOutsideValidityPeriod);
        }

        if (!HasCodeSigningUsage(certificate))
        {
            return PublisherCertificateValidationResult.Failure(
                PublisherCertificateError.CodeSigningUsageRequired);
        }

        var trustedThumbprints = (trustedPublisherThumbprints ?? Array.Empty<string>())
            .Select(NormalizeThumbprint)
            .Where(thumbprint => thumbprint.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!trustedThumbprints.Contains(NormalizeThumbprint(certificate.Thumbprint)))
        {
            return PublisherCertificateValidationResult.Failure(PublisherCertificateError.UntrustedPublisher);
        }

        return PublisherCertificateValidationResult.Success();
    }

    private static bool HasCodeSigningUsage(X509Certificate2 certificate) =>
        certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SelectMany(extension => extension.EnhancedKeyUsages.Cast<Oid>())
            .Any(usage => usage.Value == CodeSigningOid);

    private static string NormalizeThumbprint(string? thumbprint) =>
        thumbprint?.Replace(" ", string.Empty, StringComparison.Ordinal).Trim() ?? string.Empty;
}

public enum PublisherCertificateError
{
    None = 0,
    CertificateRequired,
    CertificateOutsideValidityPeriod,
    CodeSigningUsageRequired,
    UntrustedPublisher
}

public sealed record PublisherCertificateValidationResult(PublisherCertificateError Error)
{
    public bool IsSuccess => Error == PublisherCertificateError.None;

    public static PublisherCertificateValidationResult Success() =>
        new(PublisherCertificateError.None);

    public static PublisherCertificateValidationResult Failure(PublisherCertificateError error) =>
        new(error);
}
