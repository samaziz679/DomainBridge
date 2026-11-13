# TDD evidence — maintenance campaign approval and polling

## Scope

User journey: as an IT operator, I create a maintenance campaign for an approved remediation and target devices; as an agent, I can retrieve only policy-only jobs after that campaign has been approved.

## RED / GREEN evidence

| Stage | Commit | Evidence |
| --- | --- | --- |
| RED — campaign lifecycle | `e0b2c63` | Both campaign endpoints returned HTTP 404. |
| GREEN — campaign lifecycle | `0f5ad8c` | Campaign tests passed: invalid request rejected; draft campaign approved explicitly. |
| RED — agent job endpoint | `0c6e0d6` | Agent maintenance-jobs endpoint returned HTTP 404. |
| GREEN — agent job endpoint | `7957bea` | Campaign tests passed: no jobs before approval; one targeted job afterwards. |
| RED — agent client | `632aae7` | `MaintenanceJobClient` did not compile because it did not exist. |
| GREEN — agent client | `840282a` | Agent client tests passed: targeted policy job is read; blank device ID causes no request. |

## Guarantees

| # | Guarantee | Test | Type | Result |
| --- | --- | --- | --- | --- |
| 1 | Unknown remediation IDs and empty target lists are rejected. | `Create_requires_an_approved_remediation_and_target_device` | API integration | PASS |
| 2 | A campaign starts in draft and needs an explicit approval call. | `Campaign_must_be_approved_before_it_can_be_listed_as_approved` | API integration | PASS |
| 3 | A device sees no job before approval and only its targeted job afterwards. | `Agent_receives_only_approved_maintenance_jobs_for_its_device` | API integration | PASS |
| 4 | The agent client validates device ID before network access. | `MaintenanceJobClientTests` | Unit | PASS |

## Validation performed

- `dotnet test C:\dbw\tests\DomainBridge.Api.Tests\DomainBridge.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~MaintenanceCampaignEndpointTests"` — 3 passed.
- `dotnet test C:\dbw\tests\DomainBridge.Agent.Tests\DomainBridge.Agent.Tests.csproj --no-restore --filter "FullyQualifiedName~MaintenanceJobClientTests"` — 2 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 52 passed.

## Deliberate limitations

Campaigns and jobs are in-memory and available only in Development and Testing. They are not durable and are not enabled in production. The agent does not execute jobs. Durable PostgreSQL storage, operator identity/RBAC, signed job envelopes, execution receipts and an action runner must be completed before enabling operational actions.
