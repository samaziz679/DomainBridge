using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class EntraAuthenticationOptionsTests
{
    [Fact]
    public void IsReady_requires_a_tenant_id_and_api_audience()
    {
        Assert.True(new EntraAuthenticationOptions
        {
            TenantId = "11111111-1111-1111-1111-111111111111",
            ApiAudience = "api://domainbridge"
        }.IsReady());
        Assert.False(new EntraAuthenticationOptions { TenantId = "invalid" }.IsReady());
    }
}
