# #381 occupation matches: live-stack review

Simulated review on the LocalDev stack (in-memory DB, career flag on, real O*NET 30.0 snapshot), not target-user evidence. No API mocks. Synthetic, fictional profiles. Script: `AI.ProfilePhotoMaker.UI/tests/ux/career-occupation-review.mjs`; raw results in `checks.json`; screenshots at desktop 1280, mobile 390 and 320.

## Results

| Check | Result |
|---|---|
| Software duties under the title "Code Ninja" | 15-1252.00 Software Developers ranked first; title not matched, so duties decided it |
| Run steps | "Read your confirmed profile", "Read your career goal", "Compared your duties with occupation tasks", "Saved the matches for your review" |
| Allowance | not spent (used 0, reserved 0); no model needed |
| Evidence and attribution | "You wrote / Occupation task" pairs, missing evidence, known gaps, unsupported skills (e.g. "Phlebotomy"); O*NET CC BY 4.0 attribution shown |
| Another owner's match: GET / confirm / dismiss | 404 / 404 / 404; their goal unchanged |
| Confirm | new goal version 2, `occupation` 15-1252.00 (release 30.0), source `occupation_match`; career home shows it |
| Confirm again / without `If-Match` | 409 `CareerMatchNotConfirmable` / 409 (decided-check runs before the precondition) |
| Profile changed after the match | `profileChanged: true`, note shown, confirm disabled, API 409 `CareerMatchStale` |
| Contradictory duties (nursing + software) | run paused with choices: LPN/LVN, Software Developers, None of these; invalid answer 400; valid answer completes and shows "You told us" |
| Title only ("Software Developer") | unsupported with guidance; nothing to confirm |
| Expired session | `/auth/login?…&returnUrl=/app/career/occupation` |
| Percentages, overflow, axe WCAG 2.2 AA, page errors | none / 0 / 0 / 0 on every screen |

API fixtures (`OccupationMatcherTests`, `CareerOccupationMatchApiTests`) cover three families (software 15-1252.00 #1, registered nurse 29-1141.00 #1, medical secretary 43-6013.00 top 2), nonstandard titles, contradictory duties, gibberish, pay/location/arrangement not changing results, snapshot tampering (503) and owner deletion.

## Findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | P1 | Contradictory duties did not pause the run when one family filled the top two places (LPN then RN), so different careers could be mixed. | Fixed: ambiguity also compares the best rival family and duties it alone explains; choices are the leader of each family. Matcher version `duty-overlap-2`; regression test from the live profile. |
| 2 | P3 | Candidate code ran into the title ("Software Developers(15-1252.00)"). | Fixed. |

## /code-review (Standards + Spec)

| # | Axis | Severity | Finding | Status |
|---|---|---|---|---|
| R1 | Standards | P2 | A dismiss racing a confirm could mark a confirmed match `dismissed`. | Fixed: match `Status` is a concurrency token; a losing dismiss re-reads and answers 409. `CareerOccupationDecisionRaceTests`. |
| R2 | Spec | P2 | Snapshot licence and taxonomy were not validated on load. | Fixed: only `CC BY 4.0` + `O*NET-SOC 2019` with attribution, source URL and licence URL load; 6-row rejection test. |
| R3 | Spec | P2 | Retention not stated to the user. | Fixed: retention note under the matches (Playwright assertion); ADR 0010 updated. |
| R4 | Spec | P2 | Confirm check order (409 before 428) not documented. | Documented in the API contract. |
| R5 | Standards | P3 | Step output duplicates profile text from evidence. | Accepted: same owner, same deletion cascade and retention as the match. |
| R6 | Standards | P3 | Corrupt stored JSON would surface as 500; summary sentence split on `.`. | Accepted for this slice (rows are written only by the runner; split only lowers recall). |
| R7 | Spec | P3 | Export not yet available. | Owned by #392. |
| R8 | Standards | P3 | Runner growing; confirm duplicates goal-version copying; status strings. | Accepted judgement calls. |

Open P0/P1: **none**.
