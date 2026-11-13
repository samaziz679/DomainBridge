# TDD evidence — operations console redesign

## Scope

User journey: an IT operator can understand the current mode, the next safe step and the available maintenance policy within the first screen, without being presented with an execution control.

## RED / GREEN evidence

| Stage | Commit | Evidence |
| --- | --- | --- |
| RED | `782a6fc` | The safe operator-guidance elements were absent from the console. |
| GREEN | current branch | Console tests validate the visible mode, review link and absence of execution buttons. |

## Guarantees

- The operating mode is an accessible `status` landmark labelled `Current operating mode`.
- The main action is a read-only link to the maintenance catalogue, not an execution button.
- The safe workflow explains review before approval.
- Desktop and 375 px mobile renders were manually inspected; no console errors were present.

## Validation

- `npm test -- --run` — 5 passed.
- `npm run build` — passed.
- Browser QA: desktop and 375×812 mobile screenshots checked; no runtime console errors.

## Visual baseline

No committed screenshot baseline exists, so visual-regression comparison is inconclusive. The manual review found no visible overflow in the tested viewports.
