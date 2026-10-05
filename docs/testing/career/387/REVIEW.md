# #387 Career roadmap — review

Branch `career/387-roadmap` off `feature/career-workspace`. Design: ADR 0016; contract: `docs/career/api-roadmap.md`.

## Gates
- API `dotnet test` (excl. Performance): 1079 passed, 1 skipped, 0 failed. Release `-warnaserror` build is clean.
- EF `has-pending-model-changes`: "No changes have been made to the model since the last migration". Migration `AddCareerRoadmaps` only adds; its single `DropTable` is in `Down`.
- UI: `lint:errors-only` 0 errors; Karma 686 passed; `build:mvp-v1` succeeds. Playwright: roadmap + pay analysis 16 passed, with 0 axe WCAG 2.2 AA violations at 1280/390/320 and no overflow at 320.

## Acceptance criteria → evidence
| Criterion | Evidence |
|---|---|
| Options only when the evidence supports them; fewer is valid | `RoadmapBuilderTests` (thresholds at exactly +10 % and ±15 %, plus a no-alternative case); UI case with 1 option and the omitted explanation |
| Pinned versions, rationale, assumptions, missing evidence; scenario timeline | `pinned` block; rationale cites `oews`/`projections` with the release (API test asserts `$135,980`); "A scenario, not a promise" |
| This week + 30/60/90, editable effort, valid dependencies | Effort fitting and spill tests; effort edit writes a new version; cycles, self-cycles and unknown dependencies are rejected (409 `CareerRoadmapCycle`) |
| Explicit acceptance; no silent goal change or invented credentials/uplift/paid courses | Accept needs `If-Match` (428/412) and leaves the goal version unchanged (`goalUnchanged`); the UI requires a confirmation dialog (no POST before confirm); a forbidden-wording scan covers the templates and generated text |
| Private, versioned, reloadable; stale indicators; readable assumptions | Owner-scoped, cross-user 404 on every operation, deletion coverage; stale after a profile/goal change; `?roadmap=` reload |
| Low time, no alternative, ambiguous goal, cycles, sparse pay, accessible selection | 1 h/week `lowTimeNote`; no-alternative; 409 `CareerOccupationRequired`; cycle tests; missing pay analysis → missing evidence; radio fieldset + keyboard test + axe |

## Review findings (code review: spec + standards)
| # | Sev | Finding | Status |
|---|---|---|---|
| R1 | P3 | The stale banner said "profile or goal changed" even when the only reason was `market_brief_changed` | Fixed: copy names market evidence too |
| R2 | — | UI/API contract check: the source ids (`oews`, `projections` in the BLS snapshot), `no_supported_alternative`, and the `note`/`occupationLink` accept fields all match | OK |
| R3 | P3 | Version numbers use max+1 per owner and the index is not unique, so a rare concurrent run could duplicate a version number. Ids stay unique, so no data is lost | Accepted, documented |
| R4 | P3 | `steadier_transition` also requires duty evidence (stricter than the ADR wording), so it never invents a path | Accepted as the conservative reading |

Open P0/P1: **none**.

Not covered: accepting a non-closest option end-to-end against real data, because the snapshot does not reliably produce an alternative. The UI side is covered with a mock.
