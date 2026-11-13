# TDD evidence — signed manifest verification

## User journey

As an authorised operator, I want an approved maintenance manifest to be cryptographically bound to its exact payload so that a modified or untrusted maintenance request cannot be accepted.

## Guarantees

| # | Guarantee | Evidence | Result |
| --- | --- | --- | --- |
| 1 | A detached CMS signature from a trusted Code Signing certificate is accepted. | `SignedManifestPolicyTests.Validate_accepts_a_detached_manifest_signed_by_a_trusted_publisher` | Pass |
| 2 | A changed manifest is rejected. | `SignedManifestPolicyTests.Validate_rejects_a_signature_when_the_manifest_content_has_changed` | Pass |
| 3 | An untrusted signer is rejected. | `SignedManifestPolicyTests.Validate_rejects_a_manifest_signed_by_an_untrusted_publisher` | Pass |
| 4 | An attached CMS package is rejected. | `SignedManifestPolicyTests.Validate_rejects_an_attached_cms_package` | Pass |

## Evidence

| Stage | Commit | Result |
| --- | --- | --- |
| RED | `93c37f9` | The manifest policy did not exist. |
| GREEN | `3c1d603` | Four focused CMS tests and the full solution suite passed. |

## Validation

- `dotnet test C:\dbw\tests\DomainBridge.Api.Tests\DomainBridge.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~SignedManifestPolicyTests"` — 4 passed.
- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 75 passed.

## Known gap

The validator uses the configured publisher allow-list, certificate validity and Code Signing EKU. It does not yet enforce a PKI chain, revocation or timestamp policy, and it is not yet connected to an elevated execution endpoint.
