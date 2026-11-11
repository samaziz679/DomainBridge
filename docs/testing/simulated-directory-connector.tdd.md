# TDD evidence — simulated directory connector

| Stage | Result |
|---|---|
| RED | The test project failed to compile because the simulated connector did not exist. |
| GREEN | Three tests passed with 100 percent line and branch coverage. |

The connector maps a synthetic account from domain A to domain B, requires distinct domains and rejects an absent account name. It has no external directory side effects.
