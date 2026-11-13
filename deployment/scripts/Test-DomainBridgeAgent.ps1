<#
.SYNOPSIS
    Reports whether the DomainBridge agent is installed at the expected version and running.
.DESCRIPTION
    Read-only. Usable as an Intune custom detection script (exit code 0 and one line of
    output mean "detected") and by a technician on a machine.
.PARAMETER MinimumVersion
    Lowest acceptable agent version.
#>
[CmdletBinding()]
param(
    [version]$MinimumVersion = '0.3.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$settings = Get-ItemProperty -Path 'HKLM:\SOFTWARE\DomainBridge\Agent' -ErrorAction SilentlyContinue
$service = Get-Service -Name 'DomainBridgeAgent' -ErrorAction SilentlyContinue

if (-not $settings -or -not $settings.PSObject.Properties['Version'] -or -not $service) {
    exit 1
}

if ([version]$settings.Version -lt $MinimumVersion) {
    exit 1
}

if ($service.Status -ne 'Running') {
    exit 1
}

Write-Output "DomainBridge agent $($settings.Version) is installed and running."
exit 0
