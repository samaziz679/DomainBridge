# PostgreSQL durable audit

## Configuration

Outside Development and Testing, DomainBridge refuses to start unless the Audit connection string is configured. Do not store the password in appsettings or source control. Supply it through the selected secret manager, workload identity, or runtime environment.

Apply `deployment/postgres/001_audit_events.sql` and `deployment/postgres/002_device_inventory.sql` using a database-administration identity. The runtime role receives only the minimum permissions: append/read audit events and read/upsert technical device inventory; it must not receive DELETE or TRUNCATE on either table.

## Data boundary

The durable tables store only an event UUID, UTC timestamp, action, device identifier, OS, architecture and agent version. They do not store an HTTP request body, certificate, user profile, password, cache, LSA material or token.

## Operations

- Require encrypted PostgreSQL transport and server certificate validation.
- Back up the database under the organisation retention policy.
- Forward database or API audit events to the approved SIEM using a separately reviewed integration.
- In production, `Console:EnableReadApi` is disabled by default. Enable it only after placing the console/API path behind the organisation's OIDC-enabled reverse proxy. The browser sends only its authenticated session (`credentials: include`); it never stores an API password or client certificate.
