using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;

namespace DomainBridge.Agent;

/// <summary>
/// Composition root of the agent. Everything it wires is tested on its own; this
/// class only touches the operating system (registry, certificate store, service
/// control manager), which is why it is excluded from unit coverage.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Operating-system wiring; exercised by the installer scenario E12.")]
public static class Program
{
    private const string RegistryKeyPath = @"SOFTWARE\DomainBridge\Agent";

    public static string Version =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public static async Task<int> Main(string[] args)
    {
        if (AgentCommand.TryRun(args, Console.Out, Version))
        {
            return 0;
        }

        if (args.Length > 0)
        {
            await Console.Error.WriteLineAsync("Usage: DomainBridge.Agent.exe [--inventory-once]. Without argument it runs as the Windows service.");
            return 2;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(service => service.ServiceName = AgentOptions.ServiceName);

        // The loop interval is read once; the server address and the certificate are
        // looked up again on every round until both are present.
        var startup = ReadOptions();
        var interval = startup.InventoryIntervalMinutes is >= 1 and <= 1440 ? startup.InventoryIntervalMinutes : 60;
        builder.Services.AddSingleton(new AgentOptions { ServerUrl = startup.ServerUrl, InventoryIntervalMinutes = interval });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IInventoryCycle>(new ReadinessAwareCycle(TryCreateCycle));
        builder.Services.AddHostedService<AgentWorker>();

        await builder.Build().RunAsync();
        return 0;
    }

    private static AgentOptions ReadOptions() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(ReadMachineSettings())
            .AddEnvironmentVariables("DOMAINBRIDGE_")
            .Build()
            .GetSection(AgentOptions.SectionName)
            .Get<AgentOptions>() ?? new AgentOptions();

    private static InventoryCycle? TryCreateCycle()
    {
        var options = ReadOptions();
        if (options.Validate().Count > 0)
        {
            return null;
        }

        var certificate = FindMachineCertificate();
        if (certificate is null)
        {
            return null;
        }

        var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Manual };
        handler.ClientCertificates.Add(certificate);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.ServerUrl!),
            Timeout = TimeSpan.FromSeconds(30)
        };

        return new InventoryCycle(new InventorySubmissionClient(httpClient), Version);
    }

    /// <summary>Settings written by the installer under HKLM, mapped to the Agent configuration section.</summary>
    private static Dictionary<string, string?> ReadMachineSettings()
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows())
        {
            return settings;
        }

        using var key = Registry.LocalMachine.OpenSubKey(RegistryKeyPath);
        if (key is null)
        {
            return settings;
        }

        foreach (var name in key.GetValueNames())
        {
            settings[$"{AgentOptions.SectionName}:{name}"] = Convert.ToString(key.GetValue(name), CultureInfo.InvariantCulture);
        }

        return settings;
    }

    private static X509Certificate2? FindMachineCertificate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        return AgentCertificateSelector.Select(store.Certificates, Environment.MachineName, DateTimeOffset.UtcNow);
    }
}
