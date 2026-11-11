[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ConfigurationPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$validator = Join-Path $PSScriptRoot 'Validate-PilotConfiguration.ps1'
& $validator -ConfigurationPath $ConfigurationPath

Write-Output 'Next manual checks:'
Write-Output '1. Confirm the PostgreSQL audit schema has been applied.'
Write-Output '2. Confirm the API service identity can access the configured connection secret.'
Write-Output '3. Confirm agent certificates use the approved client-authentication template.'
Write-Output '4. Confirm the pilot OU and pilot devices are explicitly approved.'
Write-Output '5. Confirm no migration workflow is enabled.'
