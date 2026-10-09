# UX/UI critique: aiprofilephotomaker.com (2026-10-08)

Scope: 30 routes (11 public, 19 signed in) × 1280/390 × light/dark = 120 views.
Capture: `/tmp/crit.mjs` (fresh registered user with profile + goal, no occupation), axe-core per view.
Screenshots: `shots/before/` (baseline, before fixes) and `shots/after/` (after #437), 120 each. Detector baseline: `baseline/detector-before.json`.

## Gate results
| Check | Before | After |
|---|---|---|
| axe violations (120 views) | 50 | 0 |
| Horizontal overflow at 390 | 0 | 0 |
| 5xx during capture | 0 | 0 |

## Findings by page
Severity: P0 blocks use/unreadable, P1 major, P2 moderate, P3 minor. Status: **Fixed** (commit), **Deferred** (reason), **Not reproduced**, **Rejected** (reason).

### Global / tokens
- [P1] Contrast failures on accent links, nav, breadcrumbs, danger text, placeholders: **Fixed** at token level (`--accent-text`, `--on-primary`, danger tokens), `5a73380e`.
- [P3] Floating theme toggle over content in full-page shots (materials, settings, enhance): **Not reproduced**. In a real 390px viewport the app header has the toggle inline; only marketing/auth pages have the fixed bottom-left toggle, and it does not cover content at rest.

### Home
- [P0] Career section (reported bug) dark-theme button invisible: **Fixed** `cc87ba52` (with a Playwright contrast check).
- [P2] Very long mobile page: **Deferred**. Content/marketing decision, not a defect.

### Pricing
- [P0] "No packages available": **Rejected**. Local DB has no seed; production returns 3 packages.
- [P2] Emoji feature icons, feature cards before the offer: **Deferred** (P2). Marketing pages keep the incumbent look until migrated (DESIGN.md).

### Examples / compare / LinkedIn / how-it-works / blog
- [P0] "Blank sections": **Rejected**. Scroll-reveal content not triggered in unscrolled full-page captures; text present (verified in a browser).

### Privacy policy (legal)
- [P2] Solid band behind each H2 over the translucent card: **Fixed** `fe711662` (shared `.legal-prose`).
- [P3] No table of contents: **Deferred**. Long-form legal nav is out of scope for a contrast/UX pass.

### Login / register
- [P2] Age checkbox ~12px: **Fixed** 20px, `17e46517`.
- [P3] "Privacy Policy ." stray space (register): **Fixed** `2e93d70a`.
- [P2] Sign-in button looks disabled: **Rejected**. It is disabled until age is confirmed, and the adjacent note says so; extra hint duplicated it.

### Photo workspace (/app/enhance)
- [—] No issues found beyond the global toggle note.

### Gallery
- [P2] Two stacked empty states ("No images yet" + "No Photos Yet"): **Fixed** `df52e313`, test `369f06fc` (fails on old code).

### Settings / support
- [P3] Long stat cards on mobile: **Deferred**. Layout redesign of settings, not a defect.

### Career home
- [P2] "Goal saved" headline before occupation: **Fixed**; shows target role, `687bcd30` (API + Karma tests).
- [P3] Group locked rail steps under one label: **Deferred**. The per-step text label is clearer for screen readers.

### Career rail
- [P2] Locked steps looked open: **Fixed**, "Needs occupation" status, `49a19bdf`.

### Career pay / market / markets / jobs / resume / roadmap
- [P1] Locked callout was a small link with no reason: **Fixed**, explains why plus a primary button, `6755d6ff`.
- [P3] Pay intro with spaced hyphens: **Fixed** `586877ed`.
- [P2] Technical vocabulary on the unlocked pay page (input hash, gates): **Deferred**. Needs a product decision on what moves behind "Technical details".

### Career materials
- [P3] Duplicate "Create a new photo" heading/link: **Fixed**; it's a secondary button, `17492a6a`.

### Career privacy
- [P3] "account settings ." / "OpenAI : …" stray spaces: **Fixed** `687bcd30`.

### Post-deploy live recheck (found only on production; fixed in PR #440, `0dd8f425`)
Regression spec: `AI.ProfilePhotoMaker.UI/tests/live-contrast-regressions.spec.ts` (fails on old code).
- [P1] Pricing bonus highlight and training badge light green on tint (1.6:1): **Fixed**, new `--success-text` token.
- [P1] Homepage showcase "After" label white on teal (1.86:1): **Fixed**, `--on-primary` ink.
- [P1] Testimonial verified badge: green text plus opacity pulse down to 0.5 (1.59:1): **Fixed**, `--success-text`, pulse removed.
- [P1] Cookie banner title dark on gray-900 in light theme (1.08:1): **Fixed**, white title on the always-dark banner.
- Live axe color-contrast after deploy: 0 on /, /pricing, /legal/privacy, /auth/register at 1280/390, light/dark.

### Adversarial review follow-up (404 page added to scope)
- [P1] 404 "See Example Headshots" (a.btn-primary without .btn) light-grey on teal in dark (1.51:1): **Fixed**; the dark link rule excludes primary buttons.
- [P2] Legal-prose links and global light links #3b82f6 (~3.1:1): **Fixed**; they use `--accent-text` and `--link-color`.
- [P2] SEO related-link cards inherited link blue (3.1:1): **Fixed**; they use body ink.
- [P2] Cookie banner link on the always-dark banner: **Fixed**; light ink.
- [P3] Gallery load failure showed "No Photos Yet": **Fixed**; error with Try again.
- [P3] Register age checkbox lacked an error link: **Fixed** (aria-invalid, aria-describedby, role=alert).
- [P2] Contrast gate now enforced in CI: `tests/axe-contrast.spec.ts` (11 public routes + 8 signed-in app routes incl. career, 2 themes, 2 widths, mocked API).
- [P1] 404 card text near-invisible in app-dark on OS-light devices (Tailwind `dark:` followed the OS, 1.52:1): **Fixed**; `darkMode` selector `[data-theme="dark"]` in `tailwind.config.js`.
- [P2] Career error-summary heading forced bright on its light field in dark (found by the signed-in axe gate): **Fixed** in `career.scss`.

### Populated-state contrast gate
- `tests/axe-contrast-populated.spec.ts`: 9 populated signed-in pages (gallery with photos, career home, profile, materials, roadmap, pay, market brief, market comparison, jobs), in 2 themes at 2 widths, using shared fixtures in `tests/fixtures/`.
- [P1] Gallery "generated" badge: white text on #4fd1c7 measured 1.86:1. **Fixed**: deep teal background (#0f766e).
- [P1] Market comparison heatmap tiles in dark mode: light text on light tiles measured 1.06–2.9:1. **Fixed**: each tile sets its own text colour (`--tile-ink`).
- [P2] Header "Get started" in dark mode: #e2e8f0 on teal measured 4.43:1. **Fixed**: white text.

## Gate and deploy record
| Gate | Result |
|---|---|
| `npm run build:mvp-v1` | Application bundle generation complete (2026-10-08 17:16Z) |
| Karma `ng test` | TOTAL: 743 SUCCESS |
| Playwright full suite | 324 passed, 1 skipped |
| API `dotnet test` | 1335 passed; 1 timing perf test flaky under load, passes in isolation |
| Simple Deploy #437 (`d2404b21`) | run 37810319556 success |
| Simple Deploy #440 (`0dd8f425`) | run 37816164769 success |
| Static Analysis | passes after ImageSharp advisory suppression (#438, tracked #439) |

## Limits
- Delegated design reviews lost most screenshots (payload guard); remaining pages were reviewed directly by the implementer, so this is not independent review.
