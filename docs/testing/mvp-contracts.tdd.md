# TDD evidence — MVP enrollment contract

## Source and scope

Journeys derived during this run from ADR-001. The scope is a non-secret enrollment metadata contract only. It does not contact a directory, execute a migration, or accept credentials.

## User journeys

1. As an agent, I submit a stable device identifier, certificate thumbprint and version so the central service can establish device identity without a password.
2. As an operator, I want malformed enrollment metadata rejected before any remote operation is scheduled.

## RED and GREEN evidence

| Stage | Command | Result | Guarantee |
|---|---|---|---|
| RED | `dotnet test tests\\DomainBridge.Contracts.Tests\\DomainBridge.Contracts.Tests.csproj --no-restore` | Failed with `CS0103`: `EnrollmentRequest` and `EnrollmentError` did not exist | The test project exercised the absent public contract rather than a setup failure. |
| GREEN | `dotnet test tests\\DomainBridge.Contracts.Tests\\DomainBridge.Contracts.Tests.csproj --no-restore` | Passed: 3/3 | The first minimal contract accepted non-secret metadata and rejected missing agent ID or certificate thumbprint. |
| Coverage | `dotnet test tests\\DomainBridge.Contracts.Tests\\DomainBridge.Contracts.Tests.csproj --collect:\"XPlat Code Coverage\"` | Passed: 4/4; Cobertura line rate `1`, branch rate `1` | All current validation branches are tested, including a missing agent version. |

## Test specification

| # | What is guaranteed | Test | Type | Result |
|---|---|---|---|---|
| 1 | Blank device ID is rejected | `Create_requires_an_agent_identifier` | Unit | PASS |
| 2 | Blank certificate thumbprint is rejected | `Create_rejects_a_missing_certificate_thumbprint` | Unit | PASS |
| 3 | Blank version is rejected | `Create_rejects_a_missing_agent_version` | Unit | PASS |
| 4 | Valid metadata has no secret field and is accepted | `Create_accepts_identity_metadata_without_any_secret` | Unit | PASS |

## Coverage and known gaps

The contract assembly is currently 100% line and branch covered. Integration, mTLS, certificate validation, API authorization and real deployment remain deliberately out of scope until their dedicated test plans and threat model are approved.
