using System.Net;
using System.Text;
using DomainBridge.Agent;
using Xunit;

namespace DomainBridge.Agent.Tests;

public sealed class MaintenanceJobClientTests
{
    [Fact]
    public async Task GetApprovedJobsAsync_reads_only_policy_jobs_for_the_device()
    {
        var handler = new JsonHandler("""
            [{"campaignId":"30fce42e-37d2-414c-97e4-35f36922f91c","remediationId":"network-diagnostics","requiresApproval":false,"mayRequireRestart":false}]
            """);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://console.example/") };
        var jobClient = new MaintenanceJobClient(client);

        var jobs = await jobClient.GetApprovedJobsAsync("lab-device-002");

        var job = Assert.Single(jobs);
        Assert.Equal("network-diagnostics", job.RemediationId);
        Assert.Equal("/api/v1/agents/lab-device-002/maintenance-jobs", handler.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("password", handler.RequestUri!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetApprovedJobsAsync_rejects_an_empty_device_identifier_without_network_access()
    {
        var handler = new JsonHandler("[]");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://console.example/") };
        var jobClient = new MaintenanceJobClient(client);

        await Assert.ThrowsAsync<ArgumentException>(() => jobClient.GetApprovedJobsAsync(" "));
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class JsonHandler(string responseBody) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
