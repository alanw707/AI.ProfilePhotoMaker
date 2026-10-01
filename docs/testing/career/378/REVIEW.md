# #378 simulated UX review (Impeccable audit)

**This is a simulated review by an agent using the Impeccable `audit` checklist. It is not target-user evidence** and does not satisfy #377's independent-observation gate.

## How it was produced

- Real stack, no mocks: `scripts/career-localdev-api.sh start` (LocalDev API, in-memory DB, career flag on) + `npm run dev:local` (UI on :4200).
- `AXE_PATH=<axe.min.js> PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/chromium node tests/ux/career-ux-review.mjs ../docs/testing/career/378` from `AI.ProfilePhotoMaker.UI/`.
- The harness registers a synthetic user, walks empty home → setup step 1 (empty submit, then fill) → step 2 → saved home → reload → profile edit (goal goes stale) → version history, and records screenshots at 1280, 390 and 320 px plus `checks.json` (axe-core 4.13 WCAG 2.0/2.1/2.2 A+AA, horizontal overflow, targets < 44 px, one `h1`, page title, keyboard focus order with visible-focus check).
- Engine: Chromium 152 headless, emulated viewports. Touch gestures were not exercised (no custom gesture surfaces on these pages).

## Audit health score

| # | Dimension | Score | Key finding |
|---|---|---|---|
| 1 | Accessibility | 3 | Final run: 0 axe violations on 24 page/viewport captures; labels, `aria-invalid`, `aria-describedby`, focused `role=alert` summary, `aria-live` status |
| 2 | Performance | 4 | Lazy-loaded standalone routes; no animation; no images |
| 3 | Responsive | 4 | 0 px horizontal overflow at 320 px; all targets ≥ 44 px in the final run |
| 4 | Theming | 2 | Hard-coded hex values in `career.scss` instead of DESIGN.md tokens (P2) |
| 5 | Implementation integrity | 3 | Honest copy (manual facts only, no pay estimates/AI claims); global style collisions found and scoped |
| **Total** | | **16/20** | **Good** |

## Findings and status

| Sev | Finding | Evidence | Status |
|---|---|---|---|
| P1 | Links failed WCAG AA contrast (global `a:not(.btn):not(.nav-link)` in `styles.sass` recoloured them light blue) | axe `color-contrast` on every page in first run | **Fixed**: scoped `.career-page a` rule, underlined |
| P1 | Error summary rendered at 10 px with low-contrast red (collided with the global `.error-summary` from dashboard file-preview styles) | computed style `10px rgb(220,38,38)` | **Fixed**: renamed `.career-error-summary`, 16–17 px, dark red on tint |
| P1 | Error summary listed raw keys (`currentTitle`, `confirmed`) | screenshot `03-setup-validation` (first run) | **Fixed**: human labels; Playwright asserts no raw key |
| P2 | Saved work arrangement showed an empty select instead of "Not specified" when null | screenshot `06-…-mobile-320` (first run) | **Fixed**: null mapped to `''` |
| P2 | Page titles lacked the product suffix used elsewhere | `checks.json` titles | **Fixed** |
| P2 | Error-summary links 32 px tall | touch-target check | **Fixed**: 44 px |
| P2 | Version detail `h3` larger than section `h2` | screenshot `07-…-desktop` | **Fixed**: h3 19 px |
| P2 | Colours hard-coded rather than DESIGN.md tokens | `career.scss` | Logged: move to tokens when the career shell is styled (#393) |
| P2 | "Needs review" stale notice didn't say how to clear it | screenshot `06` | **Fixed**: "Check it, confirm, and save to clear this." |
| P2 | Goal history was not viewable/restorable (code review) | — | **Fixed**: goal history section with view/restore; screenshot `08-goal-history` |
| P3 | Goal confirmation checkbox stays checked after a successful save | screenshot `08` | Logged |
| P3 | Setup has no visible step progress beyond the heading ("Step 1 · …") | screenshots `02`, `04` | Logged |

No open P0/P1 findings.

## Verified behaviours (final run)

- Empty submit focuses the alert summary (`validation focus: alert`) and marks the title `aria-invalid`.
- Reload after setup restores the saved title, industry, goal and provenance line from the API.
- Editing the profile marks the goal "Needs review".
- Restoring goal version 1 announces "Goal version restored." in the status region.
- Tab order follows visual order and every focused control shows a visible ring.
