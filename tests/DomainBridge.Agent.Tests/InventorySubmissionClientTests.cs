using System.Net;
using System.Text;
using DomainBridge.Contracts;
using Xunit;

namespace DomainBridge.Agent.Tests;

public sealed class InventorySubmissionClientTests
{
    [Fact]
    public async Task SubmitAsync_posts_only_the_inventory_contract_to_the_api()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://console.example/")
        };
        var submissionClient = new InventorySubmissionClient(client);
        var report = new DeviceInventoryReport(
            "device-001",
            "Windows 11",
            "X64",
            "agent/1.0",
            DateTimeOffset.Parse("2026-11-11T00:00:00+00:00"));

        var result = await submissionClient.SubmitAsync(report);

        Assert.True(result.Accepted);
        Assert.Equal("/api/v1/inventory", handler.RequestUri!.AbsolutePath);
        Assert.Contains("device-001", handler.RequestBody);
        Assert.DoesNotContain("password", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAsync_reports_rejection_without_retrying_or_exposing_data()
    {
        var handler = new CapturingHandler(HttpStatusCode.BadRequest);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://console.example/")
        };
        var submissionClient = new InventorySubmissionClient(client);
        var report = new DeviceInventoryReport(
            "device-001",
            "Windows 11",
            "X64",
            "agent/1.0",
            DateTimeOffset.Parse("2026-11-11T00:00:00+00:00"));

        var result = await submissionClient.SubmitAsync(report);

        Assert.False(result.Accepted);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    private sealed class CapturingHandler(HttpStatusCode responseStatus) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(responseStatus);
        }
    }
}
