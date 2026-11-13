using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DomainBridge.Api.Tests;

/// <summary>
/// Guards the product boundary: generic maintenance and remediation features
/// were removed from DomainBridge and must not reappear on the API surface.
/// </summary>
public sealed class OutOfScopeRouteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OutOfScopeRouteTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("GET", "/api/v1/console/remediations")]
    [InlineData("GET", "/api/v1/maintenance/campaigns")]
    [InlineData("POST", "/api/v1/maintenance/campaigns")]
    [InlineData("GET", "/api/v1/agents/device-001/maintenance-jobs")]
    public async Task Out_of_scope_routes_are_not_exposed(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
