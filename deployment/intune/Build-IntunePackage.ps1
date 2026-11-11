[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$IntuneWinAppUtilPath,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$stagingRoot = Join-Path $repositoryRoot 'artifacts\\intune-staging'
$publishRoot = Join-Path $stagingRoot 'agent'
$installerRoot = Join-Path $stagingRoot 'installer'

Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishRoot, $installerRoot, $OutputDirectory -Force | Out-Null

$agentProject = Join-Path $repositoryRoot 'src\\DomainBridge.Agent\\DomainBridge.Agent.csproj'
dotnet publish $agentProject --configuration $Configuration --runtime win-x64 --self-contained true --output $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "Agent publish failed with exit code $LASTEXITCODE."
}

Copy-Item -Path (Join-Path $repositoryRoot 'deployment\\scripts\\Install-DomainBridgeAgent.ps1') -Destination $installerRoot
Copy-Item -Path (Join-Path $publishRoot '*') -Destination $installerRoot -Recurse

& $IntuneWinAppUtilPath -c $installerRoot -s 'Install-DomainBridgeAgent.ps1' -o $OutputDirectory -q
if ($LASTEXITCODE -ne 0) {
    throw "IntuneWinAppUtil failed with exit code $LASTEXITCODE."
}
