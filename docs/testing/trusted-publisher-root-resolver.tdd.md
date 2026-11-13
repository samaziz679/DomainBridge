# TDD evidence — trusted publisher root resolver

The resolver selects only roots whose configured thumbprints are present in its certificate source and fails closed when a root is missing.

| Stage | Commit | Result |
| --- | --- | --- |
| RED | `bb9974b` | The root resolver did not exist. |
| GREEN | `ca255af` | Two focused tests passed. |

- `dotnet test C:\dbw\DomainBridge.sln --no-restore` — 82 passed.
