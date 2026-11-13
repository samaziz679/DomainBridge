<#
.SYNOPSIS
    Tranche 1.2 spike, step 1 of 2: ask the target domain for an offline join token
    for the spare test machine, reusing its existing computer account.
.DESCRIPTION
    THROWAWAY CODE. Run it from a machine that can reach a domain controller of the
    target domain, under the test account whose only right is to reset computer
    accounts in the TEST organisational unit.

    The token file is a secret: it lets its holder act as that computer in the
    domain. Carry it to the spare machine by a trusted path, never by e-mail or a
    shared folder, and let step 2 delete it.

    NOT YET EXECUTED.
.EXAMPLE
    ./New-SpikeJoinToken.ps1 -TargetDomain b.example -ComputerName SPARE01 `
        -TestOuDistinguishedName 'OU=DomainBridgeTest,DC=b,DC=example' -OutputPath E:\spare01.token
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][string]$TargetDomain,
    [Parameter(Mandatory)][string]$ComputerName,
    [Parameter(Mandatory)][string]$TestOuDistinguishedName,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($TestOuDistinguishedName -notmatch '(?i)test') {
    throw "Refusing to run: '$TestOuDistinguishedName' does not look like a dedicated test OU."
}
if (Test-Path -LiteralPath $OutputPath) {
    throw "Refusing to overwrite '$OutputPath'."
}

$action = "Reset the computer account '$ComputerName' in '$TestOuDistinguishedName' and issue a join token"
if (-not $PSCmdlet.ShouldProcess($TargetDomain, $action)) {
    return
}

$output = djoin.exe /provision /domain $TargetDomain /machine $ComputerName `
    /machineou $TestOuDistinguishedName /reuse /savefile $OutputPath 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "djoin /provision failed ($LASTEXITCODE): $($output | Out-String)"
}

Write-Host "Join token written to $OutputPath."
Write-Host 'This file is a secret. Carry it to the spare machine by a trusted path; step 2 deletes it.'
