using System.Security.Cryptography.X509Certificates;

public static class PublisherCertificateChainPolicy
{
    public static PublisherCertificateChainValidationResult Validate(
        X509Certificate2? publisherCertificate,
        X509Certificate2Collection? trustedRoots,
        DateTimeOffset? now = null)
    {
        if (publisherCertificate is null)
        {
            return PublisherCertificateChainValidationResult.Failure(
                PublisherCertificateChainError.CertificateRequired);
        }

        if (trustedRoots is null || trustedRoots.Count == 0)
        {
            return PublisherCertificateChainValidationResult.Failure(
                PublisherCertificateChainError.TrustRootRequired);
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationTime = (now ?? DateTimeOffset.UtcNow).UtcDateTime;

        foreach (var root in trustedRoots)
        {
            chain.ChainPolicy.CustomTrustStore.Add(root);
        }

        return chain.Build(publisherCertificate)
            ? PublisherCertificateChainValidationResult.Success()
            : PublisherCertificateChainValidationResult.Failure(
                PublisherCertificateChainError.ChainNotTrusted);
    }
}

public enum PublisherCertificateChainError
{
    None = 0,
    CertificateRequired,
    TrustRootRequired,
    ChainNotTrusted
}

public sealed record PublisherCertificateChainValidationResult(PublisherCertificateChainError Error)
{
    public bool IsSuccess => Error == PublisherCertificateChainError.None;

    public static PublisherCertificateChainValidationResult Success() => new(PublisherCertificateChainError.None);

    public static PublisherCertificateChainValidationResult Failure(PublisherCertificateChainError error) => new(error);
}
