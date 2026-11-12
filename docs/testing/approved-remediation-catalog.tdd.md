# TDD evidence — approved remediation catalogue

## Scope

User journey: as an IT operator, I can view only approved maintenance actions and their policy metadata, so that no arbitrary PowerShell command can be executed from the control plane.

## RED / GREEN evidence

| Stage | Commit | Evidence |
| --- | --- | --- |
| RED — agent catalogue | `65d1fdc` | `RemediationCatalog` was unresolved at compile time. |
| GREEN — agent catalogue | `af8919d` | `dotnet test tests/DomainBridge.Agent.Tests/DomainBridge.Agent.Tests.csproj --no-restore` passed: 10 tests. |
| RED — console API | `166a71f` | `GET /api/v1/console/remediations` returned HTTP 404. |
| GREEN — console API | `45c86ef` | API and agent targeted test suites passed. |
| RED — console view | `ab00a99` | Maintenance catalogue heading was absent from the UI. |
| GREEN — console view | pending | Console tests, build and full .NET solution test pass after implementation. |

## Guarantees

| # | Guarantee | Evidence | Type | Result |
| --- | --- | --- | --- | --- |
| 1 | The agent resolves only known remediation identifiers. | `RemediationCatalogTests` | Unit | PASS |
| 2 | State-changing remediation declares approval and restart policy. | `RemediationCatalogTests` | Unit | PASS |
| 3 | The console API publishes policy metadata without arbitrary command content. | `ConsoleReadEndpointTests` | Integration | PASS |
| 4 | The web console renders approved actions without an execution control. | `App.test.tsx` | UI unit | PASS |
| 5 | The console client retrieves the remediation catalogue with the other read-only data. | `api.test.ts` | Client unit | PASS |

## Validation performed

- `npm test -- --run` — 4 tests passed.
- `npm run build` — Vite production build passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 47 tests passed.

## Known limits

This increment does not execute remediation actions. Job creation, role-based approval, signed action payloads, agent polling, result collection and durable campaign storage remain future increments. This deliberate limitation prevents remote arbitrary command execution.
