using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class AgentIdentityResolverTests : IDisposable
{
    private readonly X509Certificate2 _authority = TestCertificates.CreateAuthority("CN=DomainBridge Test Agent CA");
    private readonly X509Certificate2 _otherAuthority = TestCertificates.CreateAuthority("CN=Some Other CA");

    public void Dispose()
    {
        _authority.Dispose();
        _otherAuthority.Dispose();
    }

    private AgentIdentityOptions Pinned => new() { TrustedIssuerThumbprints = [_authority.Thumbprint] };

    private IAgentIssuerChainReader Reader => new X509AgentIssuerChainReader([_authority, _otherAuthority]);

    [Fact]
    public void Device_identity_is_read_from_the_certificate_subject()
    {
        using var certificate = TestCertificates.IssueAgentCertificate(_authority, "SPARE01");

        var result = AgentIdentityResolver.Resolve(certificate, required: true, Pinned, Reader);

        Assert.True(result.IsSuccess);
        Assert.Equal("SPARE01", result.DeviceId);
        Assert.Equal(certificate.Thumbprint, result.Thumbprint);
    }

    [Fact]
    public void Missing_certificate_is_rejected_when_identity_is_required()
    {
        var result = AgentIdentityResolver.Resolve(null, required: true, Pinned, Reader);

        Assert.Equal(AgentIdentityError.CertificateRequired, result.Error);
    }

    [Fact]
    public void Missing_certificate_is_anonymous_when_identity_is_not_required()
    {
        var result = AgentIdentityResolver.Resolve(null, required: false, new AgentIdentityOptions(), Reader);

        Assert.True(result.IsSuccess);
        Assert.Null(result.DeviceId);
    }

    [Fact]
    public void Certificate_from_an_unpinned_authority_is_rejected()
    {
        using var certificate = TestCertificates.IssueAgentCertificate(_otherAuthority, "SPARE01");

        var result = AgentIdentityResolver.Resolve(certificate, required: true, Pinned, Reader);

        Assert.Equal(AgentIdentityError.UntrustedIssuer, result.Error);
    }

    [Fact]
    public void Self_signed_certificate_is_rejected_even_when_its_own_thumbprint_is_pinned()
    {
        using var certificate = TestCertificates.CreateSelfSignedAgentCertificate("SPARE01");
        var options = new AgentIdentityOptions { TrustedIssuerThumbprints = [certificate.Thumbprint] };

        var result = AgentIdentityResolver.Resolve(certificate, required: true, options, Reader);

        Assert.Equal(AgentIdentityError.UntrustedIssuer, result.Error);
    }

    [Fact]
    public void Required_identity_fails_closed_when_no_authority_is_pinned()
    {
        using var certificate = TestCertificates.IssueAgentCertificate(_authority, "SPARE01");

        var result = AgentIdentityResolver.Resolve(certificate, required: true, new AgentIdentityOptions(), Reader);

        Assert.Equal(AgentIdentityError.UntrustedIssuer, result.Error);
    }

    [Fact]
    public void Expired_certificate_is_rejected()
    {
        using var certificate = TestCertificates.IssueAgentCertificate(_authority, "SPARE01");

        var result = AgentIdentityResolver.Resolve(
            certificate, required: true, Pinned, Reader, DateTimeOffset.UtcNow.AddYears(5));

        Assert.Equal(AgentIdentityError.CertificateInvalid, result.Error);
    }

    [Fact]
    public void Certificate_without_client_authentication_usage_is_rejected()
    {
        using var certificate = TestCertificates.IssueAgentCertificate(_authority, "SPARE01", clientAuthentication: false);

        var result = AgentIdentityResolver.Resolve(certificate, required: true, Pinned, Reader);

        Assert.Equal(AgentIdentityError.CertificateInvalid, result.Error);
    }

    [Fact]
    public void Pinned_thumbprints_ignore_case_and_spaces()
    {
        using var certificate = TestCertificates.IssueAgentCertificate(_authority, "SPARE01");
        var spaced = string.Join(' ', _authority.Thumbprint.ToLowerInvariant().Chunk(2).Select(pair => new string(pair)));
        var options = new AgentIdentityOptions { TrustedIssuerThumbprints = [spaced] };

        var result = AgentIdentityResolver.Resolve(certificate, required: true, options, Reader);

        Assert.True(result.IsSuccess);
    }
}
