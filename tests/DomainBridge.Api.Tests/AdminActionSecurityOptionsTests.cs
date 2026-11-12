using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class AdminActionSecurityOptionsTests
{
    [Fact]
    public void IsReady_requires_explicit_enablement_role_and_trusted_publisher_thumbprint()
    {
        var options = new AdminActionSecurityOptions
        {
            EnableElevatedExecution = true,
            RequiredOperatorRole = "DomainBridge.Admin",
            MinimumApprovals = 2,
            TrustedPublisherThumbprints = ["0123456789ABCDEF0123456789ABCDEF01234567"]
        };

        Assert.True(options.IsReady());
    }

    [Fact]
    public void IsReady_fails_closed_for_missing_or_malformed_trust_configuration()
    {
        var missingRole = new AdminActionSecurityOptions
        {
            EnableElevatedExecution = true,
            MinimumApprovals = 1,
            TrustedPublisherThumbprints = ["0123456789ABCDEF0123456789ABCDEF01234567"]
        };
        var malformedThumbprint = new AdminActionSecurityOptions
        {
            EnableElevatedExecution = true,
            RequiredOperatorRole = "DomainBridge.Admin",
            MinimumApprovals = 1,
            TrustedPublisherThumbprints = ["not-a-certificate"]
        };

        Assert.False(missingRole.IsReady());
        Assert.False(malformedThumbprint.IsReady());
    }
}
