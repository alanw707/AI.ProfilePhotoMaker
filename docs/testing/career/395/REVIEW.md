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

## Gates (after fixes, verified by coordinator)
API 1302 passed / 1 skipped; Release -warnaserror clean; EF no pending changes (migrations additive). UI lint 0; Karma 723; build:mvp-v1 ok; Playwright usage + journey 21 passed, 0 axe at 1280/390/320. Independent review: openai-codex/gpt-5.5.

## Owner gate (not a code defect)
Policy values (6 runs/min, 2 concurrent, 200 global queue, $50/month global cost cap; monthly allowance unchanged) are **provisional** until the owner approves the operating budget. Cost/latency distributions are reported by the admin page from real usage; representative journey measurement happens in beta.

## Audit follow-up (test hygiene + visual check)
| id | sev | finding | status |
|---|---|---|---|
| A1 | P2 | `PerformanceReportGenerator` lacked a `Category=Performance` trait (hardcoded path), so `Category!=Performance` ran it | fixed: trait added |
| A2 | P2 | `ResumeHardeningTests` 2 s wall-clock budget flaked under load (4.6 s) | fixed: 10 s budget, still far below an unbudgeted decompression bomb; 3 isolated runs 21/21 |
| A3 | P2 | Admin controls showed "Pause drafting — On." next to an unchecked box | fixed: off-state text "New drafts are running." / "External job sources are on."; Playwright asserts it |

Gates: `dotnet test --filter "Category!=Performance"` 1315 passed / 1 skipped; `--filter "FullyQualifiedName!~Performance"` 1302 passed / 1 skipped (the difference is the Performance namespace's untagged load tests). UI lint 0, Karma 723, build:mvp-v1 ok, Playwright usage+journey 21. Screenshots: `admin-usage-desktop.png`, `allowance-home-desktop.png` (reviewed).
