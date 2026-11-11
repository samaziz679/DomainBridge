[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$publishRoot = Join-Path $repositoryRoot 'artifacts\test-lab-agent'
$outputRoot = Join-Path $repositoryRoot 'artifacts\test-lab-installer'
$agentProject = Join-Path $repositoryRoot 'src\DomainBridge.Agent\DomainBridge.Agent.csproj'
$sourceFile = Join-Path $PSScriptRoot 'Product.wxs'
$msiPath = Join-Path $outputRoot 'DomainBridge-TestLab-0.1.0.msi'

Remove-Item -LiteralPath $publishRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishRoot, $outputRoot -Force | Out-Null

dotnet publish $agentProject --configuration $Configuration --runtime win-x64 --self-contained true --output $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "Agent publish failed with exit code $LASTEXITCODE."
}

dotnet tool run wix build -arch x64 -d AgentPublishDir=$publishRoot -d InstallerSourceDir=$PSScriptRoot -o $msiPath $sourceFile
if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed with exit code $LASTEXITCODE."
}
