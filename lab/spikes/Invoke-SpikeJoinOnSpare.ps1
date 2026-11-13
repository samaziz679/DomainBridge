<#
.SYNOPSIS
    Tranche 1.2 spike, step 2 of 2: on the SPARE test machine, apply the offline join
    token, then (after restart) record whether the machine is a healthy member of
    the target domain.
.DESCRIPTION
    THROWAWAY CODE. Run elevated, on the spare machine only. It refuses to run on
    any machine whose name is not the one given in -SpareComputerName.

    Step Apply : records the state before, optionally leaves the source domain
                 locally, applies the token, deletes the token file. Restart the
                 machine yourself afterwards.
    Step Verify: after the restart, records the state after.

    No directory credential is used on this machine. The token is never copied
    into the result file. Keep a local administrator account at hand to sign in
    if neither domain answers.

    NOT YET EXECUTED.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][ValidateSet('Apply', 'Verify')][string]$Step,
    [Parameter(Mandatory)][string]$SpareComputerName,
    [Parameter(Mandatory)][string]$TargetDomain,
    [string]$TokenPath,
    [ValidateSet('Direct', 'UnjoinFirst')][string]$Variant = 'Direct'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:COMPUTERNAME -ne $SpareComputerName) {
    throw "Refusing to run: this machine is '$env:COMPUTERNAME', not the spare machine '$SpareComputerName'."
}
$isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isElevated) {
    throw 'Run this script from an elevated PowerShell session.'
}

$resultRoot = Join-Path $env:ProgramData 'DomainBridgeSpike'
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null

function Get-MachineState {
    $system = Get-CimInstance Win32_ComputerSystem
    $secureChannel = $false
    try { $secureChannel = [bool](Test-ComputerSecureChannel) } catch { $secureChannel = $false }
    [pscustomobject]@{
        CapturedAt          = (Get-Date).ToString('o')
        Domain              = $system.Domain
        PartOfDomain        = $system.PartOfDomain
        SecureChannel       = $secureChannel
        OperatingSystem     = (Get-CimInstance Win32_OperatingSystem).Caption
        LocalAdministrators = @(Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction SilentlyContinue | ForEach-Object Name)
        ProfileSids         = @(Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList' | ForEach-Object PSChildName)
    }
}

if ($Step -eq 'Apply') {
    if (-not $TokenPath -or -not (Test-Path -LiteralPath $TokenPath)) {
        throw 'Step Apply needs -TokenPath pointing at the join token file.'
    }
    if (-not $PSCmdlet.ShouldProcess($env:COMPUTERNAME, "Move this machine to domain '$TargetDomain' ($Variant)")) {
        return
    }

    $record = [ordered]@{ Variant = $Variant; Before = Get-MachineState }
    try {
        if ($Variant -eq 'UnjoinFirst') {
            # Local leave: the computer account in the source domain is left untouched.
            $unjoin = Get-CimInstance Win32_ComputerSystem |
                Invoke-CimMethod -MethodName UnjoinDomainOrWorkgroup -Arguments @{ FUnjoinOptions = [uint32]0 }
            $record.UnjoinReturnValue = $unjoin.ReturnValue
        }
        $output = djoin.exe /requestodj /loadfile $TokenPath /windowspath $env:SystemRoot /localos 2>&1
        $record.RequestExitCode = $LASTEXITCODE
        $record.RequestOutput = ($output | Out-String).Trim()
    }
    finally {
        Remove-Item -LiteralPath $TokenPath -Force -ErrorAction SilentlyContinue
        $record.TokenFileDeleted = -not (Test-Path -LiteralPath $TokenPath)
        [pscustomobject]$record | ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath (Join-Path $resultRoot 'apply.json') -Encoding utf8
    }

    Write-Host "djoin exit code: $($record.RequestExitCode). Token file deleted: $($record.TokenFileDeleted)."
    Write-Host 'Restart the machine, sign in with the LOCAL administrator, then run this script with -Step Verify.'
    return
}

$after = Get-MachineState
$after | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resultRoot 'verify.json') -Encoding utf8
$succeeded = ($after.Domain -eq $TargetDomain) -and $after.SecureChannel
Write-Host "Domain: $($after.Domain) ; secure channel: $($after.SecureChannel) ; member of target domain and healthy: $succeeded"
Write-Host "Results: $resultRoot (apply.json, verify.json) - copy both back for analysis."
if (-not $succeeded) { exit 1 }
