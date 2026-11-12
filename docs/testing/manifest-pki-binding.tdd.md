# TDD evidence — manifest PKI binding

## Guarantees

- The strict signed-manifest validation overload accepts a trusted Code Signing signer chained to its supplied custom root.
- It rejects a signer with an approved thumbprint when that signer chains to a different root.
- The existing laboratory overload remains available for non-production test flows; it must not be used for elevated execution.

## Evidence

| Stage | Commit | Result |
| --- | --- | --- |
| RED | `35eb97e` | The signed-manifest validator had no custom-root overload. |
| GREEN | `22f4ae1` | Six focused signed-manifest tests and the full solution suite passed. |

## Validation

- `dotnet test C:\dbw\tests\DomainBridge.Api.Tests\DomainBridge.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~SignedManifestPolicyTests"` — 6 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 80 passed.

## Known gap

Roots are still supplied by the host in memory. Configuration loading, revocation/OCSP, timestamp policy, Entra operator authorization and the execution endpoint remain out of scope for this increment.
