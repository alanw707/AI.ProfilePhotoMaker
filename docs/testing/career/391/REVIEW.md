# #391 career photo handoff: live-stack review

Simulated review on the LocalDev stack (in-memory DB, stubbed providers, career flag on), not target-user evidence. Script: `AI.ProfilePhotoMaker.UI/tests/ux/career-photo-review.mjs`; raw results in `checks.json`; screenshots at desktop 1280, mobile 390 and 320.

LocalDev cannot produce finished photos (dummy provider keys), so only the populated photo list and the selection PUT were mocked for screenshots 02–04. The empty list, missing-asset select, validation, goal, workspace, sign-in redirect and credit checks all hit the real API. Cross-user rejection, watermarked-preview 409 and the "never spends" invariants are covered by `CareerPhotoHandoffTests` against the real service.

## Results

| Check | Result |
|---|---|
| Real `GET /api/career/photos` (new user) | 200, empty list, no private/raw path in body |
| Real select of a missing image / id 0 | 404 `CareerPhotoNotFound` / 400 |
| Credits before vs after list + select attempts | unchanged |
| Watermarked preview radio | disabled, explained in text |
| Choose + "Use this photo" | status "Photo saved to your career profile."; saved photo stays checked and marked "In use"; save disabled until a different photo is chosen |
| "Improve in photo workspace" | `/app/enhance?refineImageId=42&careerReturn=materials&careerGoal=<goal>` |
| Workspace banner | "You came from your career materials. Back to career materials" (label follows the key) |
| Back link | returns to `/app/career/materials?careerGoal=<goal>`, no goal-changed note |
| `careerReturn` = `https://evil.com`, `//evil.com`, `javascript:alert(1)`, `/app/career/materials` | no banner, no off-origin navigation |
| Goal changed while away | "Your career goal changed while you were in the photo workspace." |
| Expired session | `/auth/login?…&returnUrl=/app/career/materials` |
| Overflow at 1280/390/320 | 0 on every screen |
| axe (WCAG 2.2 AA) on career materials | 0 violations |
| Page errors | 0 |

## Findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | P2 | Workspace banner always said "career materials", even for the `profile`/`home` keys. | Fixed: resolver returns a label; spec covers all three. |
| 2 | P2 | After saving, the grid cleared the radio, so nothing showed which photo was in use and "Use this photo" re-saved the same photo. | Fixed: saved photo starts checked with "In use"; save only for a new choice (Karma spec). |
| 3 | P3 | Banner link tap target was 17px tall. | Fixed: ~42px. |
| 4 | P3 | "Use this photo" button touched the next heading. | Fixed: spacing. |
| 5 | P3 | Contract said `selectedPhotoAvailable` is false only for a lost photo; API also returns false when nothing is chosen. | Contract corrected; UI already keyed off `selectedPhotoId`. |
| 6 | Pre-existing, P3 | axe `label` on the workspace's hidden file input; `color-contrast` on the login page legal link. | Present on the base branch with and without the banner; out of scope, not introduced here. |
| 7 | Note | In screenshot 04 the workspace says "This photo is no longer available to refine" because image 42 is a mock id that does not exist in LocalDev. | Expected for the mocked review. |

## /code-review (Standards + Spec, PR #403)

| # | Axis | Severity | Finding | Status |
|---|---|---|---|---|
| R1 | Standards | P2 | Two first-time selects could race on the unique owner index and return 500. | Fixed: lost race (`IsLostRace`) retries as an update; `CareerPhotoSelectionRaceTests`. |
| R2 | Spec | P2 | Export/retention for `CareerPhotoSelection` not stated. | Owner cascade + deletion coverage confirmed; career export does not exist yet and is owned by #392 (recorded in ADR 0008). |
| R3 | Spec | P2 | No test proved career params survive the pricing round trip. | Extracted `workspaceReturnAfterPurchase` from the pricing page (behaviour unchanged) with a Karma spec covering career params and off-site return URLs. |
| R4 | Spec | P2 | `refineImageId` is dropped across checkout (pre-existing workspace behaviour). | Out of scope for this slice (workspace checkout-return must stay unchanged); tracked in #404. Career back link survives, so the user can return and pick "Improve" again. |
| R5 | Spec | P3 | Goal lookup unordered. | Not a bug: goals are unique per owner (unique index). |
| R6 | Standards | P3 | Magic strings, `ToLower()` in LINQ, `AlreadyExists` reused for 409, unused `profile`/`home` keys. | Accepted judgement calls: match existing repo habits; keys are documented allowlist entries. |

Open P0/P1: **none**.
