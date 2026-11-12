# TDD evidence — allowlisted diagnostic runner

## Scope

User journey: an agent can evaluate only the approved read-only diagnostics and return a normalised outcome; scripts, unknown action identifiers and state-changing remediations are refused.

## RED / GREEN evidence

| Stage | Commit | Evidence |
| --- | --- | --- |
| RED | `502fb8e` | The `IMaintenanceSignals` contract and runner did not compile because they did not exist. |
| GREEN | `5007cec` | Three runner tests passed: network success, Defender failure and refusal of non-allowlisted actions. |

## Guarantees

- Only `network-diagnostics` and `defender-health-check` have executable code paths.
- State-changing identifiers, including `windows-update-reset`, return `action_not_allowed`.
- The runner accepts remediation IDs, not shell commands or script text.
- Results have a typed outcome and a short evidence code rather than raw output.

## Validation

- `dotnet test C:\dbw\tests\DomainBridge.Agent.Tests\DomainBridge.Agent.Tests.csproj --no-restore --filter "FullyQualifiedName~MaintenanceActionRunnerTests"` — 3 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 59 passed.

## Deliberate limit

The Windows signal provider and scheduled agent loop are not wired in this increment. The runner is therefore testable but cannot execute against a workstation yet. That wiring must preserve mTLS polling, approval checks and the normalised result-reporting path.
