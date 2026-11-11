# DomainBridge development plan

## Context

DomainBridge supports the controlled migration of Windows endpoints and profiles from a **source domain A** to a **target domain B**.

## Stages

| Stage | Deliverable | Pass condition |
|---|---|---|
| 0 — Governance | Scope, risk model, owners and laboratory boundary | Approved migration policy and no production secrets in the repository |
| 1 — Foundation | Architecture, rules, ECC Tools baseline and CI | Security review of the generated ECC Tools pull request |
| 2 — Control plane | Console authentication, roles, campaign and audit APIs | A campaign cannot execute without authorised approval |
| 3 — Windows agent | Signed service, certificate enrolment and read-only inventory | A laboratory endpoint returns authenticated inventory over mTLS |
| 4 — Distribution | Intune `.intunewin` and GPO MSI/startup deployment | Both methods install, detect, upgrade and uninstall the same agent |
| 5 — Laboratory migration | Backup, profile mapping, target-domain join, restart and validation | Synthetic-user migration and rollback both pass in a lab |
| 6 — Pilot | Small approved production wave | Measured success, incident and support runbook review |
| 7 — Rollout | Controlled waves and reporting | Operational acceptance and retained audit evidence |

## Stage 1 output

- `AGENTS.md`
- Security baseline
- Initial architecture
- Acceptance-gate ledger
- ECC Tools analysis and setup pull request
