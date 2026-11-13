using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace DomainBridge.Desktop;

public partial class MainWindow : Window
{
    private static bool apiLaunchAttempted;
    private static readonly HttpClient Client = new() { BaseAddress = new Uri("http://127.0.0.1:5080") };
    private readonly ObservableCollection<ModuleRow> rows = [];
    private string currentSection = "Parc";

    private static readonly Dictionary<string, (string Hint, string Empty)> Modules = new()
    {
        ["Parc"] = ("Inventaire des postes et état de conformité", "Aucun poste remonté pour le moment."),
        ["Collections"] = ("Groupes dynamiques construits depuis le parc", "Aucune collection ne peut être calculée sans inventaire."),
        ["Déploiements"] = ("Campagnes de maintenance approuvables et traçables", "Aucune campagne en attente. Le catalogue est disponible ci-dessous."),
        ["Bibliothèque"] = ("Actions technicien approuvées et documentées", "Aucune action de maintenance n’est publiée."),
        ["Historique"] = ("Preuves d’audit centralisées", "Aucune opération enregistrée."),
        ["Paramètres"] = ("Configuration on-premise", "API locale active. Configurez PostgreSQL, PKI, AD local et Entra ID avant la production.")
    };

    public MainWindow()
    {
        InitializeComponent();
        OperationsList.ItemsSource = rows;
        Loaded += async (_, _) => await LoadCurrentSectionAsync();
    }

