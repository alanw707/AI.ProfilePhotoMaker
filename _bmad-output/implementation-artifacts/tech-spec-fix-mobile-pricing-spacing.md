---
title: 'Fix Mobile Spacing & Card Element Sizing on Pricing Payment Panel'
slug: 'fix-mobile-pricing-spacing'
created: '2026-02-19'
status: 'Completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['angular-19', 'sass', '@stripe/stripe-js', 'onpush-change-detection']
files_to_modify: ['AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.sass', 'AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.ts']
code_patterns: ['angular-component-sass', 'stripe-card-element-options', 'responsive-breakpoints-480-640-768-1024-1400', 'host-context-dark-theme', 'shared-mixins-content-container']
test_patterns: ['visual-viewport-testing']
---

# Tech-Spec: Fix Mobile Spacing & Card Element Sizing on Pricing Payment Panel

**Created:** 2026-02-19

## Overview

### Problem Statement

On mobile/tablet views of `/pricing`, the payment panel (Card Details + Billing Details) has two issues:

1. **Excessive horizontal margins** — Compounding padding from nested containers (`.credit-packages-container` → `.payment-container` → `.card-block`) eats ~88px of horizontal space on a 412px screen, making the form feel cramped with wasted screen real estate.
2. **Cramped Stripe card input** — The `CardElement` (card number / expiry / CCV in a single row) is too narrow and the numbers are crunched together, making it difficult to read and interact with.

Additionally, the `max-width: 520px` constraint at the 768px breakpoint unnecessarily restricts the panel width on tablets.

### Solution

Reduce nested padding at mobile breakpoints, relax the `max-width` constraint on tablet, and give the Stripe card element container more breathing room. Reduce `letterSpacing` in the Stripe CardElement JS options to reclaim horizontal space inside the iframe.

### Scope

**In Scope:**
- Fix `.payment-container` max-width at tablet (768px breakpoint)
- Reduce compounding horizontal padding on `.card-block` / `.billing-block` at mobile
- Reduce outer container side padding at narrow widths
- Increase Stripe card element container min-height and padding for readability
- Reduce CardElement `letterSpacing` from `0.4px` to `0.2px` in TS file

**Out of Scope:**
- Stripe console errors (`ERR_BLOCKED_BY_CLIENT`) — confirmed as ad-blocker noise, no action needed
- Desktop layout changes (≥1024px unchanged)
- Functional/logic changes to payment flow
- Switching from `CardElement` to split elements (`CardNumberElement` etc.)

## Context for Development

### Codebase Patterns

- Angular component with co-located `.sass` file using `@use '../../shared/styles/index' as shared`
- Stripe `CardElement` configured via `StripeCardElementOptions` in the `_initializeStripe` method (lines 327-430)
- Responsive breakpoints: 480px (small mobile), 640px (mobile), 768px (tablet), 1024px (desktop), 1400px (large desktop)
- Dark theme handled via `:host-context([data-theme="dark"])` selector with `!important` overrides
- Shared `content-container` mixin provides `max-width: 1200px`, `margin: 0 auto`, `padding: 24px`, `width: 100%`
- Component uses `ChangeDetectionStrategy.OnPush` with manual `_cdr.markForCheck()` / `_cdr.detectChanges()`

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.sass` | All payment form styling, responsive breakpoints (791 lines) |
| `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.ts` | Stripe CardElement options at lines 372-405 (fontSize, letterSpacing, lineHeight) |
| `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.html` | Template structure — card-block and billing-block inside `.payment-form` grid (no changes needed) |
| `AI.ProfilePhotoMaker.UI/src/app/shared/styles/_mixins.sass` | `content-container` mixin (line 147) — no changes needed |

### Technical Decisions

- **Keep `CardElement` (not split elements):** Switching to individual `CardNumberElement` / `CardExpiryElement` / `CardCvcElement` would be a larger refactor. The current combined element works well when given sufficient container width.
- **Reduce `letterSpacing` not `fontSize`:** The `fontSize: '16px'` is the minimum to prevent iOS Safari zoom-on-focus. Reducing it would cause UX issues. Instead, reduce `letterSpacing` from `0.4px` to `0.2px` to reclaim ~3-5px of horizontal space inside the Stripe iframe.
- **Bump `max-width` at 768px, not remove it:** We still want some centering on tablets; `640px` is a better cap than `520px`.

## Implementation Plan

### Tasks

- [x] Task 1: Relax `.payment-container` max-width at tablet breakpoint
  - File: `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.sass`
  - Action: At line 57, change `max-width: 520px` → `max-width: 640px` inside the `@media (max-width: 768px)` block
  - Notes: This gives the payment form ~120px more width on tablets. The ≤480px breakpoint already uses `max-width: 100%` so small phones are unaffected.

- [x] Task 2: Reduce `.payment-container` side padding at small mobile
  - File: `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.sass`
  - Action: At line 60, change `padding: 1.1rem 0.75rem` → `padding: 1rem 0.5rem` inside the `@media (max-width: 480px)` block
  - Notes: Saves 4px per side (0.25rem). Combined with Task 3, this materially reduces the horizontal dead space.

- [x] Task 3: Reduce `.card-block` / `.billing-block` side padding at small mobile
  - File: `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.sass`
  - Action: At line 136, change `padding: 1.1rem 1.25rem` → `padding: 1rem 0.85rem` inside the `@media (max-width: 480px)` block for `.card-block, .billing-block`
  - Notes: Saves 6.4px per side (0.4rem). This is the biggest win — the inner blocks had the most generous padding.

- [x] Task 4: Reduce outer container side padding at small mobile
  - File: `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.sass`
  - Action: At line 13, change `padding: 1.25rem 0.75rem` → `padding: 1.25rem 0.5rem` inside the `@media (max-width: 640px)` block for `.credit-packages-container`
  - Notes: Saves 4px per side. The outer container padding compounds with everything inside.

- [x] Task 5: Increase Stripe card element container size
  - File: `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.sass`
  - Action: At line 194, change `padding: 1rem 1.25rem` → `padding: 1.1rem 0.85rem` for `.stripe-card-container`. At line 206, change `min-height: 52px` → `min-height: 56px` for `#card-element`. Add a new mobile override inside `.stripe-card-container`: `@media (max-width: 480px)` with `padding: 1rem 0.65rem` to reduce side padding further on small screens.
  - Notes: Reduces side padding to give the Stripe iframe more usable width, while slightly increasing vertical height for readability. The iframe content will expand to fill available width automatically.

