using System.Net;
using System.Text;
using DomainBridge.Agent;
using DomainBridge.Contracts;
using Xunit;

namespace DomainBridge.Agent.Tests;

public sealed class MaintenanceResultClientTests
{
    [Fact]
    public async Task ReportAsync_posts_a_structured_outcome_without_free_form_output()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://console.example/") };
        var resultClient = new MaintenanceResultClient(client);
        var campaignId = Guid.Parse("3e7ba6f1-72ac-4e33-aa79-37b88b47910f");

        var accepted = await resultClient.ReportAsync(campaignId, "lab-device-005", MaintenanceJobOutcome.Succeeded);

        Assert.True(accepted);
        Assert.Equal($"/api/v1/maintenance/campaigns/{campaignId}/results", handler.RequestUri!.AbsolutePath);
        Assert.Contains("succeeded", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("output", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportAsync_rejects_invalid_input_before_network_access()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://console.example/") };
        var resultClient = new MaintenanceResultClient(client);

        await Assert.ThrowsAsync<ArgumentException>(() => resultClient.ReportAsync(Guid.Empty, "lab-device-005", MaintenanceJobOutcome.Succeeded));
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class CapturingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent("", Encoding.UTF8, "application/json") };
        }
    }
}
