using System.Net;
using System.Net.Http.Json;
using DomainBridge.Contracts;

namespace DomainBridge.Agent;

/// <summary>
/// Sends a single read-only inventory report to the configured API.
/// Retry policy and client-certificate transport configuration are supplied by the host.
/// </summary>
public sealed class InventorySubmissionClient(HttpClient httpClient)
{
    public async Task<InventorySubmissionResult> SubmitAsync(
        DeviceInventoryReport report,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "/api/v1/inventory",
            report,
            cancellationToken);

        return new InventorySubmissionResult(
            response.StatusCode == HttpStatusCode.Accepted,
            response.StatusCode);
    }
}

public sealed record InventorySubmissionResult(
    bool Accepted,
    HttpStatusCode StatusCode);
