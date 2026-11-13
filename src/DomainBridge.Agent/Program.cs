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
        builder.Configuration.AddInMemoryCollection(ReadMachineSettings());
        builder.Configuration.AddEnvironmentVariables("DOMAINBRIDGE_");
        builder.Services.AddWindowsService(service => service.ServiceName = AgentOptions.ServiceName);

        var options = builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();
        var problems = options.Validate();
        if (problems.Count > 0)
        {
            await Console.Error.WriteLineAsync(string.Join(Environment.NewLine, problems));
            return 3;
        }

        var certificate = FindMachineCertificate();
        if (certificate is null)
        {
            await Console.Error.WriteLineAsync(
                "No valid client-authentication certificate named after this machine was found in the local machine store.");
            return 4;
        }

        var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Manual };
        handler.ClientCertificates.Add(certificate);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.ServerUrl!),
            Timeout = TimeSpan.FromSeconds(30)
        };

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IInventoryCycle>(new InventoryCycle(new InventorySubmissionClient(httpClient), Version));
        builder.Services.AddHostedService<AgentWorker>();

        await builder.Build().RunAsync();
        return 0;
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
