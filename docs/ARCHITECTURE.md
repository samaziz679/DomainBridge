# DomainBridge architecture

## Components

```text
Administrator
  └─ DomainBridge Console
       ├─ API and campaign service
       ├─ audit store
       ├─ source-directory read connector
       └─ target-directory connector
            └─ DomainBridge Windows Agent
```

### Console

The console manages access roles, approvals, campaigns, task definitions and immutable audit events. Administrators authenticate with an enterprise identity provider and MFA.

### Windows agent

The signed Windows service is installed by Intune or GPO. It initiates outbound HTTPS connections to the console, authenticates with a unique device certificate and performs only tasks issued for its own endpoint.

### Directory connectors

A source-directory connector reads authorised inventory data. A target-directory connector performs only delegated, least-privilege operations inside a dedicated migration OU. Neither connector distributes directory-administrator passwords to endpoints.

## Deployment architecture

| Channel | Artifact | Purpose |
|---|---|---|
| Microsoft Intune | `.intunewin` package | Install, detect, update and remove the signed agent on managed devices |
| Group Policy | MSI assignment or signed startup deployment | Install the same agent on domain-managed devices through protected SYSVOL/DFS distribution |

Both deployment routes must converge on the same agent version, service name, certificate enrolment policy and upgrade path.

## Trust and control flow

1. The endpoint receives the signed agent and its device certificate.
2. The agent establishes mutual-TLS outbound connectivity to the console.
3. An authorised operator prepares a campaign; a separate authorised role approves execution.
4. The console issues a short-lived, endpoint-bound task.
5. The agent records progress locally, resumes safely after restart, and returns signed audit events.
6. The console validates completion and retains evidence for support and rollback.

## Explicit exclusions

- No credential recovery, LSA secret collection or password migration.
- No inbound management channel from the console to endpoints.
- No production migration before laboratory verification and pilot approval.
