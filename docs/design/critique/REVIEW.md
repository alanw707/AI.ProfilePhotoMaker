# UX/UI critique: aiprofilephotomaker.com (2026-10-08)

Scope: 30 routes (11 public, 19 signed in) × 1280/390 × light/dark = 120 views.
Capture: `/tmp/crit.mjs` (fresh registered user with profile + goal, no occupation), axe-core per view.
Screenshots: `shots/after/` (current branch). Detector baseline: `baseline/detector-before.json`.

## Gate results
| Check | Before | After |
|---|---|---|
| axe violations (120 views) | 50 | 0 |
| Horizontal overflow at 390 | 0 | 0 |
| 5xx during capture | 0 | 0 |

## Findings by page
Status: **Fixed** (commit), **Deferred** (reason), **Not reproduced**, **Rejected** (reason).

### Global / tokens
- Contrast failures on accent links, nav, breadcrumbs, danger text, placeholders: **Fixed** at token level (`--accent-text`, `--on-primary`, danger tokens), `5a73380e`.
- Floating theme toggle over content in full-page shots (materials, settings, enhance): **Not reproduced**. In a real 390px viewport the app header has the toggle inline; only marketing/auth pages have the fixed bottom-left toggle, and it does not cover content at rest.

### Home
- Career section dark-theme button invisible: **Fixed** `cc87ba52` (with a Playwright contrast check).
- Very long mobile page: **Deferred** (P2). Content/marketing decision, not a defect.

### Pricing
- "No packages available": **Rejected**. Local DB has no seed; production returns 3 packages.
- Emoji feature icons, feature cards before the offer: **Deferred** (P2). Marketing pages keep the incumbent look until migrated (DESIGN.md).

### Examples / compare / LinkedIn / how-it-works / blog
- "Blank sections": **Rejected**. Scroll-reveal content not triggered in unscrolled full-page captures; text present (verified in a browser).

### Privacy policy (legal)
- Solid band behind each H2 over the translucent card: **Fixed** `fe711662` (shared `.legal-prose`).
- No table of contents: **Deferred** (P3).

### Login / register
- Age checkbox ~12px: **Fixed** 20px, `17e46517`.
- "Privacy Policy ." stray space (register): **Fixed** `2e93d70a`.
- Sign-in button looks disabled: **Rejected**. It is disabled until age is confirmed, and the adjacent note says so; extra hint duplicated it.

### Photo workspace (/app/enhance)
- No issues found beyond the global toggle note.

### Gallery
- Two stacked empty states ("No images yet" + "No Photos Yet"): **Fixed** `df52e313`, test `369f06fc` (fails on old code).

### Settings / support
- Long stat cards on mobile: **Deferred** (P3).

### Career home
- "Goal saved" headline before occupation: **Fixed**; shows target role, `687bcd30` (API + Karma tests).
- Group locked rail steps under one label: **Deferred** (P3). The per-step text label is clearer for screen readers.

### Career rail
- Locked steps looked open: **Fixed**, "Needs occupation" status, `49a19bdf`.

### Career pay / market / markets / jobs / resume / roadmap
- Locked callout was a small link with no reason: **Fixed**, explains why plus a primary button, `6755d6ff`.
- Pay intro with spaced hyphens: **Fixed** `586877ed`.
- Technical vocabulary on the unlocked pay page (input hash, gates): **Deferred** (P2). Needs a product decision on what moves behind "Technical details".

### Career materials
- Duplicate "Create a new photo" heading/link: **Fixed**; it's a secondary button, `17492a6a`.

### Career privacy
- "account settings ." / "OpenAI : …" stray spaces: **Fixed** `687bcd30`.

## Limits
- Delegated design reviews lost most screenshots (payload guard); remaining pages were reviewed directly by the implementer, so this is not independent review.
