[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$publishRoot = Join-Path $repositoryRoot 'artifacts\test-lab-package'
$outputRoot = Join-Path $repositoryRoot 'artifacts\test-lab-installer'
$agentProject = Join-Path $repositoryRoot 'src\DomainBridge.Agent\DomainBridge.Agent.csproj'
$apiProject = Join-Path $repositoryRoot 'src\DomainBridge.Api\DomainBridge.Api.csproj'
$consoleRoot = Join-Path $repositoryRoot 'apps\DomainBridge.Console'
$sourceFile = Join-Path $PSScriptRoot 'Product.wxs'
$msiPath = Join-Path $outputRoot 'DomainBridge-TestLab-0.2.1.msi'

Remove-Item -LiteralPath $publishRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishRoot, $outputRoot -Force | Out-Null

& npm.cmd run build --prefix $consoleRoot
if ($LASTEXITCODE -ne 0) { throw "Console build failed with exit code $LASTEXITCODE." }

dotnet publish $agentProject --configuration $Configuration --runtime win-x64 --self-contained true --output (Join-Path $publishRoot 'Agent')
if ($LASTEXITCODE -ne 0) {
    throw "Agent publish failed with exit code $LASTEXITCODE."
}

dotnet publish $apiProject --configuration $Configuration --runtime win-x64 --self-contained true --output (Join-Path $publishRoot 'Console')
if ($LASTEXITCODE -ne 0) { throw "API publish failed with exit code $LASTEXITCODE." }
Copy-Item -LiteralPath (Join-Path $consoleRoot 'dist\*') -Destination (Join-Path $publishRoot 'Console\wwwroot') -Recurse -Force

dotnet tool run wix build -arch x64 -d PackagePublishDir=$publishRoot -d InstallerSourceDir=$PSScriptRoot -o $msiPath $sourceFile
if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed with exit code $LASTEXITCODE."
}
