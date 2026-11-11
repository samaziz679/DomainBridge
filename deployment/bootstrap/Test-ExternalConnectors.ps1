[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PilotConfigurationPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$validator = Join-Path $repositoryRoot 'deployment\pilot\Validate-PilotConfiguration.ps1'

& $validator -ConfigurationPath $PilotConfigurationPath

Write-Output 'Connector readiness checks completed:'
Write-Output '- API endpoint parameters are structurally present.'
Write-Output '- PostgreSQL endpoint parameters are structurally present.'
Write-Output '- PKI template and trusted-root thumbprint are structurally present.'
Write-Output '- Deployment channel and pilot OU are structurally present.'
Write-Output 'No network connection, certificate enrollment, database change, Intune change, or domain action was made.'
