param(
    [string]$InstallRoot = "$env:ProgramFiles\DomainBridge\Agent"
)

$agentExecutable = Join-Path $InstallRoot 'DomainBridge.Agent.exe'
$stateFile = Join-Path $InstallRoot 'installation-state.json'

if ((Test-Path -LiteralPath $agentExecutable -PathType Leaf) -and
    (Test-Path -LiteralPath $stateFile -PathType Leaf)) {
    exit 0
}

exit 1
