using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

public static class SignedManifestPolicy
{
    public static SignedManifestValidationResult Validate(
        byte[]? manifest,
        byte[]? signature,
        IEnumerable<string>? trustedPublisherThumbprints,
        DateTimeOffset? now = null)
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

        return publisherResult.Error switch
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
    UntrustedPublisher
}

public sealed record SignedManifestValidationResult(SignedManifestError Error)
{
    public bool IsSuccess => Error == SignedManifestError.None;

    public static SignedManifestValidationResult Success() => new(SignedManifestError.None);

    public static SignedManifestValidationResult Failure(SignedManifestError error) => new(error);
}
