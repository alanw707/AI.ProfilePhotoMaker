# `/app/enhance` Accessibility Verification

Verified 2026-08-28 against the Studio Proof Desk implementation.

## WCAG AA contrast

Command:

```bash
node scripts/check-app-enhance-contrast.mjs
```

| Foreground / background | Ratio | WCAG AA normal text |
|---|---:|---|
| Production ink / proof paper | 14.08:1 | Pass |
| Muted copy / proof paper | 6.91:1 | Pass |
| White / proof cobalt | 6.47:1 | Pass |
| Deep cobalt / proof paper | 8.66:1 | Pass |
| Proof red / paper white | 7.06:1 | Pass |
| Completion green / paper white | 6.30:1 | Pass |

All normative route color pairs exceed 4.5:1. Status remains textual and never relies on color alone.

## Touch targets

`profile-workflow-flags-and-download.spec.ts` measures every visible:

- button;
- select;
- range input;
- consent label;
- export/retention label; and
- application-header link.

The assertion runs in setup and completed-workspace states at 360px, 390px, 768px, and 1280px. Every measured target must be at least 44×44 CSS pixels. Range controls have an explicit 44px minimum height; checkbox targets inherit a 44px label hit area.

## 200% zoom and reflow

Playwright’s `expect200PercentZoomReflow` models a 720px-wide display at 200% browser zoom as its effective 360 CSS-pixel viewport. It verifies:

- effective viewport = 360 CSS pixels;
- document width does not exceed the viewport;
- all visible measured controls retain 44×44 CSS-pixel targets; and
- the paid fulfillment action remains reachable.

This follows WCAG reflow behavior: browser zoom reduces the available CSS viewport, causing the same responsive layout used at the equivalent width.

## Screen reader and keyboard semantics

- The three workflow stages are an ordered list with `aria-current="step"`.
- Portrait style filters use `role="group"` and toggle buttons expose `aria-pressed`; they are not presented as ARIA tabs.
- Candidate choices are buttons with descriptive accessible names and `aria-pressed` selection state.
- Candidate and generation progress use native progressbar roles with numeric values and plain-language equivalents.
- Loading, blockers, export completion, and augmentation progress use live status regions.
- Primary actions, candidate proofs, style filters, native fields, and export choices remain keyboard-operable with visible cobalt focus indicators.
- Reduced-motion rules collapse nonessential transitions.

## Automated evidence

- Angular/Karma: focused component and state tests.
- Playwright: Chromium and Mobile Chrome end-to-end flow, touch-target audit, overflow checks, zoom-equivalent reflow, package interruption, refinement restoration, and export.
- Impeccable detector: `.impeccable/review/detector-report.json` and `.impeccable/review/detector-summary.md`.
