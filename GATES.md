# DomainBridge on-premise desktop delivery gates

## G1: Windows operator application

Outcome: An installed Windows application opens a native DomainBridge operations workspace without Node.js or a development URL.

CHECK: dotnet build src/DomainBridge.Desktop/DomainBridge.Desktop.csproj -c Release
EXPECT: Build succeeded.

## G2: Core operations modules

Outcome: The desktop workspace exposes Parc, Collections, Déploiements, Bibliothèque, Historique, Migration and Paramètres, with migration locked by default.

CHECK: dotnet test DomainBridge.sln --no-restore
EXPECT: Réussi!

## G3: Unified installation

Outcome: The MSI packages the desktop client, local API, agent and all runtime dependencies with start-menu, desktop and uninstall entries.

CHECK: powershell.exe -ExecutionPolicy Bypass -File installer/TestLab/Build-TestLabMsi.ps1 -Configuration Release
EXPECT: DomainBridge-Agent

## G4: Safety boundary

Outcome: No default desktop flow performs an AD join, profile migration or arbitrary script execution.

CHECK: dotnet test DomainBridge.sln --no-restore
EXPECT: Réussi!
