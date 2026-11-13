# DomainBridge development plan

## Context

DomainBridge is the control plane for controlled migration of Windows endpoints and profiles from a **source domain A** to a **target domain B**. Intune remains the system of record for MDM, applications, remediations and Windows updates.

## Stages

| Stage | Deliverable | Pass condition |
|---|---|---|
| 0 — Governance | Scope, risk model, owners and laboratory boundary | Approved migration policy and no production secrets in the repository |
| 1 — Foundation | Architecture, rules, ECC Tools baseline and CI | Security review of the generated ECC Tools pull request |
| 2 — Hybrid control plane | Console authentication, roles, campaign, audit and read-only Intune/AD/Entra correlation APIs | A campaign cannot execute without authorised approval and every target has a reconciled source state |
| 3 — Windows agent | Signed service, certificate enrolment and read-only inventory | A laboratory endpoint returns authenticated inventory over mTLS |
| 4 — Agent distribution | Intune `.intunewin` and GPO MSI/startup deployment | Both methods install, detect, upgrade and uninstall the same agent; this does not replace Intune application delivery |
| 5 — Laboratory migration | Prechecks, profile-tool orchestration, target-domain join, restart and validation | Synthetic-user migration and rollback both pass in a lab |
| 6 — Pilot | Small approved production wave | Measured success, incident and support runbook review |
| 7 — Rollout | Controlled waves and reporting | Operational acceptance and retained audit evidence |

## Explicit product boundary

DomainBridge must integrate with Intune rather than duplicate it. No generic package catalogue, patching engine, update ring manager or MDM policy engine will be developed. Engineering priority is the migration workflow, hybrid state reconciliation and evidence-based rollback.

## Stage 1 output

- `AGENTS.md`
- Security baseline
- Initial architecture
- Acceptance-gate ledger
- ECC Tools analysis and setup pull request
