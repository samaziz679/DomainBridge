# TDD evidence — maintenance result reporting

## Scope

User journey: after an approved maintenance task, an agent reports a normalized outcome to the laboratory control plane; the API accepts it only for the approved campaign and its targeted device.

## Evidence

| Stage | Commit | Evidence |
| --- | --- | --- |
| RED — API result endpoint | `f401165` | Result endpoints returned HTTP 404. |
| GREEN — API result endpoint | `a725c2f` | Two integration tests passed: approval and target authorization enforced. |
| RED — agent result client | `541b8eb` | `MaintenanceResultClient` did not compile because it did not exist. |
| GREEN — agent result client | `ca16bae` | Two agent tests passed: structured payload and local input validation. |

## Guarantees

- A result before approval receives HTTP 409.
- A result for a non-target device receives HTTP 403.
- Only `succeeded`, `failed`, and `rebootRequired` outcomes are accepted.
- Transport contains no free-form output, credentials, or script contents.
- A normalized audit event is appended for every accepted result.

## Validation

- `dotnet test C:\dbw\tests\DomainBridge.Api.Tests\DomainBridge.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~MaintenanceJobResultEndpointTests"` — 2 passed.
- `dotnet test C:\dbw\tests\DomainBridge.Agent.Tests\DomainBridge.Agent.Tests.csproj --no-restore --filter "FullyQualifiedName~MaintenanceResultClientTests"` — 2 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 56 passed.

## Limit

This is a development/test transport only. Before production, PostgreSQL durability, actual certificate-enforced agent authentication, operator RBAC and a signed action runner are required.