- [x] Task 6: Reduce CardElement letterSpacing in JS options
  - File: `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.ts`
  - Action: At line 381, change `letterSpacing: '0.4px'` → `letterSpacing: '0.2px'` inside the `cardElementOptions.style.base` object
  - Notes: Stripe renders card number, expiry, and CCV inside an iframe. `letterSpacing` applies to all text inside. Reducing by `0.2px` across ~20 characters saves ~4px, giving the fields more room to breathe. The visual difference is subtle but the spacing relief is meaningful on narrow screens.

### Acceptance Criteria

- [x] AC 1: Given a 412px mobile viewport on `/pricing` with a selected package, when the payment form is displayed, then the Card Details and Billing Details panels use visibly more of the screen width with less wasted margin on both sides compared to before the fix.

- [x] AC 2: Given a 412px mobile viewport, when card details are entered into the Stripe CardElement (number / expiry / CCV), then all three fields are readable without numbers overlapping or being cut off.

- [x] AC 3: Given a 768px tablet viewport on `/pricing` with a selected package, when the payment form is displayed, then the `.payment-container` is wider than before (up to 640px) and the form doesn't feel pinched.

- [x] AC 4: Given a 1024px+ desktop viewport on `/pricing`, when the payment form is displayed, then the layout is unchanged from the current behavior (no regression).

- [x] AC 5: Given dark theme is active on a mobile viewport, when the payment form is displayed, then all card/billing styling remains correct with no visual regressions (dark backgrounds, border colors, focus states).

- [x] AC 6: Given a mobile viewport, when the user focuses the Stripe CardElement, then the focused border/shadow styling still applies correctly.

## Additional Context

### Dependencies

- `@stripe/stripe-js` — CardElement styling is partially controlled by Stripe's iframe; we can only configure via `StripeCardElementOptions.style`. No version change needed.

### Testing Strategy

- **Visual testing (manual):** Test on mobile viewports (375px, 412px, 480px, 768px, 1024px) in Chrome/Edge DevTools
- **Theme testing:** Verify both light and dark themes at each viewport
- **Functional smoke test:** Complete a test payment (or simulation mode payment) after styling changes to ensure the Stripe integration is unaffected
- **No unit tests needed:** Changes are purely CSS + one Stripe style option constant

## Review Notes

- Adversarial review completed
- Findings: 12 total, 3 fixed, 9 skipped (noise/undecided)
- Resolution approach: auto-fix
- F-05 fixed: billing-block mobile padding bumped to `1rem` (standard value, restores breathing room vs input internal padding)
- F-06 fixed: payment-container ≤480px padding `0.5rem` → `0.65rem` (320px device clearance improved to ~18px/side)
- F-11 fixed: stripe-card-container mobile padding `0.65rem` → `0.75rem` (standard spacing scale restored)

### Notes

- **Stripe console errors are not a bug:** The `ERR_BLOCKED_BY_CLIENT` errors for `r.stripe.com` are caused by ad blockers blocking Stripe's telemetry/fraud beacons. This is standard behavior across all Stripe integrations and does not affect payment functionality. No code change needed.
- **iOS Safari zoom caveat:** `fontSize` must stay ≥16px in the CardElement options to prevent iOS Safari from auto-zooming on input focus. Do not reduce `fontSize` below 16px.
- **Future consideration:** If the card input still feels tight after these changes, the next step would be switching from the combined `CardElement` to individual elements (`CardNumberElement`, `CardExpiryElement`, `CardCvcElement`) — but that's a larger refactor out of scope for this fix.
