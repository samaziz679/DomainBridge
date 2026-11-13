> **Obsolète depuis le 2026-11-13.** Le laboratoire à deux forêts n'est pas construit : les essais se font sur un poste de réserve réel, voir `docs/adr/ADR-004-poste-admin-et-poste-de-reserve.md` et `docs/lab/PHASE1_FAISABILITE.md`. Ce document est conservé pour référence.

# DomainBridge synthetic laboratory

## Purpose

The laboratory validates orchestration against a source domain A and target domain B without using production identities, credentials or workstations.

## Minimum topology

- One isolated virtual network.
- One synthetic directory service for domain A.
- One synthetic directory service for domain B.
- One Windows test workstation joined only to domain A.
- One management server for the API and simulated connector.
- One test certificate authority isolated from production PKI.

## Synthetic data

Use fabricated device and account names only. Do not copy production directory exports, user profiles, passwords, certificate private keys or ticket material into the lab.

## MVP execution

1. Deploy the signed test agent through the selected Intune or GPO simulation path.
2. Enroll the agent with a test client certificate.
3. Capture the read-only technical inventory.
4. Generate a simulated mapping from domain A to domain B.
5. Verify audit events and rollback documentation.

The connector produces a Simulated plan only. It does not query LDAP, join a domain, alter a profile or create a target account.
