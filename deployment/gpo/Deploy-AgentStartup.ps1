<#
.SYNOPSIS
    Computer startup script for Group Policy: installs or upgrades the DomainBridge agent
    from a protected share, for machines that Intune does not manage.
.DESCRIPTION
    Runs as the local system account at startup. It installs the same MSI that Intune
    delivers, and does nothing when that version is already installed.

    The share must be readable by computer accounts and writable by administrators only.
    The script contains no credential and no secret.

    An unsigned or tampered MSI is refused unless -AllowUnsigned is given; that switch is
    for the spare test machine only.
.EXAMPLE
    Deploy-AgentStartup.ps1 -MsiPath \\fileserver\DomainBridge$\DomainBridge-Agent-0.3.0.msi -ServerUrl https://admin-pc.example:8443/
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$MsiPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$ServerUrl,

    [switch]$AllowUnsigned
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$logRoot = Join-Path $env:ProgramData 'DomainBridge\Agent'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$logPath = Join-Path $logRoot 'install.log'

if ($MsiPath -notmatch 'DomainBridge-Agent-(\d+\.\d+\.\d+)') {
    throw "Cannot read the agent version from the file name '$MsiPath'."
}
$packageVersion = [version]$Matches[1]

$installed = Get-ItemProperty -Path 'HKLM:\SOFTWARE\DomainBridge\Agent' -Name Version -ErrorAction SilentlyContinue
if ($installed -and ([version]$installed.Version -ge $packageVersion)) {
    exit 0
}

if (-not (Test-Path -LiteralPath $MsiPath -PathType Leaf)) {
    throw "Agent MSI not reachable: '$MsiPath'."
}

$signature = Get-AuthenticodeSignature -FilePath $MsiPath
if ($signature.Status -ne 'Valid' -and -not $AllowUnsigned) {
    throw "The agent MSI is not validly signed ($($signature.Status)); installation refused."
}

$arguments = @('/i', "`"$MsiPath`"", "SERVERURL=`"$ServerUrl`"", '/qn', '/norestart', '/l*v', "`"$logPath`"")
$process = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\msiexec.exe') -ArgumentList $arguments -Wait -PassThru

# 3010 means installed, restart required; the agent itself never forces one.
if ($process.ExitCode -notin 0, 3010) {
    throw "msiexec failed with exit code $($process.ExitCode). See $logPath."
}

exit 0
