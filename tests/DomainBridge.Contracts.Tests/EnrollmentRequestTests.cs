using DomainBridge.Contracts;
using Xunit;

namespace DomainBridge.Contracts.Tests;

public sealed class EnrollmentRequestTests
{
    [Fact]
    public void Create_requires_an_agent_identifier()
    {
        var result = EnrollmentRequest.Create("", "thumbprint", "agent/1.0");

        Assert.True(result.IsFailure);
        Assert.Equal(EnrollmentError.MissingAgentId, result.Error);
    }

    [Fact]
    public void Create_rejects_a_missing_certificate_thumbprint()
    {
        var result = EnrollmentRequest.Create("device-001", " ", "agent/1.0");

        Assert.True(result.IsFailure);
        Assert.Equal(EnrollmentError.MissingCertificateThumbprint, result.Error);
    }

    [Fact]
    public void Create_rejects_a_missing_agent_version()
    {
        var result = EnrollmentRequest.Create("device-001", "A1B2C3", null);

        Assert.True(result.IsFailure);
        Assert.Equal(EnrollmentError.MissingAgentVersion, result.Error);
    }

    [Fact]
    public void Create_accepts_identity_metadata_without_any_secret()
    {
        var result = EnrollmentRequest.Create("device-001", "A1B2C3", "agent/1.0");

        Assert.True(result.IsSuccess);
        Assert.Equal("device-001", result.Value!.AgentId);
        Assert.Equal("A1B2C3", result.Value.CertificateThumbprint);
        Assert.Equal("agent/1.0", result.Value.AgentVersion);
    }
}
