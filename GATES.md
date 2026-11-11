# Gates: DomainBridge foundation

OWNS: AGENTS.md, GATES.md, docs/**

Scope: establish the documented, reviewable foundation for secure DomainBridge development.

- [x] G1: The project scope distinguishes the central console, Windows agent and directory connector.
  EVIDENCE: 2026-11-11 — reviewed docs/ARCHITECTURE.md component and trust-boundary sections.

- [x] G2: The security baseline prohibits embedded domain credentials and requires auditability, certificate-based agent authentication and least privilege.
  EVIDENCE: 2026-11-11 — reviewed AGENTS.md and docs/SECURITY_BASELINE.md.

- [x] G3: The deployment design requires one signed agent distributed through both Intune and GPO.
  EVIDENCE: 2026-11-11 — reviewed docs/ARCHITECTURE.md deployment architecture.

- [ ] G4: ECC Tools has been installed on this repository and its generated PR has been reviewed.
  EVIDENCE: pending