    private async void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Navigation.SelectedItem is not ListBoxItem item || MigrationWorkspace is null) return;
        currentSection = item.Content?.ToString() ?? "Parc";
        SectionTitle.Text = currentSection;
        var migration = currentSection == "Migration";
        MigrationWorkspace.Visibility = migration ? Visibility.Visible : Visibility.Collapsed;
        OperationsList.Visibility = migration ? Visibility.Collapsed : Visibility.Visible;
        if (migration)
        {
            SectionHint.Text = "Préparez et contrôlez une migration de domaine A vers domaine B";
            PrimaryMessage.Text = "Préparez une migration contrôlée de domaine A vers domaine B.";
            SecondaryMessage.Text = "Aucune bascule n’est lancée depuis cet écran : le pilote et la validation restent obligatoires.";
            return;
        }
        await LoadCurrentSectionAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadCurrentSectionAsync();

    private async Task LoadCurrentSectionAsync()
    {
        if (MigrationWorkspace is null || currentSection == "Migration") return;
        var module = Modules[currentSection];
        SectionHint.Text = module.Hint;
        rows.Clear();
        try
        {
            await EnsureApiAsync();
            switch (currentSection)
            {
                case "Parc": await LoadDevicesAsync(); break;
                case "Collections": await LoadCollectionsAsync(); break;
                case "Déploiements": await LoadDeploymentsAsync(); break;
                case "Bibliothèque": await LoadRemediationsAsync("Bibliothèque technicien"); break;
                case "Historique": await LoadAuditAsync(); break;
                default: PrimaryMessage.Text = module.Empty; SecondaryMessage.Text = "Aucune donnée sensible n’est affichée dans la console."; break;
            }
        }
        catch
        {
            PrimaryMessage.Text = "API locale indisponible.";
            SecondaryMessage.Text = "Démarrez le package complet ou vérifiez la configuration réseau et PKI.";
        }
    }

    private async Task EnsureApiAsync()
    {
        try { using var response = await Client.GetAsync("/health"); response.EnsureSuccessStatusCode(); }
        catch when (StartPackagedApi()) { await Task.Delay(TimeSpan.FromSeconds(2)); }
    }

    private async Task LoadDevicesAsync()
    {
        using var document = await GetJsonAsync("/api/v1/console/agents?limit=100");
        foreach (var device in document.RootElement.EnumerateArray())
            rows.Add(new ModuleRow(device.GetProperty("deviceId").GetString() ?? "Poste", "Géré", $"{device.GetProperty("operatingSystem").GetString()} · {device.GetProperty("architecture").GetString()} · agent {device.GetProperty("agentVersion").GetString()}", FormatTime(device.GetProperty("capturedAtUtc").GetDateTimeOffset())));
        PrimaryMessage.Text = rows.Count == 0 ? "Aucun poste remonté pour le moment." : $"{rows.Count} poste(s) géré(s)";
        SecondaryMessage.Text = "Inventaire technique remonté par les agents DomainBridge.";
    }

    private async Task LoadCollectionsAsync()
    {
        using var document = await GetJsonAsync("/api/v1/console/agents?limit=100");
        foreach (var group in document.RootElement.EnumerateArray().GroupBy(device => device.GetProperty("operatingSystem").GetString() ?? "Système inconnu").OrderBy(group => group.Key))
            rows.Add(new ModuleRow(group.Key, $"{group.Count()} poste(s)", "Collection dynamique basée sur le système d’exploitation", "Inventaire local"));
        PrimaryMessage.Text = rows.Count == 0 ? "Aucune collection ne peut être calculée sans inventaire." : $"{rows.Count} collection(s) dynamique(s)";
        SecondaryMessage.Text = "Les collections s’actualisent depuis les données d’inventaire, sans exécuter d’action.";
    }

    private async Task LoadDeploymentsAsync()
    {
        using var campaigns = await GetJsonAsync("/api/v1/maintenance/campaigns");
        foreach (var campaign in campaigns.RootElement.EnumerateArray())
            rows.Add(new ModuleRow(campaign.GetProperty("remediationId").GetString() ?? "Campagne", campaign.GetProperty("status").GetString() ?? "draft", $"{campaign.GetProperty("targetCount").GetInt32()} poste(s) ciblé(s)", FormatTime(campaign.GetProperty("createdAtUtc").GetDateTimeOffset())));
        if (rows.Count == 0) await LoadRemediationsAsync("Catalogue de déploiement");
        else { PrimaryMessage.Text = $"{rows.Count} campagne(s) de déploiement"; SecondaryMessage.Text = "Les campagnes restent en brouillon jusqu’à leur approbation explicite."; }
    }

    private async Task LoadRemediationsAsync(string title)
    {
        using var document = await GetJsonAsync("/api/v1/console/remediations");
        foreach (var remediation in document.RootElement.EnumerateArray())
            rows.Add(new ModuleRow(remediation.GetProperty("id").GetString() ?? "Action", "Approuvée", remediation.GetProperty("displayName").GetString() ?? "Action contrôlée", remediation.GetProperty("mayRequireRestart").GetBoolean() ? "Redémarrage possible" : "Sans redémarrage"));
        PrimaryMessage.Text = $"{rows.Count} action(s) dans {title.ToLowerInvariant()}";
        SecondaryMessage.Text = "Le catalogue expose les métadonnées, jamais les scripts ni les secrets.";
    }

    private async Task LoadAuditAsync()
    {
        using var document = await GetJsonAsync("/api/v1/console/audit?limit=100");
        foreach (var audit in document.RootElement.EnumerateArray())
            rows.Add(new ModuleRow(audit.GetProperty("action").GetString() ?? "Audit", "Enregistré", audit.GetProperty("subject").GetString() ?? "Système", FormatTime(audit.GetProperty("occurredAtUtc").GetDateTimeOffset())));
        PrimaryMessage.Text = rows.Count == 0 ? "Aucune opération enregistrée." : $"{rows.Count} preuve(s) d’audit";
        SecondaryMessage.Text = "Les événements restent consultables pour le suivi de conformité.";
    }

    private static async Task<JsonDocument> GetJsonAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string FormatTime(DateTimeOffset timestamp) => timestamp.ToLocalTime().ToString("dd/MM HH:mm");

    private static bool StartPackagedApi()
    {
        var apiPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Console", "DomainBridge.Api.exe"));
        if (apiLaunchAttempted || !File.Exists(apiPath)) return false;
        apiLaunchAttempted = true;
        var startInfo = new ProcessStartInfo(apiPath, "--urls http://127.0.0.1:5080") { UseShellExecute = false, CreateNoWindow = true };
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        Process.Start(startInfo);
        return true;
    }

    private void CreatePilotPlan_Click(object sender, RoutedEventArgs e) => MigrationStatus.Text = "Plan pilote créé : découverte → prérequis → approbation → vague pilote → validation → rollback si nécessaire.";
    private void CheckPrerequisites_Click(object sender, RoutedEventArgs e) => MigrationStatus.Text = "Prérequis : inventaire agent, DNS/DC, PKI, profils, espace disque, BitLocker et audit PostgreSQL.";
}

public sealed record ModuleRow(string Name, string Status, string Detail, string Activity);
