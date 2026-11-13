<#
.SYNOPSIS
    Wraps the DomainBridge agent MSI into an Intune Win32 package (.intunewin).
.DESCRIPTION
    The MSI is the single agent package; Intune and GPO both deliver that same file.
    This script only wraps it. Build the MSI first with installer/Agent/Build-AgentMsi.ps1.

    In Intune, create the Win32 app with:
      Install   : msiexec /i "DomainBridge-Agent-<version>.msi" SERVERURL="https://<admin-workstation>:<port>/" /qn /norestart
      Uninstall : msiexec /x "{product code shown by Intune}" /qn /norestart
      Context   : System
      Detection : MSI product code and version (filled in by Intune from the package)

    An unsigned MSI is refused unless -AllowUnsigned is given; that switch is for the
    spare test machine only.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$IntuneWinAppUtilPath,

    [Parameter(Mandatory)]
    [string]$MsiPath,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [switch]$AllowUnsigned
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $MsiPath -PathType Leaf)) {
    throw "Agent MSI not found: '$MsiPath'. Build it with installer/Agent/Build-AgentMsi.ps1."
}

$signature = Get-AuthenticodeSignature -FilePath $MsiPath
if ($signature.Status -ne 'Valid' -and -not $AllowUnsigned) {
    throw "The agent MSI is not validly signed ($($signature.Status)). Sign it, or pass -AllowUnsigned for the spare test machine."
}

$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("domainbridge-intune-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingRoot, $OutputDirectory -Force | Out-Null
try {
    Copy-Item -LiteralPath $MsiPath -Destination $stagingRoot
    $setupFile = Split-Path -Leaf $MsiPath

    & $IntuneWinAppUtilPath -c $stagingRoot -s $setupFile -o $OutputDirectory -q
    if ($LASTEXITCODE -ne 0) {
        throw "IntuneWinAppUtil failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output "Intune package written to $OutputDirectory (signature: $($signature.Status))."
