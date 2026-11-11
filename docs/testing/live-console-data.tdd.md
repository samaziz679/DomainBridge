# Live console data — TDD evidence

Source: user journeys derived during this implementation.

| Guarantee | Test | RED evidence | GREEN evidence |
|---|---|---|---|
| The console overview exposes only fleet and audit metrics | `ConsoleReadEndpointTests.Overview_returns_read_only_fleet_and_audit_metrics` | endpoint returned 404 | API test suite passed: 27 tests |
| Technical inventory excludes profile and password data | `ConsoleReadEndpointTests.Agents_returns_technical_metadata_without_profile_data` | endpoint returned 404 | API test suite passed: 27 tests |
| Invalid read limits are rejected | `ConsoleReadEndpointTests.Audit_rejects_out_of_range_limits` | endpoint returned 404 | API test suite passed: 27 tests |
| Console data client maps API responses and fails closed | `api.test.ts` | module import failed | Vitest suite passed |
| Agents and audit evidence are shown in the UI | `App.test.tsx` | device row was absent | Vitest suite passed |

Coverage note: this repository does not expose a coverage script for the React console. The focused API and UI contracts above are executed in CI; E2E OIDC coverage requires a configured identity provider and is intentionally not simulated with production credentials.
