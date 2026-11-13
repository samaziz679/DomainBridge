<#
.SYNOPSIS
    Tranche 1.2 feasibility spike: move a client from forest A to forest B with an
    offline domain join token, reusing the computer account that already exists in B.
.DESCRIPTION
    THROWAWAY CODE. It answers one question and is not a product component.

    For each iteration: restore the 'Baseline' checkpoint, have the B domain
    controller provision a join token for the existing computer account, apply it
    on the client, restart, then record whether the client is a healthy member of B.

    The join token is a secret. It is passed in memory between laboratory sessions,
    written on the client only for the duration of the djoin call, deleted
    immediately afterwards, and never written to the result file or the console.

    NOT YET EXECUTED: written before a laboratory host was available.
.PARAMETER Variant
    Direct: apply the token while the client is still a member of A.
    UnjoinFirst: leave A locally (to a workgroup, no directory credential), then apply the token.
#>
[CmdletBinding()]
param(
    [ValidateSet('Direct', 'UnjoinFirst')]
    [string]$Variant = 'Direct',

    [string]$ClientName = 'CL11',

    [ValidateRange(1, 20)]
    [int]$Iterations = 5,

    [string]$LabName = 'DomainBridgeLab'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module AutomatedLab
Import-Lab -Name $LabName -NoValidation

$targetDomain = 'lab-b.test'
$migrationOu = 'OU=Migration,DC=lab-b,DC=test'
$labCredential = Import-Clixml -LiteralPath (Join-Path $env:LOCALAPPDATA "DomainBridgeLab\$LabName.credential.clixml")
$localAdministrator = [pscredential]::new("$ClientName\Administrator", $labCredential.Password)

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$resultRoot = Join-Path $repositoryRoot 'artifacts\lab'
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
$results = @()

foreach ($iteration in 1..$Iterations) {
    Write-Host "== Iteration $iteration / $Iterations ($Variant)"
    Restore-LabVMSnapshot -All -SnapshotName 'Baseline'
    Start-LabVM -All -Wait

    $before = Invoke-LabCommand -ComputerName $ClientName -PassThru -ActivityName 'State before' -ScriptBlock {
        (Get-CimInstance Win32_ComputerSystem).Domain
    }

    # 1. Domain B issues a token for the EXISTING account (/reuse resets its secret).
    $token = Invoke-LabCommand -ComputerName 'DCB' -PassThru -ActivityName 'Provision join token' `
        -ArgumentList $targetDomain, $ClientName, $migrationOu -ScriptBlock {
        param($domain, $machine, $ou)
        $path = Join-Path $env:TEMP ([guid]::NewGuid().ToString('N'))
        try {
            $output = djoin.exe /provision /domain $domain /machine $machine /machineou $ou /reuse /savefile $path 2>&1
            if ($LASTEXITCODE -ne 0) { throw "djoin /provision failed ($LASTEXITCODE): $output" }
            Get-Content -LiteralPath $path -Raw -Encoding Unicode
        }
        finally {
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }

    # 2. The client applies the token. No directory credential is present on the client.
    $apply = Invoke-LabCommand -ComputerName $ClientName -PassThru -ActivityName 'Apply join token' `
        -ArgumentList $token, $Variant -ScriptBlock {
        param($joinToken, $variant)
        $steps = [ordered]@{}
        if ($variant -eq 'UnjoinFirst') {
            # Local leave: the A computer account is left untouched in the directory.
            $unjoin = (Get-CimInstance Win32_ComputerSystem) |
                Invoke-CimMethod -MethodName UnjoinDomainOrWorkgroup -Arguments @{ FUnjoinOptions = [uint32]0 }
            $steps.UnjoinReturnValue = $unjoin.ReturnValue
        }
        $path = Join-Path $env:TEMP ([guid]::NewGuid().ToString('N'))
        try {
            Set-Content -LiteralPath $path -Value $joinToken -Encoding Unicode -NoNewline
            $output = djoin.exe /requestodj /loadfile $path /windowspath $env:SystemRoot /localos 2>&1
            $steps.RequestExitCode = $LASTEXITCODE
            $steps.RequestOutput = ($output | Out-String).Trim()
        }
        finally {
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
        [pscustomobject]$steps
    }
    $token = $null

    # 3. Restart and observe, using the LOCAL administrator so the check does not
    #    depend on either domain.
    Restart-LabVM -ComputerName $ClientName -Wait
    $after = Invoke-LabCommand -ComputerName $ClientName -Credential $localAdministrator -PassThru `
        -ActivityName 'State after' -ScriptBlock {
        $system = Get-CimInstance Win32_ComputerSystem
        $secureChannel = $false
        try { $secureChannel = Test-ComputerSecureChannel } catch { $secureChannel = $false }
        [pscustomobject]@{
            Domain          = $system.Domain
            PartOfDomain    = $system.PartOfDomain
            SecureChannel   = $secureChannel
            LogonServer     = (nltest.exe /dsgetdc:lab-b.test 2>&1 | Select-String 'DC:' | Out-String).Trim()
            LocalAdminGroup = @(Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction SilentlyContinue | ForEach-Object Name)
        }
    }

    $results += [pscustomobject]@{
        Iteration       = $iteration
        Variant         = $Variant
        DomainBefore    = $before
        UnjoinReturn    = if ($apply.PSObject.Properties['UnjoinReturnValue']) { $apply.UnjoinReturnValue } else { $null }
        RequestExitCode = $apply.RequestExitCode
        RequestOutput   = $apply.RequestOutput
        DomainAfter     = $after.Domain
        SecureChannel   = $after.SecureChannel
        LocalAdminGroup = $after.LocalAdminGroup
        Succeeded       = ($after.Domain -eq $targetDomain) -and $after.SecureChannel
    }
}

$resultPath = Join-Path $resultRoot ("spike-odj-{0}-{1:yyyyMMdd-HHmmss}.json" -f $Variant.ToLowerInvariant(), (Get-Date))
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding utf8

$succeeded = @($results | Where-Object Succeeded).Count
Write-Host "Variant '$Variant': $succeeded / $Iterations iterations ended as a healthy member of $targetDomain."
Write-Host "Results: $resultPath"
if ($succeeded -ne $Iterations) { exit 1 }
exit 0
