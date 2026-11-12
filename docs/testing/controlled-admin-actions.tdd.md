# TDD evidence — controlled administrator actions

## Scope

User journey: an authorised administrator requests a known elevated action at a specific version; the policy allows it only after signature verification and approval, and rejects arbitrary parameters or unknown actions.

## Acceptance criteria

- AC-001: `windows-update-reset` version `1.0` is permitted only with verified signature and approval.
- AC-002: missing signature or approval returns a concrete denial reason.
- AC-003: arbitrary action names and undeclared parameters are rejected without an execution payload.

## RED / GREEN evidence

| Stage | Commit | Evidence |
| --- | --- | --- |
| RED | `b9fa7b1` | Admin-action contracts and policy did not exist. |
| GREEN | `8e3220f` | Four contract tests passed for positive and denial cases. |

## Validation

- `dotnet test C:\dbw\tests\DomainBridge.Contracts.Tests\DomainBridge.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~AdminActionPolicyTests"` — 4 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 65 passed.

## Required production work

The `HasVerifiedSignature` and `HasApproval` flags are contracts, not production security proofs. Before enabling elevated execution, DomainBridge must validate signed manifests against a configured trusted publisher/PKI chain and validate the operator role plus approval record using the real Entra/AD identity provider. No elevated action is enabled by this increment.
