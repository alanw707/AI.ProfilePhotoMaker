---
title: 'Fix SEO mobile overflow, role hero image alignment, and homepage style count'
slug: 'fix-seo-mobile-overflow-role-hero-images-and-home-style-count'
created: '2026-02-14T20:09:43-08:00'
status: 'Implementation Complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['TypeScript', 'Angular 19 standalone components', 'Sass (.sass indented syntax)', 'RxJS 7', 'Angular Router', 'Karma/Jasmine', 'Playwright']
files_to_modify: ['AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass', 'AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts', 'AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html']
code_patterns: ['Data-driven SEO pages via seoPages route data', 'Single shared SEO component template+sass for many routes', 'Mobile-first breakpoint overrides in component Sass', 'Landing styles generated in TS and rendered in template slice', 'Style preview URLs provided by StylePreviewService naming conventions']
test_patterns: ['Unit tests use Jasmine/Karma .spec.ts files under src/app', 'No dedicated spec currently for seo-page or landing style-grid count', 'Manual responsive validation required for marketing route regressions', 'Playwright available for e2e/mobile validation']
---

# Tech-Spec: Fix SEO mobile overflow, role hero image alignment, and homepage style count

**Created:** 2026-02-14T20:09:43-08:00

## Overview

### Problem Statement

New SEO routes that use `SeoPageComponent` overflow horizontally on mobile due to fixed-width hero panel elements. In addition, role pages currently use generic hero images instead of role-appropriate style previews from the homepage style set (for example, doctor should use a medical style preview). On the homepage, the styles section claims 20+ styles but currently renders only 12 cards.

### Solution

Apply mobile-only responsive width fixes in the SEO page stylesheet so hero media/cards are fluid at small breakpoints while preserving desktop/tablet behavior. Update SEO role hero image mappings to use homepage style-preview images (`doctor-headshots` -> `medical`, `lawyer-headshots` -> `executive`). Update homepage styles grid rendering to display all 20 available style cards.

### Scope

**In Scope:**
- Fix mobile overflow in SEO pages powered by `src/app/pages/marketing/seo-page/seo-page.component.*`.
- Keep desktop/tablet visuals unchanged.
- Remove/override fixed width constraints causing overflow on mobile.
- Update role page hero image mapping in `src/app/pages/marketing/seo-pages.data.ts` to style-preview assets (`medical`, `executive`).
- Update landing styles section to render all 20 styles instead of 12.

**Out of Scope:**
- Global overflow masking/hacks (`overflow-x` on body/page as primary fix).
- Redesign of desktop/tablet layout or SEO page information architecture.
- Broader copy/content rewrite unrelated to this issue.

## Context for Development

### Codebase Patterns

- SEO routes are configured in `src/app/app.routes.ts` and load `SeoPageComponent` with `seoPage` route data.
- SEO page layout/styling is centralized in `src/app/pages/marketing/seo-page/seo-page.component.html` and `.sass`.
- Hero image metadata is data-driven via `src/app/pages/marketing/seo-pages.data.ts` (`hero.imageSrc`, `hero.imageAlt`).
- Homepage style previews come from `StylePreviewService` (direct `/style-previews/{style}.jpg` URL generation and cache) and are rendered from `styledPhotos` in landing page.
- Landing page currently generates up to 20 style cards in TS but template slices display to 12.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass` | Mobile overflow root cause and responsive fixes |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html` | Hero panel/card structure affected by width constraints |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts` | Role page hero image and alt mappings |
| `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html` | Styles grid rendering count (currently capped at 12) |
| `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts` | Source for up to 20 style items and style metadata |
| `AI.ProfilePhotoMaker.UI/src/app/services/style-preview.service.ts` | Style preview URL naming/availability (`medical`, `executive`) |

### Technical Decisions

- Apply fix at component level (SEO page styles), not global overflow masking.
- Mobile-only width behavior changes; desktop/tablet layout parity preserved.
- Role-specific hero mapping uses existing homepage style preview assets:
  - `doctor-headshots` -> `medical`
  - `lawyer-headshots` -> `executive`
- Homepage styles section should display all 20 generated styles.

## Implementation Plan

### Tasks

- [x] Task 1: Remove mobile overflow constraints in SEO hero panel
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass`
  - Action: At mobile breakpoints, override fixed hero widths (`width: 360px`, `max-width: 360px`, `min-width: 260px`) to fluid sizing that cannot exceed container width.
  - Notes: Keep desktop/tablet rules unchanged; scope changes to mobile media queries only.
