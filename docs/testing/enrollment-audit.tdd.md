# TDD evidence — enrollment audit

## Scope

Accepted enrollment requests append an in-process audit event containing an event ID, UTC timestamp, action and device identifier. The store has no update or delete operation. It does not record request bodies, certificate values, credentials or secrets.

## Evidence

| Stage | Command | Result |
|---|---|---|
| RED | `dotnet test tests\\DomainBridge.Api.Tests\\DomainBridge.Api.Tests.csproj --no-restore` | Expected compile failure: `IAuditStore` did not exist. |
| GREEN | `dotnet test tests\\DomainBridge.Api.Tests\\DomainBridge.Api.Tests.csproj --no-restore --collect:"XPlat Code Coverage"` | 9/9 passing; 100% line and 83.3% branch coverage. |

## Limit

`InMemoryAuditStore` is a tested MVP seam, not a delivery-grade durable audit store. PostgreSQL persistence, retention, access control and SIEM forwarding remain separate operational work.
