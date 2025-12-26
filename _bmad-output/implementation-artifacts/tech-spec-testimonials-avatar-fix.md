# Tech-Spec: Landing Testimonials Avatar Fix

**Created:** 2025-12-25
**Status:** Completed

## Overview

### Problem Statement

The landing testimonials section has a missing image (one style preview 404s), avatar photos are visually too small, and one female name maps to a male photo. Star ratings are uniform and add no value.

### Solution

Update testimonial styles to known, existing preview images (Sophie -> edgy-urban, Grace -> glamour), increase avatar size for better visual balance, and replace the star row with a simple "Verified review" label. Keep the existing visual theme and layout.

### Scope (In/Out)

In scope:
- Update testimonial data to use the specified style previews.
- Increase avatar size and tune styling for better legibility.
- Replace star ratings with a "Verified review" label.

Out of scope:
- New assets or custom photography uploads.
- Changes to other landing sections or pricing/FAQ content.

## Context for Development

### Codebase Patterns

- Landing page uses `landing.component.ts` for data and `landing.component.html` + `landing.component.sass` for markup/styles.
- Testimonial images are resolved via `StylePreviewService.getCachedUrl(style)`.
- Missing images fall back to a generated SVG via `onImageError`.

### Files to Reference

- `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.sass`
- `AI.ProfilePhotoMaker.UI/src/app/services/style-preview.service.ts`

### Technical Decisions

- Replace Sophie Kline style `consultant` with `edgy-urban` (preview exists).
- Replace Grace Nolan style `entrepreneur` with `glamour` to align female name and photo.
- Remove `rating` from the `Testimonial` model and data and remove the star block from the template.
- Replace stars with a text label "Verified review" (optionally with a small check icon for visual affordance).
- Increase `.testimonial-avatar` to a larger size (target 72px desktop) with a responsive downshift on small screens.

## Implementation Plan

### Tasks

- [x] Update `Testimonial` interface and `initializeTestimonials()` data:
  - Remove `rating` from the interface and data entries.
  - Set Sophie Kline style to `edgy-urban`.
  - Set Grace Nolan style to `glamour`.
- [x] Update `landing.component.html`:
  - Remove the star ratings block.
  - Add a "Verified review" label (text-only or check icon + text).
- [x] Update `landing.component.sass`:
  - Increase `.testimonial-avatar` size (ex: 72px) and adjust border-radius/shadow if needed for the larger image.
  - Add a small responsive tweak to keep avatars balanced on mobile (ex: 64px).

### Acceptance Criteria

- [x] Testimonials show real photos for Sophie (edgy-urban) and Grace (glamour) with no placeholder.
- [x] Avatar photos are larger and clearer than the current 56px size.
- [x] Star ratings are removed and replaced by "Verified review".
- [x] Styling remains consistent with the current landing page theme.

## Additional Context

### Dependencies

- Style preview images must exist in the Azure blob container used by `StylePreviewService`.

### Testing Strategy

- Manual check of the landing testimonials section (desktop and mobile).
- Confirm no broken image loads for the updated styles.

### Notes

- If any style preview is missing, the fallback SVG will render; use only known existing styles.

## Review Notes

- Adversarial review completed
- Findings: 10 total, 6 fixed, 4 skipped
- Resolution approach: auto-fix