- [x] Task 2: Preserve mobile hero readability while making CTA/media blocks fluid
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass`
  - Action: Ensure `.hero-panel`, `.hero-panel-stack`, `.hero-image-card`, `.panel-card`, and CTA row/button widths stack cleanly and do not trigger horizontal expansion at ~360px viewport.
  - Notes: Avoid global overflow hacks; fix root sizing behavior in component layout.
- [x] Task 3: Align role hero images with homepage style preview assets
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts`
  - Action: Update hero image mapping for role pages:
  - Action: `doctor-headshots` hero image source to medical style preview URL.
  - Action: `lawyer-headshots` hero image source to executive style preview URL.
  - Notes: Update matching alt text for role intent (provider/attorney) and keep SEO-safe wording.
- [x] Task 4: Render all 20 style cards on homepage
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html`
  - Action: Replace the current `styledPhotos.slice(0, 12)` loop with rendering that displays all 20 generated styles.
  - Notes: Keep existing card interaction/accessibility behavior unchanged.
- [x] Task 5: Validate route-level behavior and guard against regression
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass`
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts`
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html`
  - Action: Run targeted local verification on `/doctor-headshots`, `/lawyer-headshots`, and homepage styles section at mobile width.
  - Notes: Confirm no side-scroll and role-image correctness without altering desktop/tablet layout.

### Acceptance Criteria

- [x] AC 1: Given any SEO route using `SeoPageComponent` on a 360px-wide viewport, when the page fully renders, then no horizontal scroll is present and no content extends off-screen right.
- [x] AC 2: Given `/doctor-headshots`, when the hero section loads, then hero image source resolves to the medical style preview asset and alt text reflects a provider/medical context.
- [x] AC 3: Given `/lawyer-headshots`, when the hero section loads, then hero image source resolves to the executive style preview asset and alt text reflects an attorney/legal context.
- [x] AC 4: Given the homepage styles section, when active styles are loaded successfully, then 20 style cards are visible in the grid instead of 12.
- [x] AC 5: Given fallback style data is used, when the homepage styles section renders, then 20 fallback style cards remain visible and interactive.
- [x] AC 6: Given desktop/tablet breakpoints (>= 961px), when SEO pages render, then visual layout behavior remains consistent with pre-fix behavior aside from unchanged content mappings.

## Additional Context

### Dependencies

- `StylePreviewService` URL conventions and availability for style keys (`medical`, `executive`) in the style-previews container.
- Existing `seoPages` schema (`hero.imageSrc`, `hero.imageAlt`) consumed by `SeoPageComponent`.
- Existing responsive token/breakpoint usage in component-level Sass.

### Testing Strategy

- Manual responsive verification at `360x740` and one adjacent mobile size (for example `390x844`) on:
- Manual responsive verification: `/doctor-headshots`
- Manual responsive verification: `/lawyer-headshots`
- Manual responsive verification: one additional SEO route using same component.
- Confirm no horizontal scroll (`document.documentElement.scrollWidth === clientWidth`) after page load on tested routes.
- Confirm doctor/lawyer hero image `src` resolves to medical/executive style preview URL and alt text semantics are correct.
- Confirm homepage styles grid renders 20 cards in normal load path and fallback path.
- Optional confidence check: run Playwright mobile smoke for overflow and card-count assertions if test harness is already available.

### Notes

- User explicitly rejected global overflow masking; fix must target root layout constraints.
- User confirmed style mapping: `doctor-headshots -> medical`, `lawyer-headshots -> executive`.
- Highest regression risk is unintended desktop/tablet layout drift from mobile Sass changes; limit overrides to mobile media blocks.
- If style preview keys ever change in backend/storage naming, hero image mappings must be updated in `seo-pages.data.ts`.
