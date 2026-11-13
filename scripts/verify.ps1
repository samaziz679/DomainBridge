<#
.SYNOPSIS
    Single verification entry point for DomainBridge, used unchanged locally and in CI.
.DESCRIPTION
    Runs every quality gate in order and stops at the first failure.
    Exit code 0 means every step passed; exit code 1 names the failed step.
.PARAMETER SkipConsole
    Skips the web console steps (npm). Use only when Node.js is unavailable.
.PARAMETER MinimumLineCoverage
    Minimum line coverage, in percent, across the measured .NET assemblies.
#>
[CmdletBinding()]
param(
    [switch]$SkipConsole,

    [ValidateRange(0, 100)]
    [int]$MinimumLineCoverage = 80,

    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'DomainBridge.sln'
$consoleRoot = Join-Path $repositoryRoot 'apps\DomainBridge.Console'
$coverageRoot = Join-Path $repositoryRoot 'artifacts\coverage'

function Invoke-Step {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    Write-Host "==> $Name"
    $global:LASTEXITCODE = 0
    & $Action
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $Name (exit code $LASTEXITCODE)"
        exit 1
    }
}

function Get-LineCoverage {
    param([Parameter(Mandatory)][string]$Root)

    # The same assembly can appear in several test reports; keep its best result.
    $packages = @{}
    Get-ChildItem -LiteralPath $Root -Recurse -Filter 'coverage.cobertura.xml' | ForEach-Object {
        [xml]$report = Get-Content -LiteralPath $_.FullName -Raw
        foreach ($package in $report.SelectNodes('//package')) {
            $lines = @($package.SelectNodes('.//class/lines/line'))
            $covered = @($lines | Where-Object { [int]$_.hits -gt 0 }).Count
            $name = $package.name
            if (-not $packages.ContainsKey($name) -or $packages[$name].Covered -lt $covered) {
                $packages[$name] = [pscustomobject]@{ Valid = $lines.Count; Covered = $covered }
            }
        }
    }

    $valid = ($packages.Values | Measure-Object -Property Valid -Sum).Sum
    $covered = ($packages.Values | Measure-Object -Property Covered -Sum).Sum
    if (-not $valid) {
        return $null
    }

    return [math]::Round(100 * $covered / $valid, 1)
}

Invoke-Step 'Restore (locked)' {
    dotnet restore $solution --locked-mode
}

Invoke-Step 'Format' {
    dotnet format $solution --verify-no-changes --no-restore
}

Invoke-Step 'Build (warnings as errors, analyzers)' {
    dotnet build $solution --configuration $Configuration --no-restore
}

Invoke-Step 'Test with coverage' {
    Remove-Item -LiteralPath $coverageRoot -Recurse -Force -ErrorAction SilentlyContinue
    dotnet test $solution --configuration $Configuration --no-build `
        --collect 'XPlat Code Coverage' --results-directory $coverageRoot
}

Invoke-Step "Coverage threshold ($MinimumLineCoverage %)" {
    $coverage = Get-LineCoverage -Root $coverageRoot
    if ($null -eq $coverage) {
        Write-Host 'No coverage report was produced.'
        $global:LASTEXITCODE = 1
        return
    }

    Write-Host "Line coverage: $coverage %"
    if ($coverage -lt $MinimumLineCoverage) {
        $global:LASTEXITCODE = 1
    }
}

if (-not $SkipConsole) {
    Push-Location $consoleRoot
    try {
        Invoke-Step 'Console: install' { npm ci }
        Invoke-Step 'Console: test' { npm test }
        Invoke-Step 'Console: build' { npm run build }
        Invoke-Step 'Console: dependency audit' { npm audit --audit-level=high }
    }
    finally {
        Pop-Location
    }
}

Write-Host 'VERIFY OK'
exit 0
