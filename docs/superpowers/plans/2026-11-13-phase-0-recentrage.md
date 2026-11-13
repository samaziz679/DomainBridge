# Phase 0 — recentrage et socle de vérification : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retirer du dépôt tout ce que le périmètre v2 déclare hors périmètre, puis installer une vérification unique rejouée par la CI.

**Architecture:** Suppression pure (aucun comportement nouveau) : les tests restants doivent rester verts et un test de garde empêche le retour des routes retirées. La vérification est un script PowerShell unique appelé à l'identique en local et en CI.

**Tech Stack:** .NET (C#, xUnit), React/TypeScript (Vitest), PowerShell, GitHub Actions, WiX.

**Spec:** `docs/PRODUCT_BRIEF.md` (section « Hors périmètre »), `docs/DEVELOPMENT_PLAN.md` (tranches 0.2 et 0.3), `docs/adr/ADR-003-decisions-de-cadrage.md` (D4).

## Global Constraints

- Aucun secret, mot de passe, clé ou certificat de production dans le dépôt (`AGENTS.md`).
- Sont hors périmètre et retirés : catalogue de remédiations, actions technicien, campagnes de maintenance, PKI d'éditeur de manifestes, `DomainBridge.Desktop`.
- Sont conservés pour la phase 2 : `OperatorAuthorizationPolicy`, `EntraAuthenticationOptions`, `ConsoleAccessOptions`, `AgentCertificatePolicy`, `AgentTransportSecurity`, stores d'audit et d'inventaire.
- Fichiers de verrouillage NuGet régénérés et validés ; restauration en `--locked-mode` en CI.
- Le passage à .NET 10 exige l'installation du SDK sur la machine : hors de ce plan tant que l'installation n'est pas autorisée.

## Review Focus

- Une route retirée répond encore (catalogue, campagnes, tâches d'agent) → doit répondre 404 : test de garde, tâche 1.
- L'installeur de laboratoire référence encore le client de bureau → la construction du MSI échoue : tâche 2.
- La console web appelle une route retirée et affiche « indisponible » → tests de la console, tâche 1.
- `verify.ps1` renvoie 0 alors qu'une étape a échoué → chaque étape contrôle son code de sortie : tâche 3.
- La documentation décrit encore des fonctions retirées → recherche de mots-clés, tâche 4.

---

### Task 1: Retrait du code hors périmètre (API, agent, contrats, console web)

**Files:**
- Delete: `src/DomainBridge.Agent/{MaintenanceActionRunner,MaintenanceJobClient,MaintenanceJobProcessor,MaintenanceResultClient,RemediationCatalog}.cs`
- Delete: `src/DomainBridge.Api/{AdminActionSecurityOptions,MaintenanceCampaignStore,PublisherCertificateChainPolicy,PublisherCertificatePolicy,SignedManifestPolicy,TrustedPublisherRootResolver}.cs`
- Delete: `src/DomainBridge.Contracts/{AdminActionPolicy,MaintenanceCampaign,RemediationActionDefinition}.cs`
- Delete: les fichiers de tests correspondants sous `tests/`
- Modify: `src/DomainBridge.Api/Program.cs`, `src/DomainBridge.Api/DomainBridge.Api.csproj`, `tests/DomainBridge.Api.Tests/DomainBridge.Api.Tests.csproj`, `src/DomainBridge.Api/appsettings.Production.example.json`
- Modify: `apps/DomainBridge.Console/src/{api.ts,App.tsx,LiveConsole.tsx,App.test.tsx,api.test.ts}`
- Test: `tests/DomainBridge.Api.Tests/OutOfScopeRouteTests.cs`

- [ ] **Step 1: Écrire le test de garde**

```csharp
[Theory]
[InlineData("GET", "/api/v1/console/remediations")]
[InlineData("GET", "/api/v1/maintenance/campaigns")]
[InlineData("POST", "/api/v1/maintenance/campaigns")]
[InlineData("GET", "/api/v1/agents/device-001/maintenance-jobs")]
public async Task Out_of_scope_routes_are_not_exposed(string method, string path)
// attend HttpStatusCode.NotFound
```

- [ ] **Step 2: Vérifier qu'il échoue** — `dotnet test tests/DomainBridge.Api.Tests --filter OutOfScopeRouteTests` → 4 échecs.
- [ ] **Step 3: Supprimer fichiers et routes** ; retirer la dépendance `System.Security.Cryptography.Pkcs` ; retirer la section `AdminActions` de l'exemple de configuration ; retirer le test du catalogue dans `ConsoleReadEndpointTests`.
- [ ] **Step 4: Console web** : retirer `RemediationRecord`, le quatrième appel réseau, le panneau et le lien « Maintenance » ; le lien principal devient « Review pilot readiness » (`#pilot`). Adapter les tests.
- [ ] **Step 5: Vérifier** — `dotnet test DomainBridge.sln -c Release` et `npm test && npm run build` → tout vert.
- [ ] **Step 6: Commit** — `refactor: remove out-of-scope maintenance and publisher features`

### Task 2: Retrait du client de bureau

**Files:**
- Delete: `src/DomainBridge.Desktop/`
- Modify: `DomainBridge.sln`, `installer/TestLab/Build-TestLabMsi.ps1`, `installer/TestLab/Product.wxs`

- [ ] **Step 1:** `dotnet sln remove src/DomainBridge.Desktop/DomainBridge.Desktop.csproj`, puis suppression du dossier.
- [ ] **Step 2:** Installeur : retirer la publication du client de bureau ; les raccourcis pointent vers `Run-DomainBridgeConsole.cmd` (console web locale de laboratoire) ; version `0.3.0`.
- [ ] **Step 3: Vérifier** — `dotnet build DomainBridge.sln -c Release` vert ; `installer/TestLab/Build-TestLabMsi.ps1` produit `DomainBridge-TestLab-0.3.0.msi`.
- [ ] **Step 4: Commit** — `refactor: retire desktop client in favour of the single web console`

### Task 3: Vérification unique et CI

**Files:**
- Create: `scripts/verify.ps1`
- Modify: `.github/workflows/verify.yml`, `Directory.Build.props`

**Interfaces:**
- Produces: `scripts/verify.ps1 [-SkipConsole]` ; code de sortie 0 si toutes les étapes passent, sinon 1 avec le nom de l'étape en échec.

- [ ] **Step 1:** `Directory.Build.props` : `TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel=latest`.
- [ ] **Step 2:** `verify.ps1` enchaîne : restauration verrouillée, `dotnet format --verify-no-changes`, construction, tests avec couverture, puis console (`npm ci`, `npm test`, `npm run build`, `npm audit --audit-level=high`). Chaque étape contrôle `$LASTEXITCODE`.
- [ ] **Step 3:** CI : un seul job Windows appelant `scripts/verify.ps1`, plus un job gitleaks.
- [ ] **Step 4: Vérifier** — `pwsh scripts/verify.ps1` → sortie 0 ; introduire une erreur de format volontaire → sortie 1 ; la retirer.
- [ ] **Step 5: Commit** — `build: add single verification script and harden CI`

### Task 4: Documentation remise à plat

**Files:**
- Delete: `docs/security/ELEVATED_ACTIONS.md` et les dix fichiers `docs/testing/*.tdd.md` des fonctions retirées
- Modify: `README.md`, `GATES.md`, `docs/ARCHITECTURE.md`, `installer/TestLab/README.txt`, `docs/deployment/TEST_LAB_INSTALLER.md`

- [ ] **Step 1:** README aligné sur le périmètre v2 ; `GATES.md` : jalons de la phase 0 avec commande et preuve datée.
- [ ] **Step 2: Vérifier** — recherche de `remédiation|maintenance|Desktop|runbook` hors `docs/audit` et ADR → aucune occurrence décrivant une fonction existante.
- [ ] **Step 3: Commit** — `docs: align README, gates and architecture with scope v2`

### Task 5 (en attente d'autorisation) : passage à .NET 10 LTS

- [ ] Installer le SDK .NET 10 ; `global.json`, `TargetFramework` → `net10.0`, paquets alignés, fichiers de verrouillage régénérés ; `scripts/verify.ps1` vert.
