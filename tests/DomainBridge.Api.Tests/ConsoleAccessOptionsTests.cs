using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class ConsoleAccessOptionsTests
{
    [Fact]
    public void IsReady_requires_an_enabled_https_authority_client_and_audience()
    {
        var options = new ConsoleAccessOptions
        {
            EnableReadApi = true,
            Authority = "https://login.microsoftonline.com/tenant-id/v2.0",
            ClientId = "00000000-0000-0000-0000-000000000000",
            Audience = "api://00000000-0000-0000-0000-000000000000"
        };

        Assert.True(options.IsReady());
    }

    [Fact]
    public void IsReady_fails_closed_when_any_entra_setting_is_missing_or_insecure()
    {
        var options = new ConsoleAccessOptions
        {
            EnableReadApi = true,
            Authority = "http://insecure.example",
            ClientId = "client",
            Audience = "api://client"
        };

        Assert.False(options.IsReady());
    }
}
