# Career journey live-stack review (#393)

Simulated review on LocalDev API (in-memory DB, fake text model) + `npm run dev:local`. No mocks, no production, no purchases. Not user evidence.
Run: `AXE_PATH=/tmp/axe/node_modules/axe-core/axe.min.js PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/chromium node tests/ux/career-journey-review.mjs ../docs/testing/career/393` -> 96 checks, 14/14 next-step steps ok, 0 page errors, 0 dialogs. Raw data: `checks.json`.

## Gates
After all fixes: API `dotnet test` (excl. Performance) 1276 passed, 1 skipped, 0 failed; Release `-warnaserror` clean; no migration. UI lint 0 errors; Karma 706; `build:mvp-v1` succeeds; Playwright journey + roadmap + pay 26 passed (0 axe violations at 1280/390/320). Independent review: openai-codex/gpt-5.5 (spec + standards).

## Steps
| Step | nextAction | Page landed | Result | Screenshot |
|---|---|---|---|---|
| 0 empty home | create_profile | /app/career/setup | ok | 00-empty-home-desktop.png |
| 1 profile saved | set_goal | /app/career/setup | ok | 01-profile-home-desktop.png |
| 2 goal set | confirm_occupation | /app/career/occupation | ok | 02-goal-home-desktop.png |
| 3 match proposed | confirm_occupation | /app/career/occupation | ok (15-1252.00 strong) | 03-occupation-home-desktop.png |
| 4 confirmed | build_brief | /app/career/market | ok, brief complete (Denver metro) | 04-brief-home-desktop.png |
| 5 brief done | analyze_pay | /app/career/pay | ok, benchmark + scenario; personalized unavailable (rights unverified), labelled not advertised pay | 05-pay-home-desktop.png |
| 6 pay done | build_roadmap | /app/career/roadmap | ok | 06-roadmap-home-desktop.png |
| 7 proposed | accept_roadmap | /app/career/roadmap | ok, accepted via UI | 07-accept-home-desktop.png |
| 8 accepted | draft_resume | /app/career/resume | ok | 08-resume-home-desktop.png |
| 9 resume drafted | export_material | /app/career/materials | ok; PDF `%PDF` 27936 B, DOCX `PK` 3128 B | 09-export-home-desktop.png |
| 10 summary | none | no Next step shown | ok | 10-after-summary-home-desktop.png |
| U1 unusual occupation | confirm_occupation | /app/career/occupation | unsupported + guidance, no fake match | U1-unusual-occupation-home-desktop.png |
| S1/S2 sparse (Hyder, AK) | build_brief / analyze_pay | market / pay | resolves to state (Alaska); local figures "not available (too few survey responses)"; scenario unavailable | S2b-sparse-pay-*.png |

Other checks: photo section renders without purchase (Continue without a photo + Create link present); XSS title shown as text, 0 injected elements, 0 dialogs; `?next=javascript:alert(1)` ignored (no link); reload mid-run recovers (home shows result, run completed); second user gets 404 on roadmap, resume, summary, versions, brief, pay, other's run, and export create; privacy JSON download ok. Axe (wcag2a/aa/21aa/22aa): 0 violations at 1280/390/320; no horizontal overflow; one h1 per page.

## Findings
| id | sev | finding | status |
|---|---|---|---|
| F1 | P3 | At 390/320 the pay-table "Source" buttons wrap mid-word ("Sourc e") in narrow columns (S2b-sparse-pay-mobile-390.png) | fixed: `.source-link` no longer breaks mid-word |
| F2 | P3 | nextAction stays `export_material` after files were downloaded (journey has no export signal); harmless, Next step still valid | open, reported |
| F3 | P3 | Sparse pay scenario says unavailable when the goal has no requested pay; wording does not say why | open, reported |
| F4 | info | Probe errors fixed in script only (profile needs real duties for a match; roadmap opened by id) - not app bugs | closed |
| SPEC-1 | P1 | export_material was satisfied by any export, even expired or of an older version | fixed: needs an unexpired export of the material's CurrentVersion; 3 API tests |
| SPEC-2 | P1 | Failed-run "Try again" on home dropped the run id | fixed: link carries `?run=<id>`; page shows the failed run and its existing retry; home issues no POST; Playwright + unit |
| STD-1 | P2 | Journey loaded every artifact in full | fixed: AsNoTracking projections, bounded to 200 per kind, export check limited to resume ids |
| SPEC-3 | P2 | Stale notices linked to the bare page | fixed: link carries the artifact's query param; Playwright + spec |
| TEST-1 | P2 | Review script exited 0 on failed checks | fixed: exits 1 on step failures, failed checks, page errors, dialogs, axe violations |
| TEST-2 | P2 | Sparse setup failure was silently skipped | fixed: recorded as a failure |
| TEST-3 | P3 | Mocked "Try again" test did not assert recovery context | fixed: Playwright asserts the run id, the failed state with retry, and no POST from home |

Open P0/P1: **none**
