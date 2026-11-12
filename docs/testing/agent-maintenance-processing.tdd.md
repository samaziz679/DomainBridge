# TDD evidence — agent maintenance processing

## Scope

User journey: an agent reads approved tasks, runs each through its local allowlist and reports a typed result. The local runner remains the final authority even if upstream data is incorrect.

## RED / GREEN evidence

| Stage | Commit | Evidence |
| --- | --- | --- |
| RED | `1328db5` | Job-source and result-reporter abstractions did not exist. |
| GREEN | `8abac55` | Processing tests passed: a safe diagnostic succeeds, while a state-changing remediation is refused and reported as failed. |

## Guarantees

- A blank device ID stops processing before any task is fetched.
- Every fetched task is passed through `MaintenanceActionRunner`; it is never executed directly.
- The result reporter receives only typed outcomes.
- A task with `windows-update-reset` is rejected locally even if it was supplied by an approved campaign.

## Validation

- `dotnet test C:\dbw\tests\DomainBridge.Agent.Tests\DomainBridge.Agent.Tests.csproj --no-restore --filter "FullyQualifiedName~MaintenanceJobProcessorTests"` — 2 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 61 passed.

## Limit

The loop is an injectable component, not yet hosted as a scheduled Windows service. Production hosting must add certificate-backed HTTP configuration, backoff, cancellation, idempotency and durable result storage before it is enabled.
