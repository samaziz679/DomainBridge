<#
.SYNOPSIS
    Checks whether this machine can host the DomainBridge two-forest laboratory.
.DESCRIPTION
    Read-only. Changes nothing on the host. Exit code 0 when every blocking
    requirement is met, 1 otherwise.
.PARAMETER Topology
    Minimal: two domain controllers and one Windows 11 client.
    Full: adds a Windows 10 client and a DomainBridge server.
#>
[CmdletBinding()]
param(
    [ValidateSet('Minimal', 'Full')]
    [string]$Topology = 'Minimal',

    [string]$LabSources = 'C:\LabSources'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$requirements = @{
    Minimal = @{ FreeMemoryGb = 7; FreeDiskGb = 80; IsoCount = 2 }
    Full    = @{ FreeMemoryGb = 14; FreeDiskGb = 140; IsoCount = 3 }
}[$Topology]

$computer = Get-CimInstance Win32_ComputerSystem
$os = Get-CimInstance Win32_OperatingSystem
$systemDrive = Get-PSDrive -Name $env:SystemDrive.TrimEnd(':')
$isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
$isoRoot = Join-Path $LabSources 'ISOs'
$isoFiles = @(Get-ChildItem -LiteralPath $isoRoot -Filter '*.iso' -ErrorAction SilentlyContinue)

$freeMemoryGb = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
$totalMemoryGb = [math]::Round($computer.TotalPhysicalMemory / 1GB, 1)
$freeDiskGb = [math]::Round($systemDrive.Free / 1GB)

$checks = @(
    [pscustomobject]@{
        Check    = 'Elevated PowerShell session'
        Passed   = $isElevated
        Detail   = if ($isElevated) { 'yes' } else { 'run as administrator' }
    }
    [pscustomobject]@{
        Check    = 'Hyper-V management service'
        Passed   = [bool](Get-Service -Name vmms -ErrorAction SilentlyContinue)
        Detail   = 'Windows feature Microsoft-Hyper-V-All (needs a restart once enabled)'
    }
    [pscustomobject]@{
        Check    = 'Hyper-V PowerShell module'
        Passed   = [bool](Get-Module -ListAvailable -Name Hyper-V)
        Detail   = 'installed with the Hyper-V feature'
    }
    [pscustomobject]@{
        Check    = 'AutomatedLab module'
        Passed   = [bool](Get-Module -ListAvailable -Name AutomatedLab)
        Detail   = 'Install-Module AutomatedLab ; New-LabSourcesFolder'
    }
    [pscustomobject]@{
        Check    = "Free memory >= $($requirements.FreeMemoryGb) GB"
        Passed   = $freeMemoryGb -ge $requirements.FreeMemoryGb
        Detail   = "$freeMemoryGb GB free of $totalMemoryGb GB"
    }
    [pscustomobject]@{
        Check    = "Free disk >= $($requirements.FreeDiskGb) GB on $env:SystemDrive"
        Passed   = $freeDiskGb -ge $requirements.FreeDiskGb
        Detail   = "$freeDiskGb GB free"
    }
    [pscustomobject]@{
        Check    = "Windows ISO files >= $($requirements.IsoCount) in $isoRoot"
        Passed   = $isoFiles.Count -ge $requirements.IsoCount
        Detail   = "$($isoFiles.Count) found (Windows Server, Windows 11$(if ($Topology -eq 'Full') { ', Windows 10' }))"
    }
)

$checks | Format-Table -AutoSize -Wrap | Out-String | Write-Host

$failed = @($checks | Where-Object { -not $_.Passed })
if ($failed.Count -gt 0) {
    Write-Host "HOST NOT READY for topology '$Topology': $($failed.Count) requirement(s) missing."
    exit 1
}

Write-Host "HOST READY for topology '$Topology'."
exit 0
