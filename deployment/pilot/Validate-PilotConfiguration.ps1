[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ConfigurationPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ConfigurationPath -PathType Leaf)) {
    throw "Pilot configuration file not found: $ConfigurationPath"
}

$requiredKeys = @(
    'DOMAINBRIDGE_API_FQDN',
    'DOMAINBRIDGE_API_HTTPS_PORT',
    'DOMAINBRIDGE_POSTGRES_HOST',
    'DOMAINBRIDGE_POSTGRES_DATABASE',
    'DOMAINBRIDGE_POSTGRES_RUNTIME_IDENTITY',
    'DOMAINBRIDGE_PKI_CLIENT_TEMPLATE',
    'DOMAINBRIDGE_PKI_ROOT_THUMBPRINT',
    'DOMAINBRIDGE_PILOT_OU_DN',
    'DOMAINBRIDGE_DEPLOYMENT_CHANNEL'
)

$configuration = @{}
foreach ($line in Get-Content -LiteralPath $ConfigurationPath) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
        continue
    }

    $parts = $trimmed.Split('=', 2)
    if ($parts.Count -ne 2) {
        throw "Invalid configuration line: $line"
    }

    $configuration[$parts[0].Trim()] = $parts[1].Trim()
}

foreach ($key in $requiredKeys) {
    if (-not $configuration.ContainsKey($key) -or
        [string]::IsNullOrWhiteSpace($configuration[$key]) -or
        $configuration[$key] -match 'REPLACE|example\.internal') {
        throw "A real non-secret value is required for $key."
    }
}

if ($configuration['DOMAINBRIDGE_API_HTTPS_PORT'] -notmatch '^[0-9]+$' -or
    [int]$configuration['DOMAINBRIDGE_API_HTTPS_PORT'] -lt 1 -or
    [int]$configuration['DOMAINBRIDGE_API_HTTPS_PORT'] -gt 65535) {
    throw 'DOMAINBRIDGE_API_HTTPS_PORT must be between 1 and 65535.'
}

if ($configuration['DOMAINBRIDGE_DEPLOYMENT_CHANNEL'] -notin @('Intune', 'GPO')) {
    throw 'DOMAINBRIDGE_DEPLOYMENT_CHANNEL must be Intune or GPO.'
}

Write-Output 'Pilot configuration is structurally valid. No network or domain changes were made.'
