# TDD evidence — publisher certificate policy

## Guarantees

- A publisher certificate must be present and within its validity period.
- It must expose the `Code Signing` EKU.
- Its thumbprint must appear in the configured trusted-publisher allow-list.
- The policy does not assert that a manifest or package signature has been verified.

## Evidence

| Stage | Commit | Result |
| --- | --- | --- |
| RED | `a31cacf` | The publisher certificate policy did not exist. |
| GREEN | `acb7b47` | Four focused certificate-policy tests passed. |

## Validation

- `dotnet test C:\dbw\tests\DomainBridge.Api.Tests\DomainBridge.Api.Tests.csproj --no-restore` — 41 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 71 passed.
