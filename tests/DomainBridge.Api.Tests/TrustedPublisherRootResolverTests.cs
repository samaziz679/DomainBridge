using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class TrustedPublisherRootResolverTests
{
    [Fact]
    public void Resolve_returns_only_the_configured_root_certificate()
    {
        using var configuredRoot = CreateRoot("Configured Root");
        using var otherRoot = CreateRoot("Other Root");
        var availableRoots = new X509Certificate2Collection();
        availableRoots.Add(configuredRoot);
        availableRoots.Add(otherRoot);
        var source = new TestRootSource(availableRoots);

        var result = TrustedPublisherRootResolver.Resolve(
            new TrustedPublisherPkiOptions { TrustedRootThumbprints = [configuredRoot.Thumbprint!] },
            source);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Roots);
        Assert.Equal(configuredRoot.Thumbprint, result.Roots[0].Thumbprint);
    }

    [Fact]
    public void Resolve_fails_closed_when_a_configured_root_is_not_available()
    {
        using var root = CreateRoot("Configured Root");
        var source = new TestRootSource(new X509Certificate2Collection(root));

        var result = TrustedPublisherRootResolver.Resolve(
            new TrustedPublisherPkiOptions { TrustedRootThumbprints = ["0000000000000000000000000000000000000000"] },
            source);

        Assert.Equal(TrustedPublisherRootError.ConfiguredRootNotFound, result.Error);
    }

    private static X509Certificate2 CreateRoot(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 1, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7));
    }

    private sealed class TestRootSource(X509Certificate2Collection roots) : IPublisherRootCertificateSource
    {
        public X509Certificate2Collection ReadRoots() => roots;
    }
}
