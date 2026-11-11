using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class AgentCertificatePolicyTests
{
    [Fact]
    public void Validate_accepts_a_current_client_authentication_certificate_with_matching_thumbprint()
    {
        using var certificate = CreateCertificate(includeClientAuthentication: true);

        var result = AgentCertificatePolicy.Validate(certificate, certificate.Thumbprint!);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_rejects_a_certificate_without_client_authentication_usage()
    {
        using var certificate = CreateCertificate(includeClientAuthentication: false);

        var result = AgentCertificatePolicy.Validate(certificate, certificate.Thumbprint!);

        Assert.Equal(AgentCertificateError.ClientAuthenticationUsageRequired, result.Error);
    }

    [Fact]
    public void Validate_rejects_a_claimed_thumbprint_that_does_not_match_the_certificate()
    {
        using var certificate = CreateCertificate(includeClientAuthentication: true);

        var result = AgentCertificatePolicy.Validate(certificate, "0000000000000000000000000000000000000000");

        Assert.Equal(AgentCertificateError.ThumbprintMismatch, result.Error);
    }

    private static X509Certificate2 CreateCertificate(bool includeClientAuthentication)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=DomainBridge Test Agent",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        if (includeClientAuthentication)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.2") },
                critical: false));
        }

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }
}
