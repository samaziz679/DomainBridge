<#
.SYNOPSIS
    Builds the DomainBridge agent MSI: the single package delivered by Intune and by GPO.
.DESCRIPTION
    Publishes the agent as one self-contained executable, optionally signs it,
    packages it, and optionally signs the package.

    Signing uses a code-signing certificate that is already in a Windows certificate
    store, selected by thumbprint. The private key never enters the repository or
    this script. Without -SigningCertificateThumbprint the package is built unsigned
    and named accordingly; an unsigned package is for the spare test machine only.
.EXAMPLE
    ./Build-AgentMsi.ps1
.EXAMPLE
    ./Build-AgentMsi.ps1 -SigningCertificateThumbprint <thumbprint> -TimestampServer http://timestamp.example
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',

    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$SigningCertificateThumbprint,

    [string]$TimestampServer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$agentProject = Join-Path $repositoryRoot 'src\DomainBridge.Agent\DomainBridge.Agent.csproj'
$publishRoot = Join-Path $repositoryRoot 'artifacts\agent-publish'
$outputRoot = Join-Path $repositoryRoot 'artifacts\agent-installer'
$sourceFile = Join-Path $PSScriptRoot 'Product.wxs'

[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$version = $buildProperties.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Directory.Build.props must define a three-part <Version>; found '$version'."
}

function Get-SigningCertificate {
    param([string]$Thumbprint)

    $certificate = Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $Thumbprint } |
        Select-Object -First 1
    if (-not $certificate) {
        throw "No code-signing certificate with thumbprint $Thumbprint was found in the Windows certificate stores."
    }

    return $certificate
}

function Add-Signature {
    param([string]$Path, $Certificate)

    $arguments = @{ FilePath = $Path; Certificate = $Certificate; HashAlgorithm = 'SHA256' }
    if ($TimestampServer) { $arguments.TimestampServer = $TimestampServer }
    $signature = Set-AuthenticodeSignature @arguments
    if ($signature.Status -ne 'Valid') {
        throw "Signing '$Path' failed: $($signature.Status) $($signature.StatusMessage)"
    }
}

$certificate = if ($SigningCertificateThumbprint) { Get-SigningCertificate -Thumbprint $SigningCertificateThumbprint } else { $null }

Remove-Item -LiteralPath $publishRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishRoot, $outputRoot -Force | Out-Null

# Single-file publishing is declared in the agent project itself, so that the
# package lock file is the same for every build and publish.
dotnet publish $agentProject --configuration $Configuration --runtime win-x64 --self-contained true `
    -p:DebugType=none --output $publishRoot
if ($LASTEXITCODE -ne 0) { throw "Agent publish failed with exit code $LASTEXITCODE." }

$agentExecutable = Join-Path $publishRoot 'DomainBridge.Agent.exe'
if ($certificate) { Add-Signature -Path $agentExecutable -Certificate $certificate }

$suffix = if ($certificate) { '' } else { '-unsigned' }
$msiPath = Join-Path $outputRoot "DomainBridge-Agent-$version$suffix.msi"
Remove-Item -LiteralPath $msiPath -Force -ErrorAction SilentlyContinue

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "Tool restore failed with exit code $LASTEXITCODE." }

dotnet tool run wix build -arch x64 -d ProductVersion=$version -d AgentPublishDir=$publishRoot -o $msiPath $sourceFile
if ($LASTEXITCODE -ne 0) { throw "WiX build failed with exit code $LASTEXITCODE." }

if ($certificate) {
    Add-Signature -Path $msiPath -Certificate $certificate
    Write-Host "Signed agent package: $msiPath"
}
else {
    Write-Warning "UNSIGNED agent package: $msiPath. Use it on the spare test machine only."
}

Write-Output $msiPath
