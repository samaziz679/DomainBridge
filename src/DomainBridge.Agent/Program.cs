using System.Diagnostics.CodeAnalysis;

namespace DomainBridge.Agent;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static int Main(string[] args)
    {
        if (AgentCommand.TryRun(args, Console.Out, "0.1.0"))
        {
            return 0;
        }

        Console.Error.WriteLine("DomainBridge.Agent only supports '--inventory-once' in this MVP.");
        return 2;
    }
}
