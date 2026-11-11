[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackageRoot,

    [switch]$LabMode
)

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$installer = Join-Path $repositoryRoot 'deployment\\scripts\\Install-DomainBridgeAgent.ps1'
& $installer -PackageRoot $PackageRoot -LabMode:$LabMode
exit $LASTEXITCODE
