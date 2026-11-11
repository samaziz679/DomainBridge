namespace DomainBridge.Contracts;

/// <summary>
/// Technical device metadata accepted by the central inventory endpoint.
/// The contract intentionally excludes user, profile, credential and secret data.
/// </summary>
public sealed record DeviceInventoryReport(
    string DeviceId,
    string OperatingSystem,
    string Architecture,
    string AgentVersion,
    DateTimeOffset CapturedAtUtc);
