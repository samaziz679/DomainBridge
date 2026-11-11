using System.Runtime.InteropServices;

namespace DomainBridge.Agent;

/// <summary>
/// Read-only technical metadata captured locally by the DomainBridge agent.
/// This type intentionally excludes identities, profiles, credentials and secrets.
/// </summary>
public sealed record DeviceInventorySnapshot(
    string DeviceId,
    string OperatingSystem,
    string Architecture,
    string AgentVersion,
    DateTimeOffset CapturedAtUtc)
{
    public static DeviceInventorySnapshot Capture(string agentVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentVersion);

        return new DeviceInventorySnapshot(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            agentVersion.Trim(),
            DateTimeOffset.UtcNow);
    }
}
