<#
.SYNOPSIS
    Builds the DomainBridge two-forest laboratory on Hyper-V with AutomatedLab.
.DESCRIPTION
    Creates an isolated network, forest A (source) and forest B (target), Windows
    clients joined to A, synthetic identities in both forests and pre-created
    computer accounts in B, then takes a 'Baseline' checkpoint of every machine.

    Everything is synthetic. No production name, credential or certificate is used.
    The laboratory password is generated at build time and stored outside the
    repository, protected for the current Windows user (DPAPI).

    NOT YET EXECUTED: this script was written before a laboratory host was
    available. Treat its first run as part of tranche 1.1.
.PARAMETER Topology
    Minimal: DCA, DCB, CL11. Full: adds CL10 and SRV (DomainBridge server).
#>
[CmdletBinding()]
param(
    [ValidateSet('Minimal', 'Full')]
    [string]$Topology = 'Minimal',

    [string]$LabName = 'DomainBridgeLab',

    [string]$ServerOperatingSystem = 'Windows Server 2025 Datacenter Evaluation',

    [string]$Windows11OperatingSystem = 'Windows 11 Enterprise Evaluation',

    [string]$Windows10OperatingSystem = 'Windows 10 Enterprise Evaluation'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Keep a build log that can be read afterwards from a non-elevated session.
$logRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\lab'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
Start-Transcript -LiteralPath (Join-Path $logRoot ("build-lab-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))) | Out-Null

& (Join-Path $PSScriptRoot 'Test-LabHost.ps1') -Topology $Topology
if ($LASTEXITCODE -ne 0) {
    throw 'The host does not meet the laboratory requirements.'
}

Import-Module AutomatedLab

$sourceDomain = 'lab-a.test'
$targetDomain = 'lab-b.test'
$network = 'DomainBridgeLabNet'
$migrationOu = 'Migration'
$adminUser = 'LabAdmin'

# --- Laboratory secret: generated here, never written to the repository -----
$secretRoot = Join-Path $env:LOCALAPPDATA 'DomainBridgeLab'
$secretPath = Join-Path $secretRoot "$LabName.credential.clixml"
New-Item -ItemType Directory -Path $secretRoot -Force | Out-Null

$alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'.ToCharArray()
$labPasswordPlain = 'Lb1!' + (-join (1..20 | ForEach-Object { $alphabet | Get-Random }))
$labPassword = ConvertTo-SecureString -String $labPasswordPlain -AsPlainText -Force
[pscredential]::new($adminUser, $labPassword) | Export-Clixml -LiteralPath $secretPath

# --- Definition ---------------------------------------------------------------
New-LabDefinition -Name $LabName -DefaultVirtualizationEngine HyperV
Add-LabVirtualNetworkDefinition -Name $network -AddressSpace '192.168.77.0/24'

Add-LabDomainDefinition -Name $sourceDomain -AdminUser $adminUser -AdminPassword $labPasswordPlain
Add-LabDomainDefinition -Name $targetDomain -AdminUser $adminUser -AdminPassword $labPasswordPlain

$PSDefaultParameterValues = @{
    'Add-LabMachineDefinition:Network' = $network
}

Set-LabInstallationCredential -Username $adminUser -Password $labPasswordPlain
Add-LabMachineDefinition -Name 'DCA' -DomainName $sourceDomain -Roles RootDC `
    -OperatingSystem $ServerOperatingSystem -Memory 1536MB -IpAddress '192.168.77.10'

$clients = @(
    @{ Name = 'CL11'; OperatingSystem = $Windows11OperatingSystem; IpAddress = '192.168.77.21' }
)
if ($Topology -eq 'Full') {
    $clients += @{ Name = 'CL10'; OperatingSystem = $Windows10OperatingSystem; IpAddress = '192.168.77.22' }
}

foreach ($client in $clients) {
    Add-LabMachineDefinition -Name $client.Name -DomainName $sourceDomain `
        -OperatingSystem $client.OperatingSystem -Memory 3GB -IpAddress $client.IpAddress
}

Set-LabInstallationCredential -Username $adminUser -Password $labPasswordPlain
Add-LabMachineDefinition -Name 'DCB' -DomainName $targetDomain -Roles RootDC `
    -OperatingSystem $ServerOperatingSystem -Memory 1536MB -IpAddress '192.168.77.11'

if ($Topology -eq 'Full') {
    Add-LabMachineDefinition -Name 'SRV' -DomainName $targetDomain `
        -OperatingSystem $ServerOperatingSystem -Memory 4GB -IpAddress '192.168.77.30'
}

Install-Lab

# --- Name resolution and trust between the two forests ------------------------
Invoke-LabCommand -ComputerName 'DCA' -ActivityName 'Forwarder to forest B' -ScriptBlock {
    if (-not (Get-DnsServerZone -Name 'lab-b.test' -ErrorAction SilentlyContinue)) {
        Add-DnsServerConditionalForwarderZone -Name 'lab-b.test' -MasterServers '192.168.77.11'
    }
}
Invoke-LabCommand -ComputerName 'DCB' -ActivityName 'Forwarder to forest A' -ScriptBlock {
    if (-not (Get-DnsServerZone -Name 'lab-a.test' -ErrorAction SilentlyContinue)) {
        Add-DnsServerConditionalForwarderZone -Name 'lab-a.test' -MasterServers '192.168.77.10'
    }
}

$trustExists = Invoke-LabCommand -ComputerName 'DCA' -PassThru -ActivityName 'Check forest trust' -ScriptBlock {
    [bool](Get-ADTrust -Filter "Name -eq 'lab-b.test'" -ErrorAction SilentlyContinue)
}
if (-not $trustExists) {
    Invoke-LabCommand -ComputerName 'DCA' -ActivityName 'Create two-way forest trust' -ArgumentList $adminUser, $labPasswordPlain -ScriptBlock {
        param($remoteUser, $remotePassword)
        $remoteContext = [System.DirectoryServices.ActiveDirectory.DirectoryContext]::new(
            'Forest', 'lab-b.test', "$remoteUser@lab-b.test", $remotePassword)
        $remoteForest = [System.DirectoryServices.ActiveDirectory.Forest]::GetForest($remoteContext)
        $localForest = [System.DirectoryServices.ActiveDirectory.Forest]::GetCurrentForest()
        $localForest.CreateTrustRelationship($remoteForest, 'Bidirectional')
    }
}

# --- Synthetic identities: already migrated to B, as the product assumes -------
$syntheticUsers = @(
    @{ Source = 'alice.synth'; Target = 'alice.synth' }   # same login in A and B
    @{ Source = 'bruno.synth'; Target = 'bruno.synth' }
    @{ Source = 'carla.synth'; Target = 'c.synth' }       # login changes between A and B
)

Invoke-LabCommand -ComputerName 'DCA' -ActivityName 'Synthetic users in A' -ArgumentList $syntheticUsers, $labPasswordPlain -ScriptBlock {
    param($users, $password)
    $secure = ConvertTo-SecureString -String $password -AsPlainText -Force
    foreach ($user in $users) {
        if (-not (Get-ADUser -Filter "SamAccountName -eq '$($user.Source)'")) {
            New-ADUser -Name $user.Source -SamAccountName $user.Source -AccountPassword $secure -Enabled $true -PasswordNeverExpires $true
        }
    }
}

Invoke-LabCommand -ComputerName 'DCB' -ActivityName 'Synthetic users and computer accounts in B' `
    -ArgumentList $syntheticUsers, $labPasswordPlain, $migrationOu, @($clients.Name) -ScriptBlock {
    param($users, $password, $ouName, $computerNames)
    $secure = ConvertTo-SecureString -String $password -AsPlainText -Force
    $domainDn = (Get-ADDomain).DistinguishedName
    $ouDn = "OU=$ouName,$domainDn"
    if (-not (Get-ADOrganizationalUnit -Filter "DistinguishedName -eq '$ouDn'")) {
        New-ADOrganizationalUnit -Name $ouName -Path $domainDn
    }
    foreach ($user in $users) {
        if (-not (Get-ADUser -Filter "SamAccountName -eq '$($user.Target)'")) {
            New-ADUser -Name $user.Target -SamAccountName $user.Target -AccountPassword $secure -Enabled $true -PasswordNeverExpires $true
        }
    }
    foreach ($computerName in $computerNames) {
        if (-not (Get-ADComputer -Filter "Name -eq '$computerName'")) {
            New-ADComputer -Name $computerName -Path $ouDn -Enabled $true
        }
    }
}

Checkpoint-LabVM -All -SnapshotName 'Baseline'
Show-LabDeploymentSummary

Write-Host "Laboratory '$LabName' ($Topology) is ready. Baseline checkpoint taken."
Write-Host "Laboratory credential stored for the current user at: $secretPath"
