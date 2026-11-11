[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$PackageRoot,

    [string]$InstallRoot = "$env:ProgramFiles\DomainBridge\Agent",

    [switch]$LabMode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$agentExecutable = Join-Path $PackageRoot 'DomainBridge.Agent.exe'
if (-not (Test-Path -LiteralPath $agentExecutable -PathType Leaf)) {
    throw "DomainBridge.Agent.exe is missing from '$PackageRoot'."
}

$signature = Get-AuthenticodeSignature -FilePath $agentExecutable
if (-not $LabMode -and $signature.Status -ne 'Valid') {
    throw "The agent package must be Authenticode-signed for production deployment. Signature status: $($signature.Status)."
}

if ($PSCmdlet.ShouldProcess($InstallRoot, 'Install signed DomainBridge agent package')) {
    New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $PackageRoot '*') -Destination $InstallRoot -Recurse -Force

    $state = @{
        installedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        packageRoot = $PackageRoot
        signatureStatus = $signature.Status.ToString()
        labMode = [bool]$LabMode
    } | ConvertTo-Json
    Set-Content -LiteralPath (Join-Path $InstallRoot 'installation-state.json') -Value $state -Encoding utf8
}
