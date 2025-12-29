# Tech-Spec: Headshot-Eligible Credit Display

**Created:** 2025-12-28
**Status:** Ready for Development

## Overview

### Problem Statement
The Headshot Studio dashboard shows total credits (weekly + purchased). Weekly/free credits cannot be used for headshot generation, so the displayed total misleads users into thinking they can generate headshots when they cannot.

### Solution
Use **purchased credits** as the sole eligibility signal for headshot generation. Update the Headshot Studio credit display to show purchased credits only and add inline copy clarifying that weekly/free credits cannot be used for headshots. Keep the existing credit system unchanged (no new credit status).

### Scope (In/Out)
**In scope**
- Update the Headshot Studio dashboard credit display to use purchased credits only.
- Add inline copy/tooltip clarifying that weekly/free credits cannot be used for headshots.
- Ensure the CTA reflects headshot eligibility (e.g., purchase credits when eligible is zero).

**Out of scope**
- Backend credit model changes.
- Global credit display changes (header, settings credit management) unless explicitly requested.
- Pricing or packaging changes.

## Context for Development

### Codebase Patterns
- Angular (standalone components), 2-space indent, single quotes.
- Credit display logic currently lives in `CreditDisplayComponent` with totals derived from weekly + purchased credits.
- Headshot Studio paywall already treats purchased credits as required in dashboard local calculation.

### Files to Reference
- `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/components/dashboard/credit-display/credit-display.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/components/dashboard/credit-display/credit-display.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/services/credit.service.ts`

### Technical Decisions
- Use purchased credits as the headshot-eligible amount (no new credit type/status).
- Prefer UI-only changes; use existing credit data (`purchasedCredits`) from `userCreditStatus`/`creditsInfo`.

## Implementation Plan

### Tasks
- [ ] Update `CreditDisplayComponent` to support a "Headshot Studio" display that uses purchased credits only.
- [ ] In `dashboard.component.ts`, pass purchased credits as the eligible amount for headshots.
- [ ] Update the dashboard credit display copy to clarify weekly/free credits are not eligible for headshots.
- [ ] Adjust any purchase prompt logic so it keys off purchased credits when in Headshot Studio.
- [ ] Update or add a small UI/unit test if existing coverage is present for credit display logic.

### Acceptance Criteria
- [ ] Headshot Studio dashboard shows "Eligible for headshots" count based on purchased credits only.
- [ ] Weekly/free credits are not included in the displayed headshot-eligible count.
- [ ] Inline copy clarifies eligibility at point of use.
- [ ] Purchase CTA appears when headshot-eligible credits are zero.
- [ ] No changes to backend APIs or other global credit displays unless explicitly requested.

## Additional Context

### Dependencies
- None (UI-only)

### Testing Strategy
- Manual: verify Headshot Studio dashboard for users with (a) weekly credits only, (b) purchased credits only, (c) both.
- Optional: add/update unit test for `CreditDisplayComponent` display mode if test harness exists.

### Notes
- Keep the data model unchanged; we are reusing purchased credits as the eligibility signal.
