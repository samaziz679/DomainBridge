# DomainBridge development rules

## Product boundary

DomainBridge migrates authorised Windows endpoints and existing Windows profiles from a **source domain A** to a **target domain B**. It is an administration and migration tool, not a credential-recovery or security-bypass tool.

## Mandatory safety rules

- Never place passwords, client secrets, private keys, Offline Domain Join blobs, or production certificates in source control, logs, test fixtures, Intune packages, GPO scripts, or issue text.
- Do not use Domain Admin credentials on endpoints. Directory operations run through a connector using delegated least-privilege rights in a dedicated target OU.
- Agent-to-console traffic uses outbound HTTPS with mutual TLS. Do not expose inbound SMB, WinRM, RDP, or RPC as the control channel.
- Every destructive action requires an explicit campaign approval, idempotency key, audit event, and tested recovery path.
- Tests must use synthetic identities only. Tests that change a machine may run against the real source and target domains only inside the dedicated test organisational units, on a designated spare machine with no real user, as recorded in `docs/adr/ADR-004-poste-admin-et-poste-de-reserve.md`. Never test on a machine or a profile that belongs to a real user.

## Delivery rules

- The agent is a single signed Windows service, distributable by both Intune and GPO.
- Intune and GPO packages must share versioning, detection and upgrade semantics.
- Changes affecting identity, certificates, domain join, profile mapping, or rollback require security review.
