# TDD evidence — agent inventory

## Scope

The agent offers a local `--inventory-once` command. It captures only device name, operating system, architecture, agent version and UTC timestamp. It does not access local accounts, user profiles, credentials or domain services.

## Evidence

| Stage | Command | Result |
|---|---|---|
| RED | `dotnet test tests\\DomainBridge.Agent.Tests\\DomainBridge.Agent.Tests.csproj` | Expected failure: the agent had no entry point and no inventory contract. |
| GREEN | `dotnet test tests\\DomainBridge.Agent.Tests\\DomainBridge.Agent.Tests.csproj --no-restore --collect:"XPlat Code Coverage"` | 5/5 passing; 100% line and branch coverage. |

## Tested guarantees

- Captured data includes technical device metadata and a non-blank agent version.
- The public snapshot contains no property whose name indicates user, profile, password or secret data.
- Blank agent versions are rejected.
- Only the exact `--inventory-once` argument is handled; unsupported arguments produce no inventory output.
