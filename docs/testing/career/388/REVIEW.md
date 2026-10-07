# #388 Roadmap tracking and replans — review

Branch `career/388-roadmap-tracking` off `feature/career-workspace`. Design: ADR 0017; contract: `docs/career/api-roadmap-tracking.md`.

## Gates
- API `dotnet test` (excluding Performance): 1116 passed, 1 skipped, 0 failed. The Release build with `-warnaserror` is clean.
- EF: "No changes have been made to the model since the last migration". `AddCareerRoadmapTracking` only adds tables.
- UI: `lint:errors-only` reports 0 errors; Karma 688; `build:mvp-v1` succeeds. Playwright (tracking + roadmap): 22 passed, including 0 axe WCAG 2.2 AA violations at 1280/390/320 and no overflow at 320.

## Acceptance criteria → evidence
| Criterion | Evidence |
|---|---|
| Status/effort/output edits with concurrency and owner validation | Task `If-Match` (428/412; two writers → second 412); another user's material → 404; deleted material → `linkedMaterialMissing` |
| Preserve done, evidence-bearing and human tasks across replans | `ReplanDiffTests` + API tests: preserved list with reasons; never in `changes` |
| Versioned diff, apply only accepted changes | Field-level before/after (effort, day, dependencies) with rationale; partial apply sends only checked ids (Playwright) and applies only those (API); reject keeps the version |
| Reject cycles; explain blocked tasks; completing a task doesn't touch pay | 409 `CareerRoadmapCycle`; "Waiting on: <titles>"; test asserts pay analyses and goal version unchanged |
| Contextual help; recovery across reload | Per-task help panel; selected task and drafts restored from sessionStorage (Playwright) |
| Simultaneous edits, partial/reject, missing material, faithful restoration | All covered above; progress carried over to the applied version |

## Review findings (spec + standards)
| # | Sev | Finding | Status |
|---|---|---|---|
| R1 | — | UI/API contract check: diff field names (`effort`, `milestoneDay`, `dependencies`) and task etags match | OK |
| R2 | P3 | No career-materials table exists yet, so `linkedMaterialId` validates against the caller's resume documents | Accepted; swap when a materials table lands |
| R3 | P3 | The replan DTO adds `status` (open/applied/rejected), which is not in the contract | Accepted; additive and needed to read closed replans |
| R4 | P3 | The InMemory test DB doesn't enforce the unique index on a first concurrent insert; on SQL Server the lost race maps to 412 | Accepted |

Open P0/P1: **none**.
