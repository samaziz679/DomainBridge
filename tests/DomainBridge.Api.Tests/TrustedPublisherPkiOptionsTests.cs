using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class TrustedPublisherPkiOptionsTests
{
    [Fact]
    public void IsReady_requires_at_least_one_valid_root_thumbprint()
    {
        var valid = new TrustedPublisherPkiOptions
        {
            TrustedRootThumbprints = ["0123456789ABCDEF0123456789ABCDEF01234567"]
        };
        var malformed = new TrustedPublisherPkiOptions
        {
            TrustedRootThumbprints = ["not-a-certificate"]
        };

        Assert.True(valid.IsReady());
        Assert.False(malformed.IsReady());
    }
}
