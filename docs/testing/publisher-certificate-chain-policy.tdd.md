# TDD evidence — publisher certificate chain policy

## User journey

As a platform operator, I want a signer to chain only to an explicitly configured PKI root so that an unrelated trusted certificate on a workstation cannot authorise a maintenance manifest.

## Guarantees

| # | Guarantee | Evidence | Result |
| --- | --- | --- | --- |
| 1 | A publisher issued by the custom root is accepted. | `PublisherCertificateChainPolicyTests.Validate_accepts_a_publisher_chained_to_a_configured_custom_root` | Pass |
| 2 | A publisher issued by another root is rejected. | `PublisherCertificateChainPolicyTests.Validate_rejects_a_publisher_chained_to_an_unknown_root` | Pass |
| 3 | An empty root set is rejected. | `PublisherCertificateChainPolicyTests.Validate_rejects_an_empty_custom_root_set` | Pass |

## Evidence

| Stage | Commit | Result |
| --- | --- | --- |
| RED | `7f28234` | The custom-root chain policy did not exist. |
| GREEN | `87c2501` | Three focused chain-policy tests and the full solution suite passed. |

## Validation

- `dotnet test C:\dbw\tests\DomainBridge.Api.Tests\DomainBridge.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PublisherCertificateChainPolicyTests"` — 3 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 78 passed.

## Known gap

The component receives roots in memory for a laboratory integration. It is not yet wired to configuration, a certificate source, revocation/OCSP, timestamp validation or an elevated execution endpoint.
