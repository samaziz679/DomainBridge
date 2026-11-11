[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$InstallMissing,

    [switch]$SkipNode,

    [switch]$SkipWiX
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-CommandAvailable {
    param([Parameter(Mandatory)][string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Install-WithWinget {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [Parameter(Mandatory)][string]$DisplayName
    )

    if (-not (Test-CommandAvailable 'winget')) {
        throw "Cannot install ${DisplayName}: winget is not available."
    }

    if ($PSCmdlet.ShouldProcess($DisplayName, "Install missing prerequisite $PackageId")) {
        winget install --id $PackageId --exact --silent --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -ne 0) {
            throw "winget failed while installing $DisplayName."
        }
    }
}

if (-not (Test-CommandAvailable 'dotnet')) {
    if (-not $InstallMissing) {
        throw 'Missing prerequisite: .NET SDK 8. Re-run with -InstallMissing to install it.'
    }
    Install-WithWinget -PackageId 'Microsoft.DotNet.SDK.8' -DisplayName '.NET SDK 8'
}

$dotnetVersion = & dotnet --version
Write-Output ".NET SDK detected: $dotnetVersion"

if (-not $SkipNode) {
    if (-not (Test-CommandAvailable 'node')) {
        if (-not $InstallMissing) {
            throw 'Missing prerequisite: Node.js. Re-run with -InstallMissing to install it.'
        }
        Install-WithWinget -PackageId 'OpenJS.NodeJS.LTS' -DisplayName 'Node.js LTS'
    }

    Write-Output "Node.js detected: $(& node --version)"
}

if (-not $SkipWiX) {
    $repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $manifest = Join-Path $repositoryRoot '.config\dotnet-tools.json'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
        throw 'Local WiX tool manifest is missing from the repository.'
    }

    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not restore the local WiX tool.'
    }

    Write-Output 'WiX local tool restored.'
}

Write-Output 'Bootstrap complete. PKI, PostgreSQL, Intune and Active Directory are not auto-installed or auto-configured.'
