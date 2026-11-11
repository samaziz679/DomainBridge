# DomainBridge development rules

## Product boundary

DomainBridge migrates authorised Windows endpoints and existing Windows profiles from TerangaGold to edv-ops.com. It is an administration and migration tool, not a credential-recovery or security-bypass tool.

## Mandatory safety rules

- Never place passwords, client secrets, private keys, Offline Domain Join blobs, or production certificates in source control, logs, test fixtures, Intune packages, GPO scripts, or issue text.
- Do not use Domain Admin credentials on endpoints. Directory operations run through a connector using delegated least-privilege rights in a dedicated target OU.
- Agent-to-console traffic uses outbound HTTPS with mutual TLS. Do not expose inbound SMB, WinRM, RDP, or RPC as the control channel.
- Every destructive action requires an explicit campaign approval, idempotency key, audit event, and tested recovery path.
- Tests must use synthetic identities and laboratory domains only.

## Delivery rules

- The agent is a single signed Windows service, distributable by both Intune and GPO.
- Intune and GPO packages must share versioning, detection and upgrade semantics.
- Changes affecting identity, certificates, domain join, profile mapping, or rollback require security review.
