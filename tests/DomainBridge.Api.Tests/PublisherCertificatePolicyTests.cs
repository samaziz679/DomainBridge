using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class PublisherCertificatePolicyTests
{
    [Fact]
    public void Validate_accepts_a_trusted_current_code_signing_certificate()
    {
        using var certificate = CreateCertificate(includeCodeSigning: true);

        var result = PublisherCertificatePolicy.Validate(
            certificate,
            new[] { certificate.Thumbprint! });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_rejects_an_untrusted_publisher_certificate()
    {
        using var certificate = CreateCertificate(includeCodeSigning: true);

        var result = PublisherCertificatePolicy.Validate(
            certificate,
            new[] { "0000000000000000000000000000000000000000" });

        Assert.Equal(PublisherCertificateError.UntrustedPublisher, result.Error);
    }

    [Fact]
    public void Validate_rejects_a_certificate_without_code_signing_usage()
    {
        using var certificate = CreateCertificate(includeCodeSigning: false);

        var result = PublisherCertificatePolicy.Validate(
            certificate,
            new[] { certificate.Thumbprint! });

        Assert.Equal(PublisherCertificateError.CodeSigningUsageRequired, result.Error);
    }

    [Fact]
    public void Validate_rejects_a_certificate_outside_its_validity_period()
    {
        using var certificate = CreateCertificate(
            includeCodeSigning: true,
            notBefore: DateTimeOffset.UtcNow.AddDays(-2),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));

        var result = PublisherCertificatePolicy.Validate(
            certificate,
            new[] { certificate.Thumbprint! });

        Assert.Equal(PublisherCertificateError.CertificateOutsideValidityPeriod, result.Error);
    }

    private static X509Certificate2 CreateCertificate(
        bool includeCodeSigning,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=DomainBridge Test Publisher",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        if (includeCodeSigning)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.3") },
                critical: false));
        }

        return request.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddDays(1));
    }
}
