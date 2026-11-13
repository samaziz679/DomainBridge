using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

public static class SignedManifestPolicy
{
    public static SignedManifestValidationResult Validate(
        byte[]? manifest,
        byte[]? signature,
        IEnumerable<string>? trustedPublisherThumbprints,
        DateTimeOffset? now = null)
        => ValidateInternal(manifest, signature, trustedPublisherThumbprints, trustedRoots: null, now);

    public static SignedManifestValidationResult Validate(
        byte[]? manifest,
        byte[]? signature,
        IEnumerable<string>? trustedPublisherThumbprints,
        X509Certificate2Collection trustedRoots,
        DateTimeOffset? now = null)
        => ValidateInternal(manifest, signature, trustedPublisherThumbprints, trustedRoots, now);

    private static SignedManifestValidationResult ValidateInternal(
        byte[]? manifest,
        byte[]? signature,
        IEnumerable<string>? trustedPublisherThumbprints,
        X509Certificate2Collection? trustedRoots,
        DateTimeOffset? now)
    {
        if (manifest is null || manifest.Length == 0)
        {
            return SignedManifestValidationResult.Failure(SignedManifestError.ManifestRequired);
        }

        if (signature is null || signature.Length == 0)
        {
            return SignedManifestValidationResult.Failure(SignedManifestError.SignatureRequired);
        }

        var signedCms = new SignedCms(new ContentInfo(manifest), detached: true);
        try
        {
            signedCms.Decode(signature);
        }
        catch (CryptographicException)
        {
            return SignedManifestValidationResult.Failure(SignedManifestError.SignatureInvalid);
        }

        if (signedCms.SignerInfos.Count != 1)
        {
            return SignedManifestValidationResult.Failure(SignedManifestError.ExactlyOneSignerRequired);
        }

        try
        {
            signedCms.CheckSignature(verifySignatureOnly: true);
        }
        catch (CryptographicException)
        {
            return SignedManifestValidationResult.Failure(SignedManifestError.SignatureInvalid);
        }

        var publisherResult = PublisherCertificatePolicy.Validate(
            signedCms.SignerInfos[0].Certificate,
            trustedPublisherThumbprints,
            now);

        var validationResult = publisherResult.Error switch
        {
            PublisherCertificateError.None => SignedManifestValidationResult.Success(),
            PublisherCertificateError.CertificateRequired =>
                SignedManifestValidationResult.Failure(SignedManifestError.SignerCertificateRequired),
            PublisherCertificateError.CertificateOutsideValidityPeriod =>
                SignedManifestValidationResult.Failure(SignedManifestError.SignerCertificateOutsideValidityPeriod),
            PublisherCertificateError.CodeSigningUsageRequired =>
                SignedManifestValidationResult.Failure(SignedManifestError.SignerCodeSigningUsageRequired),
            PublisherCertificateError.UntrustedPublisher =>
                SignedManifestValidationResult.Failure(SignedManifestError.UntrustedPublisher),
            _ => SignedManifestValidationResult.Failure(SignedManifestError.SignatureInvalid)
        };

        if (!validationResult.IsSuccess || trustedRoots is null)
        {
            return validationResult;
        }

        return PublisherCertificateChainPolicy.Validate(
            signedCms.SignerInfos[0].Certificate,
            trustedRoots,
            now).IsSuccess
            ? SignedManifestValidationResult.Success()
            : SignedManifestValidationResult.Failure(SignedManifestError.SignerChainNotTrusted);
    }
}

public enum SignedManifestError
{
    None = 0,
    ManifestRequired,
    SignatureRequired,
    SignatureInvalid,
    ExactlyOneSignerRequired,
    SignerCertificateRequired,
    SignerCertificateOutsideValidityPeriod,
    SignerCodeSigningUsageRequired,
    UntrustedPublisher,
    SignerChainNotTrusted
}

public sealed record SignedManifestValidationResult(SignedManifestError Error)
{
    public bool IsSuccess => Error == SignedManifestError.None;

    public static SignedManifestValidationResult Success() => new(SignedManifestError.None);

    public static SignedManifestValidationResult Failure(SignedManifestError error) => new(error);
}
