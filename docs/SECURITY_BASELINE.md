# Security baseline

## Trust boundaries

1. Administrators authenticate to the console through Microsoft Entra ID with MFA and role-based access.
2. A Windows agent establishes only outbound TLS connections to the control plane.
3. Each agent uses a unique device certificate; its private key should be TPM-protected where available.
4. The directory connector runs in the target environment under a gMSA or equivalent managed identity with permissions delegated only to the migration OU.

## Credentials and secrets

- No domain-administrator password is sent to an agent.
- No user password, Windows credential cache or LSA secret is collected.
- Domain join uses a controlled connector and a short-lived, endpoint-bound mechanism; sensitive provisioning material is encrypted for the intended agent, expires quickly and is deleted after use.
- Secrets are stored only in an approved vault and are redacted from logs.

## Audit

Every campaign, approval, task issuance, agent acknowledgement, reboot, migration result and rollback action must create an immutable audit event containing time, actor, target endpoint, task identifier and outcome.

## Deployment channels

- **Intune:** Win32 package in SYSTEM context, versioned detection rule, certificate policy and staged rollout group.
- **GPO:** assigned MSI or signed startup deployment sourced from protected SYSVOL/DFS, scoped to approved computer OUs.
