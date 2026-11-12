using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace DomainBridge.Desktop;

public partial class MainWindow : Window
{
    private static bool apiLaunchAttempted;
    private static readonly HttpClient Client = new() { BaseAddress = new Uri("http://127.0.0.1:5080") };
    private static readonly Dictionary<string, (string Primary, string Detail)> Modules = new()
    {
        ["Parc"] = ("Connexion à l’API locale…", "L’inventaire des agents apparaît automatiquement quand le service est démarré."),
        ["Collections"] = ("Créez des collections dynamiques.", "Ciblez par domaine, OU, site, version Windows ou conformité."),
        ["Déploiements"] = ("Préparez un déploiement approuvé.", "Packages, WinGet et maintenance par vagues avec suivi par poste."),
        ["Bibliothèque"] = ("Bibliothèque technicien.", "Diagnostics, GPO, Defender et Windows Update comme actions signées."),
        ["Historique"] = ("Aucune opération enregistrée.", "Chaque action est conservée dans PostgreSQL."),
        ["Paramètres"] = ("Configurez l’environnement on-premise.", "API, PostgreSQL, PKI, AD local et Entra ID après installation.")
    };

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshOverviewAsync();
    }

    private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Navigation.SelectedItem is not ListBoxItem item) return;
        var section = item.Content?.ToString() ?? "Parc";
        SectionTitle.Text = section;
        var migration = section == "Migration";
        MigrationWorkspace.Visibility = migration ? Visibility.Visible : Visibility.Collapsed;
        if (migration)
        {
            PrimaryMessage.Text = "Préparez une migration contrôlée de domaine A vers domaine B.";
            SecondaryMessage.Text = "Le plan pilote collecte les prérequis et génère les étapes d’exécution et de rollback.";
        }
        else
        {
            var module = Modules[section];
            PrimaryMessage.Text = module.Primary;
            SecondaryMessage.Text = module.Detail;
        }
    }

    private async Task RefreshOverviewAsync()
    {
        try
        {
            using var response = await Client.GetAsync("/api/v1/console/overview");
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            PrimaryMessage.Text = $"{root.GetProperty("enrolledAgents").GetInt32()} agent(s) enrôlé(s)";
            SecondaryMessage.Text = $"API {root.GetProperty("apiStatus").GetString()} · {root.GetProperty("auditEvents").GetInt32()} preuve(s) d’audit.";
        }
        catch
        {
            if (StartPackagedApi())
            {
                PrimaryMessage.Text = "Démarrage de l’API locale…";
                SecondaryMessage.Text = "Actualisation automatique du parc dans quelques secondes.";
                await Task.Delay(TimeSpan.FromSeconds(2));
                await RefreshOverviewAsync();
                return;
            }

            PrimaryMessage.Text = "API locale indisponible.";
            SecondaryMessage.Text = "Installez le package complet pour connecter le parc.";
        }
    }

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
