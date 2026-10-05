# #395 review findings

## Review findings

| ID | Sev | Finding | Status |
| --- | --- | --- | --- |
| S1 | P1 | Global backpressure/cost checks raced across users | **Fixed**: `CareerOperatorState.GuardVersion` concurrency token bumped by every create (migration `CareerGlobalUsageGuard`); loser retries. SQLite parallel test, 10 users, cap 3 |
| S2 | P2 | Caps ignored the requested run's cost | **Fixed**: `current + estimate > cap` refuses (user and global); `TaskCostEstimatesUsd`, model-free = 0; cap-minus-one-cent tests |
| S3 | P2 | UI admin usage DTO differed from API | **Fixed**: `actions[].failures` (rate derived), `perActiveUser.*`; spec mocks updated; docs |
| S4 | P2 | 429 usage codes unmapped in UI | **Fixed**: `rateLimited`, `concurrencyLimit`, `userCostCap` plain copy; Karma tests |
| S5 | P3 | Allowance UX could use run DTO allowance | **Fixed**: summary page uses `GET /api/career/allowance` only; run DTO allowance documented as legacy |
| S6 | P3 | `updatedBy` missing in UI controls DTO | **Fixed**: added and shown on the admin page |

Open P0/P1: **none**
