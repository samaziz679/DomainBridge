using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DomainBridge.Api.Tests;

/// <summary>
/// Synthetic, in-memory certificates for tests. Nothing here is written to a
/// certificate store or to disk.
/// </summary>
internal static class TestCertificates
{
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    public static X509Certificate2 CreateAuthority(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
    }

    public static X509Certificate2 IssueAgentCertificate(
        X509Certificate2 authority,
        string deviceName,
        bool clientAuthentication = true)
    {
        using var key = RSA.Create(2048);
        var request = CreateAgentRequest(deviceName, key, clientAuthentication);
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority, true, false));
        var serial = RandomNumberGenerator.GetBytes(12);

        return request.Create(authority, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30), serial);
    }

    public static X509Certificate2 CreateSelfSignedAgentCertificate(string deviceName)
    {
        using var key = RSA.Create(2048);
        var request = CreateAgentRequest(deviceName, key, clientAuthentication: true);

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static CertificateRequest CreateAgentRequest(string deviceName, RSA key, bool clientAuthentication)
    {
        var request = new CertificateRequest($"CN={deviceName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (clientAuthentication)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new(ClientAuthenticationOid) }, critical: false));
        }

        return request;
    }
}
