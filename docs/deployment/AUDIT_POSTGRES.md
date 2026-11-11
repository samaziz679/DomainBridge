# PostgreSQL durable audit

## Configuration

Outside Development and Testing, DomainBridge refuses to start unless the Audit connection string is configured. Do not store the password in appsettings or source control. Supply it through the selected secret manager, workload identity, or runtime environment.

Apply deployment/postgres/001_audit_events.sql using a database-administration identity. The runtime role receives INSERT only; it must not receive UPDATE, DELETE or TRUNCATE on audit_events.

## Data boundary

The durable audit table stores only an event UUID, UTC timestamp, action and subject identifier. It does not store an HTTP request body, certificate, user profile, password, cache, LSA material or token.

## Operations

- Require encrypted PostgreSQL transport and server certificate validation.
- Back up the database under the organisation retention policy.
- Forward database or API audit events to the approved SIEM using a separately reviewed integration.
- Give operators read access through a dedicated read model, not the runtime write role.
