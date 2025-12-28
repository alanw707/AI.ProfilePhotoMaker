# Tech-Spec: Landing Pricing + Compliance Copy Alignment + Testimonials

**Created:** 2025-12-25
**Status:** Ready for Development

## Overview

### Problem Statement

The landing page pricing grid and legal/FAQ copy contain mismatches with current product behavior (e.g., showing AI-enhanced photo counts instead of headshot-style generations, mentions of subscriptions or free trial language). Contact emails are spread across multiple inboxes. Testimonials use emoji avatars instead of real photo examples.

### Solution

Align landing pricing to the packages pricing grid metrics (headshot style generations per credit package, training availability, one-time purchase language). Update compliance/policy/FAQ copy to remove subscription/trial references, and route all contact email addresses to `support@aiprofilephotomaker.com`. Refresh testimonial cards to use existing example headshots (style preview images) while keeping the overall theme consistent.

### Scope (In/Out)

In scope:
- Landing pricing copy and feature mapping to display headshot-style generation counts and one-time purchase language.
- Landing testimonials to use real photo examples (style preview images) instead of emoji icons, with a light visual refresh.
- FAQ verbiage on landing to remove trial/subscription references and align with current offering.
- Legal/compliance pages: update copy to remove subscription references, and replace all contact emails with support.

Out of scope:
- Backend pricing logic, credit cost definitions, or billing flows.
- New assets or commissioned photography.
- Large-scale redesigns outside the testimonials/pricing section.

## Context for Development

### Codebase Patterns

- Angular standalone components; landing uses `landing.component.html` + `landing.component.sass` while legal pages embed templates in TS.
- Landing pricing cards are driven by `plans` mapped from credit packages.
- Packages pricing page uses `CreditPackagesComponent`, which already computes style generations via `getStyledGenerations` (credits / 5) and training eligibility (>= 15 credits).
- Style preview images are served via `StylePreviewService` with direct Azure Blob URLs.

### Files to Reference

- Landing: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts`
- Landing template: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html`
- Landing styles: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.sass`
- Packages pricing (reference): `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.ts`
- Packages pricing template (reference): `AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.html`
- Refund Policy: `AI.ProfilePhotoMaker.UI/src/app/pages/refund-policy/refund-policy.component.ts`
- Terms: `AI.ProfilePhotoMaker.UI/src/app/pages/terms/terms.component.ts`
- Privacy: `AI.ProfilePhotoMaker.UI/src/app/pages/privacy/privacy.component.ts`
- Retention Policy: `AI.ProfilePhotoMaker.UI/src/app/pages/retention-policy/retention-policy.component.ts`
- Cookies: `AI.ProfilePhotoMaker.UI/src/app/pages/cookies/cookies.component.ts`
- AI Transparency: `AI.ProfilePhotoMaker.UI/src/app/pages/ai-transparency/ai-transparency.component.ts`
- Acceptable Use: `AI.ProfilePhotoMaker.UI/src/app/pages/acceptable-use/acceptable-use.component.ts`
- IP/DMCA: `AI.ProfilePhotoMaker.UI/src/app/pages/ip-dmca/ip-dmca.component.ts`
- Security: `AI.ProfilePhotoMaker.UI/src/app/pages/security/security.component.ts`
- Children Privacy: `AI.ProfilePhotoMaker.UI/src/app/pages/children-privacy/children-privacy.component.ts`
- Biometric Consent: `AI.ProfilePhotoMaker.UI/src/app/pages/biometric-consent/biometric-consent.component.ts`
- Settings copy (subscription wording): `AI.ProfilePhotoMaker.UI/src/app/pages/settings/settings.component.html`

### Technical Decisions

- Compute headshot-style generation counts on landing using `CreditService.getCreditCostSync('styled_generation')` to avoid hardcoding costs; use `Math.floor(totalCredits / styledGenerationCost)`.
- Replace landing plan feature lines that say "AI-enhanced photos" with "headshot style photos" (mirroring the packages grid).
- Align pricing section copy to one-time purchases (remove "cancel anytime" copy).
- Testimonials will use `StylePreviewService.getCachedUrl(styleName)` for images (existing style previews), with a fallback state if an image fails.
- Replace all contact emails in legal/FAQ content with `support@aiprofilephotomaker.com`.

## Implementation Plan

### Tasks

- [ ] Update landing pricing mapping (`LandingComponent.loadPackagesFromDatabase` and `getPackageFeatures`) to surface headshot-style generation counts and training availability; remove "AI-enhanced photos" wording.
- [ ] Adjust landing pricing section copy for one-time purchases (remove subscription-style language).
- [ ] Update landing FAQ entries to remove subscription or free-trial language, and ensure structured data reflects the updated FAQ content.
- [ ] Replace testimonial emoji avatars with real photo examples via `StylePreviewService` (add `imageUrl` to `Testimonial`, update HTML + SASS for image avatar).
- [ ] Update legal/compliance pages to use `support@aiprofilephotomaker.com` and remove subscription references (notably Refund Policy).
- [ ] Update any remaining subscription references in non-legal user-facing copy (e.g., settings copy).

### Acceptance Criteria

- [ ] Landing pricing grid shows headshot-style generation counts (e.g., "~X style photos") derived from package credits and styled generation cost.
- [ ] Landing pricing copy reflects one-time purchase (no "cancel anytime" or subscription language).
- [ ] No mentions of "free trial" or subscriptions in FAQ and legal pages, unless explicitly required.
- [ ] All legal/compliance contact emails resolve to `support@aiprofilephotomaker.com`.
- [ ] Testimonials display real example photos (style previews) with graceful fallback if an image fails.
- [ ] UI changes keep the existing theme and do not introduce off-brand visuals.

## Additional Context

### Dependencies

- Credit cost references from `CreditService` (styled generation cost).
- Style preview image URLs from `StylePreviewService`.

### Testing Strategy

- Manual UI verification on landing page (pricing, testimonials, FAQ accordion).
- Spot-check all legal/compliance pages for updated copy and contact email.
- Optional: run `npm run lint` if time allows.

### Notes

- Confirm whether the 14-day satisfaction guarantee remains valid; if not, adjust refund/FAQ copy accordingly.
- If any style preview URL fails, ensure the testimonial avatar degrades to a neutral placeholder (no emoji).
