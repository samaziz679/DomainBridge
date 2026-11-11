using System.Text.Json;

namespace DomainBridge.Agent;

public static class AgentCommand
{
    public static bool TryRun(string[] args, TextWriter output, string agentVersion)
    {
        if (!args.SequenceEqual(["--inventory-once"]))
        {
            return false;
        }

        var snapshot = DeviceInventorySnapshot.Capture(agentVersion);
        output.WriteLine(JsonSerializer.Serialize(snapshot));
        return true;
    }
}
