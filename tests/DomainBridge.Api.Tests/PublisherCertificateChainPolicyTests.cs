using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class PublisherCertificateChainPolicyTests
{
    [Fact]
    public void Validate_accepts_a_publisher_chained_to_a_configured_custom_root()
    {
        using var root = CreateRootCertificate("DomainBridge Lab Root");
        using var publisher = CreatePublisherCertificate(root, "DomainBridge Lab Publisher");

        var result = PublisherCertificateChainPolicy.Validate(
            publisher,
            new X509Certificate2Collection(root));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_rejects_a_publisher_chained_to_an_unknown_root()
    {
        using var trustedRoot = CreateRootCertificate("DomainBridge Trusted Lab Root");
        using var unknownRoot = CreateRootCertificate("DomainBridge Unknown Lab Root");
        using var publisher = CreatePublisherCertificate(unknownRoot, "DomainBridge Unknown Publisher");

        var result = PublisherCertificateChainPolicy.Validate(
            publisher,
            new X509Certificate2Collection(trustedRoot));

        Assert.Equal(PublisherCertificateChainError.ChainNotTrusted, result.Error);
    }

    [Fact]
    public void Validate_rejects_an_empty_custom_root_set()
    {
        using var root = CreateRootCertificate("DomainBridge Lab Root");
        using var publisher = CreatePublisherCertificate(root, "DomainBridge Lab Publisher");

        var result = PublisherCertificateChainPolicy.Validate(
            publisher,
            new X509Certificate2Collection());

        Assert.Equal(PublisherCertificateChainError.TrustRootRequired, result.Error);
    }

    private static X509Certificate2 CreateRootCertificate(string commonName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 1, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreatePublisherCertificate(X509Certificate2 issuer, string commonName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.3") },
            critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var serialNumber = RandomNumberGenerator.GetBytes(16);
        return request.Create(
            issuer,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(7),
            serialNumber);
    }
}
