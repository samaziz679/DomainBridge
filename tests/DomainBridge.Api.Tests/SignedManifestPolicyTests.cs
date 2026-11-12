using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class SignedManifestPolicyTests
{
    [Fact]
    public void Validate_accepts_a_detached_manifest_signed_by_a_trusted_publisher()
    {
        using var certificate = CreateCodeSigningCertificate();
        var manifest = "{\"actionId\":\"group-policy-refresh\",\"version\":\"1.0\"}"u8.ToArray();
        var signature = SignDetached(manifest, certificate);

        var result = SignedManifestPolicy.Validate(manifest, signature, new[] { certificate.Thumbprint! });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_rejects_a_signature_when_the_manifest_content_has_changed()
    {
        using var certificate = CreateCodeSigningCertificate();
        var manifest = "{\"actionId\":\"group-policy-refresh\",\"version\":\"1.0\"}"u8.ToArray();
        var signature = SignDetached(manifest, certificate);
        var modifiedManifest = "{\"actionId\":\"windows-update-reset\",\"version\":\"1.0\"}"u8.ToArray();

        var result = SignedManifestPolicy.Validate(modifiedManifest, signature, new[] { certificate.Thumbprint! });

        Assert.Equal(SignedManifestError.SignatureInvalid, result.Error);
    }

    [Fact]
    public void Validate_rejects_a_manifest_signed_by_an_untrusted_publisher()
    {
        using var certificate = CreateCodeSigningCertificate();
        var manifest = "{\"actionId\":\"group-policy-refresh\",\"version\":\"1.0\"}"u8.ToArray();
        var signature = SignDetached(manifest, certificate);

        var result = SignedManifestPolicy.Validate(
            manifest,
            signature,
            new[] { "0000000000000000000000000000000000000000" });

        Assert.Equal(SignedManifestError.UntrustedPublisher, result.Error);
    }

    [Fact]
    public void Validate_rejects_an_attached_cms_package()
    {
        using var certificate = CreateCodeSigningCertificate();
        var manifest = "{\"actionId\":\"group-policy-refresh\",\"version\":\"1.0\"}"u8.ToArray();
        var attachedCms = SignAttached(manifest, certificate);

        var result = SignedManifestPolicy.Validate(manifest, attachedCms, new[] { certificate.Thumbprint! });

        Assert.Equal(SignedManifestError.SignatureInvalid, result.Error);
    }

    private static byte[] SignDetached(byte[] manifest, X509Certificate2 certificate) =>
        Sign(manifest, certificate, detached: true);

    private static byte[] SignAttached(byte[] manifest, X509Certificate2 certificate) =>
        Sign(manifest, certificate, detached: false);

    private static byte[] Sign(byte[] manifest, X509Certificate2 certificate, bool detached)
    {
        var cms = new SignedCms(new ContentInfo(manifest), detached);
        cms.ComputeSignature(new CmsSigner(certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        });

        return cms.Encode();
    }

    private static X509Certificate2 CreateCodeSigningCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=DomainBridge Test Publisher",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.3") },
            critical: false));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
