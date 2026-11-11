using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class AgentTransportSecurityTests
{
    [Fact]
    public void Validate_allows_missing_certificate_only_when_transport_security_is_not_required()
    {
        var result = AgentTransportSecurity.Validate(null, requireClientCertificate: false);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_rejects_missing_certificate_when_transport_security_is_required()
    {
        var result = AgentTransportSecurity.Validate(null, requireClientCertificate: true);

        Assert.Equal(AgentCertificateError.CertificateRequired, result.Error);
    }

    [Fact]
    public void Validate_accepts_current_client_authentication_certificate_when_required()
    {
        using var certificate = CreateClientAuthenticationCertificate();

        var result = AgentTransportSecurity.Validate(certificate, requireClientCertificate: true);

        Assert.True(result.IsSuccess);
    }

    private static X509Certificate2 CreateClientAuthenticationCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=DomainBridge Test Agent",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.2") },
            critical: false));

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }
}
