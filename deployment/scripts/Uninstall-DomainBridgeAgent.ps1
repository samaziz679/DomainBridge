[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InstallRoot = "$env:ProgramFiles\DomainBridge\Agent"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ((Test-Path -LiteralPath $InstallRoot -PathType Container) -and
    $PSCmdlet.ShouldProcess($InstallRoot, 'Remove DomainBridge agent package')) {
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
}
