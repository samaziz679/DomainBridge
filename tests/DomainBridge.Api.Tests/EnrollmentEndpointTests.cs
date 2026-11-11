using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class EnrollmentEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EnrollmentEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Enrollment_accepts_non_secret_metadata()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/enrollments", new
        {
            agentId = "device-001",
            certificateThumbprint = "A1B2C3",
            agentVersion = "agent/1.0"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("device-001", payload);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enrollment_rejects_missing_certificate_metadata()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/enrollments", new
        {
            agentId = "device-001",
            certificateThumbprint = "",
            agentVersion = "agent/1.0"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrollment_rejects_secret_like_fields()
    {
        using var content = JsonContent.Create(new
        {
            agentId = "device-001",
            certificateThumbprint = "A1B2C3",
            agentVersion = "agent/1.0",
            password = "must-not-be-accepted"
        });

        var response = await _client.PostAsync("/api/v1/enrollments", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
