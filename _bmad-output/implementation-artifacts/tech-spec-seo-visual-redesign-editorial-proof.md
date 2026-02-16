---
title: 'SEO Page Visual Redesign - Editorial Proof Layout'
slug: 'seo-visual-redesign-editorial-proof'
created: '2026-02-15'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Angular 19', 'SASS', 'Tailwind CSS', 'TypeScript']
files_to_modify:
  - 'AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass'
  - 'AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html'
  - 'AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1a.data.ts'
  - 'AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1b.data.ts'
  - 'AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.ts'
code_patterns: ['mobile-first responsive', 'clamp() fluid typography', 'aspect-ratio image containment', 'data-driven section rendering']
test_patterns: ['seo-metadata-smoke.spec.ts', 'seo-pages.data.spec.ts', 'seo-compare-static.spec.js']
---

# Tech-Spec: SEO Page Visual Redesign - Editorial Proof Layout

**Created:** 2026-02-15

## Overview

### Problem Statement

The 19 SEO pages use generic AI-template aesthetics - glassmorphism card grids, ambient glow orbs, uniform hover-lift cards, left accent bars - that users recognize instantly as undesigned. The layout is text-heavy and leads with paragraphs instead of visual proof. This causes high bounce rates and low CTA engagement. Additionally, the CSS uses desktop-first `max-width` breakpoints with no refinement below 640px, causing overflow issues on 320px-375px devices.

### Solution

Redesign the SEO page component with an editorial, image-led, mobile-first layout. Replace glassmorphism grids with asymmetric sections, photography-first visual hierarchy, fluid typography, and distinctive editorial design. Reduce text density, lead with visual proof (before/after images), and limit CTAs to strategic placements. All CSS written mobile-first with `min-width` breakpoints. All existing SEO mechanics (h1, meta, JSON-LD, canonical, static prerender) preserved exactly.

### Scope

**In Scope:**

- Full SASS rewrite of `seo-page.component.sass` - mobile-first, editorial layout, no glassmorphism
- Template restructure of `seo-page.component.html` - new section layouts, image containment, CTA cleanup
- Fluid typography with `clamp()` throughout
- Image containment: `aspect-ratio` + `object-fit` + percentage widths, `width`/`height` attributes on `<img>`
- Content restructure of 3 priority pages: `ai-headshot-generator`, `linkedin-headshots`, `realtor-headshots`
- New section type in data model if needed (`stat-strip` or similar)
- Dark/light theme compatibility
- `prefers-reduced-motion` support
- Guidelines doc for remaining 16 pages

**Out of Scope:**

- Landing page (`LandingComponent`) changes
- Marketing header/footer component changes
- New Angular services or API integrations
- Content rewriting for all 19 pages (spec covers pattern + 3 exemplars)
- Static prerender script (`generate-seo-static-pages.cjs`) changes
- Shared style system (`_redesign-system.sass`, `_mixins.sass`) overhaul
- Route configuration changes

## Context for Development

### Codebase Patterns

**Single component, data-driven architecture:** All 19 SEO pages share one `SeoPageComponent`. Content is defined statically in `seo-pages.data.ts` and passed via route `data` property. The component uses a discriminated union (`SeoSection`) with 7 type guards to render section types via `ngSwitch`. Any visual change affects all pages simultaneously.

**Current responsive approach (BROKEN):** Lines 26-31 use mobile-first `min-width` for `main` padding, but lines 794+ use desktop-first `max-width: 960px` and `max-width: 640px`. No breakpoint below 640px. Font sizes are all fixed `rem` with no `clamp()`. Hero image container is hardcoded at `width: 360px`.

**Glassmorphism dependency:** Every card type uses `shared.glass-card` mixin (backdrop-filter blur + border + hover glow pseudo-element + translateY lift). The CTA uses `shared.glass-strong`. Removing these means writing replacement styles - clean borders, subtle shadows, no blur.

**Static prerender is independent:** The `generate-seo-static-pages.cjs` script only injects `<h1>` + `<p>` into static HTML. It does NOT replicate the Angular template. Template/CSS changes do not require script updates.

**Test safety:** All 3 test files (`seo-metadata-smoke.spec.ts`, `seo-pages.data.spec.ts`, `seo-compare-static.spec.js`) test metadata only - not visual layout. CSS/template changes will NOT break them. Only data field changes to `h1`, `title`, `description`, `slug`, or hero image configs would break tests.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass` | Full SASS styles (930 lines) - REWRITE TARGET |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html` | Template (263 lines) - RESTRUCTURE TARGET |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts` | Barrel re-export (3 lines) - exports types + seoPages record |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.types.ts` | Type definitions (SeoPageContent, SeoSection union, etc.) - MODIFY if new section types |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.data.ts` | Aggregator - spreads part1a/1b/1c/part2 into single seoPages record |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1a.data.ts` | Pages: how-it-works, examples, reviews, free-headshot-enhancer, **ai-headshot-generator**, **linkedin-headshots** (581 lines) - CONTENT UPDATE |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1b.data.ts` | Pages: professional-headshots, headshots-for-job-search, **realtor-headshots** (324 lines) - CONTENT UPDATE |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1c.data.ts` | Pages: lawyer-headshots, doctor-headshots, compare/aragon-ai, compare/headshotpro, features (519 lines) - READ ONLY |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part2.data.ts` | Pages: dating-app, real-estate-agent, medical-professional, pricing, help (389 lines) - READ ONLY |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.ts` | Component logic (288 lines) - type guards if new section types |
| `AI.ProfilePhotoMaker.UI/src/app/shared/styles/_redesign-system.sass` | Design system mixins/vars (621 lines) - READ ONLY reference |
| `AI.ProfilePhotoMaker.UI/src/app/shared/styles/_mixins.sass` | Core mixins (423 lines) - READ ONLY reference |
| `AI.ProfilePhotoMaker.UI/src/styles.sass` | Global theme vars (794 lines) - READ ONLY reference |
| `AI.ProfilePhotoMaker.UI/src/app/shared/directives/animate-on-scroll.directive.ts` | Scroll animation directive - KEEP USING |
| `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html` | Landing page for design reference |
| `AI.ProfilePhotoMaker.UI/scripts/generate-seo-static-pages.cjs` | Static prerender - verify not broken |
| `AI.ProfilePhotoMaker.UI/tests/seo-metadata-smoke.spec.ts` | E2E metadata tests - must pass |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.spec.ts` | Unit tests - must pass |
| `tests/e2e/seo-compare-static.spec.js` | Compare page tests - must pass |

### Technical Decisions

1. **Mobile-first CSS only.** All base styles target 320px+. Breakpoints at `min-width: 480px`, `768px`, `1024px`, `1280px`. No `max-width` queries.

2. **Fluid typography via `clamp()`.** H1: `clamp(1.75rem, 5vw, 3rem)`. H2: `clamp(1.5rem, 3.5vw, 2rem)`. No fixed `font-size` on any heading.

3. **No glassmorphism on section cards.** Replace `backdrop-filter` + blur + glass-border with clean `border: 1px solid var(--border-color)` + subtle `box-shadow`. Keep `glass-strong` mixin ONLY for marketing header (not our file).

4. **No hover-lift/glow.** Remove `translateY(-6px)` hover + glow pseudo-elements from all cards. Use subtle `box-shadow` transition or border-color change on hover instead.

5. **Image containment via `aspect-ratio`.** All image containers use `aspect-ratio` + `object-fit: cover` + `width: 100%`. Add explicit `width`/`height` HTML attributes for CLS prevention.

6. **Keep `appAnimateOnScroll` directive.** The scroll-triggered entrance animations are fine - they use `IntersectionObserver` efficiently and respect `prefers-reduced-motion`. We just need cleaner entrance effects (fade-in only, no translateY).

7. **Keep existing shared mixins for buttons.** `shared.btn-primary` and `shared.btn-secondary` stay. `shared.page-container` and `shared.content-container` stay. Only glass mixins removed from section cards.

8. **Data model: prefer existing types.** Avoid adding new section types if possible. Use existing `highlights` for stat strips. If a new type is truly needed, add it minimally.

## Implementation Plan

### Tasks

#### Phase 1: Mobile-First SASS Foundation

- [ ] **Task 1.1: Rewrite responsive architecture to mobile-first**
  - File: `seo-page.component.sass`
  - Action: Replace all `@media (max-width: ...)` with `@media (min-width: ...)`. Define breakpoints: `$bp-sm: 480px`, `$bp-md: 768px`, `$bp-lg: 1024px`, `$bp-xl: 1280px`. Base styles (no media query) = 320px mobile layout. `.content-container` padding: `padding-inline: clamp(16px, 4vw, 24px)`. All grids default to `grid-template-columns: 1fr`.

- [ ] **Task 1.2: Implement fluid typography with clamp()**
  - File: `seo-page.component.sass`
  - Action: `h1`: `clamp(1.75rem, 5vw, 3rem)`. `h2`: `clamp(1.5rem, 3.5vw, 2rem)`. `h3`: `clamp(1.1rem, 2.5vw, 1.25rem)`. `.subhead`: `clamp(0.95rem, 2vw, 1.1rem)`. `.eyebrow`: `0.8rem` (fixed). Add `overflow-wrap: break-word` on all headings.

- [ ] **Task 1.3: Implement image containment system**
  - Files: `seo-page.component.sass` + `seo-page.component.html`
  - Action: Hero image container: `width: 100%; max-width: min(360px, 100%)`. All showcase images: parent with `aspect-ratio` + `overflow: hidden`, img with `width: 100%; height: 100%; object-fit: cover`. Add `width`/`height` attributes to all `<img>` (hero: `360x450`, showcase: `300x375`). Remove fixed `width: 360px` and `min-width: 260px`.

- [ ] **Task 1.4: Rewrite grid system mobile-first**
  - File: `seo-page.component.sass`
  - Action: `.hero-grid`: base `1fr`, `min-width: 1024px` -> `1.2fr 0.8fr`. `.steps-grid`: base `1fr`, `480px` -> `repeat(2, 1fr)`, `768px` -> `repeat(3, 1fr)`. `.cards-grid`: base `1fr`, `480px` -> `repeat(2, 1fr)`. `.showcase-grid`: base `1fr`, `768px` -> `repeat(auto-fit, minmax(280px, 1fr))`. `.testimonials-grid`: same pattern. `.related-grid`: base `1fr`, `480px` -> `repeat(2, 1fr)`. `.showcase-visual`: base `1fr`, `480px` -> `repeat(2, 1fr)`.

#### Phase 2: Editorial Layout Redesign

- [ ] **Task 2.1: Redesign hero section - remove glassmorphism and glow orbs**
  - File: `seo-page.component.sass`
  - Action: Remove `&::before`/`&::after` ambient glow orbs and `@keyframes orb-float-gentle`. Hero bg: `var(--bg-primary)` with optional CSS noise texture. Mobile: single column, image on top. Desktop: asymmetric `1.2fr 0.8fr`. `.panel-card`: remove glass-card, replace with clean stat strip (`display: flex; gap: 24px; border-bottom: 1px solid var(--border-color)`). `.hero-image-card`: remove glass-card, use `border-radius: 16px; overflow: hidden; box-shadow: var(--depth-1)`. CTA row: max 2 buttons.

- [ ] **Task 2.2: Overhaul all section layouts - remove glassmorphism**
  - File: `seo-page.component.sass`
  - Action: Remove `.section-header::before` accent bar. Remove `&:nth-of-type(even)` alternating bg. Remove `&:nth-child(3n)::before` purple orb. For each card type: `.step-card` -> clean card with numbered circle. `.card` -> clean border + shadow, subtle hover border-color change. `.bullet-grid li` -> simple list with checkmark prefix. `.showcase-item` -> clean card, prominent images. `.testimonial-card` -> blockquote with left border accent. `.comparison-table` -> clean table, stripe rows. `.faq-item` -> border-bottom separator. `.related-card` -> simple link row with hover underline.

- [ ] **Task 2.3: Overhaul CTA strategy - remove per-section CTAs**
  - Files: `seo-page.component.html` + `seo-page.component.sass`
  - Action: Remove `.section-cta` div from HTML (per-section "Try enhancer"). Bottom `.cta-section`: remove glass-strong, use clean full-width band. Remove `@keyframes cta-glow-pulse`. Mobile CTA buttons: `width: 100%; min-height: 48px`.

- [ ] **Task 2.4: Apply editorial visual texture and color restraint**
  - File: `seo-page.component.sass`
  - Action: Hero: solid `var(--bg-primary)` or subtle gradient, optional CSS noise via SVG data URI. Section bgs: generous `padding-block` whitespace, no gradient alternation. Teal accent only on CTAs, stat values, active/focus. All styles use CSS variables for dark/light theme.

#### Phase 3: Template Updates

- [ ] **Task 3.1: Restructure HTML for mobile-first DOM order and CTA cleanup**
  - File: `seo-page.component.html`
  - Action: Hero: reorder image-card before copy in DOM, use CSS `order` to flip on desktop. Remove "Try enhancer" from hero CTA row. Remove per-section "Try enhancer" CTA block. Add `width`/`height` to hero `<img>` (`360x450`). Add `width`/`height` to showcase `<img>` (`300x375`). Testimonials: conditional class for featured-quote layout when 1-2 items.

- [ ] **Task 3.2: Evaluate and implement stat-strip using existing highlights data**
  - Files: `seo-page.component.html` + `seo-page.component.sass`
  - Action: Review if existing `highlights` data on `SeoHero` is sufficient for stat strips (likely YES - 12 of 19 pages already have highlights). Style highlights as clean inline stat strip. Only add new section type to `seo-pages.types.ts` + component if truly needed.

#### Phase 4: Content Restructure (3 Priority Pages)

- [ ] **Task 4.1: Restructure `ai-headshot-generator` page for visual-proof-first**
  - File: `seo-pages.records.part1a.data.ts` (starts at line 383)
  - Action: Add `imageSrc` to hero (user provides asset). Add `showcase` section with 1-2 before/after pairs. Reorder sections: showcase -> cards -> steps -> faq. Shorten card descriptions to single lines.

- [ ] **Task 4.2: Restructure `linkedin-headshots` page with hero image and showcase**
  - File: `seo-pages.records.part1a.data.ts` (starts at line 486)
  - Action: Add `imageSrc` to hero (user provides asset). Add `highlights` (currently missing). Add `showcase` section with LinkedIn before/after. Merge "Ideal framing" + "Common mistakes" cards into one section. Reorder: showcase -> merged cards -> faq.

- [ ] **Task 4.3: Reorder `realtor-headshots` page to lead with showcase**
  - File: `seo-pages.records.part1b.data.ts` (starts at line 179)
  - Action: Move `showcase` section from position 3 to position 1 (immediately after hero). Keep remaining order. Shorten bullet descriptions.

#### Phase 5: Quality Assurance

- [ ] **Task 5.1: Viewport overflow audit across all 19 pages**
  - Action: Test all 19 pages at 320px, 375px, 414px, 768px, 1024px, 1440px. Verify zero horizontal scrollbar, all images contained, all text readable, all CTAs tappable (44x44px min).

- [ ] **Task 5.2: Run all existing test suites**
  - Action: Run `npx playwright test tests/seo-metadata-smoke.spec.ts`. Run unit tests for `seo-pages.data.spec.ts`. Run `npx playwright test tests/e2e/seo-compare-static.spec.js`. All must pass with zero failures.

- [ ] **Task 5.3: Build and static generation verification**
  - Action: Run `ng build` - must complete with zero errors. Run `node scripts/generate-seo-static-pages.cjs` - must complete with zero errors. Spot-check 2-3 generated static HTML files for correct structure.

### Acceptance Criteria

#### AC-1: Mobile-First Responsive Foundation
```
GIVEN any SEO page rendered at 320px viewport width
WHEN the page loads and is scrolled fully
THEN no content overflows horizontally (no horizontal scrollbar)
AND all text is readable without horizontal scrolling
AND all images fit within the viewport
AND all CTA buttons are tappable with minimum 44x44px touch targets
```

#### AC-2: Fluid Typography
```
GIVEN any SEO page
WHEN the viewport resizes from 320px to 1440px
THEN h1 font size scales fluidly between 28px and 48px (using clamp)
AND h2 font size scales fluidly between 24px and 32px
AND no heading text overflows its container at any width
AND long single-word headings wrap with overflow-wrap: break-word
```

#### AC-3: Image Containment
```
GIVEN any SEO page with hero images or showcase images
WHEN viewed at any viewport width from 320px to 1440px
THEN all images stay within their container bounds
AND all images maintain aspect ratio (no stretching/squashing)
AND all <img> elements have explicit width and height attributes
AND no image causes layout shift during loading (CLS < 0.1)
```

#### AC-4: Hero Section Redesign
```
GIVEN any SEO page hero section
WHEN viewed on mobile (< 768px)
THEN the layout is single-column stacked
AND there is maximum 1 primary CTA button + 1 secondary
AND highlights/stats appear as a compact inline strip

WHEN viewed on desktop (>= 1024px)
THEN the layout is asymmetric (roughly 55/45 or 60/40 copy/visual split)
AND no glassmorphism blur effects are visible
AND no animated glow orbs are present
```

#### AC-5: Section Layout - No Glassmorphism
```
GIVEN any SEO page section (cards, steps, bullets, showcase, testimonials, comparison, faq)
WHEN rendered at any viewport
THEN no backdrop-filter or -webkit-backdrop-filter is applied to section cards
AND no hover-glow pseudo-elements (::after with blur) exist on cards
AND no translateY(-6px) hover-lift exists on cards
AND sections use clean borders/shadows instead of glassmorphism
```

#### AC-6: CTA Strategy
```
GIVEN any SEO page (except free-headshot-enhancer)
WHEN the page is rendered
THEN the "Try enhancer" per-section CTA is NOT present
AND there are maximum 3 CTAs on the page: hero, mid-page (optional), bottom
AND on mobile, CTA buttons are full-width with min-height 48px
```

#### AC-7: Editorial Visual Language
```
GIVEN any SEO page section header
WHEN rendered at any viewport
THEN no left accent bar (::before gradient bar) is present
AND section headers use typography hierarchy (size/weight) for emphasis

GIVEN alternating page sections
WHEN rendered
THEN sections do NOT use --subtle-gradient alternating backgrounds
AND visual rhythm is created through whitespace and content variation
```

#### AC-8: Showcase/Before-After Sections
```
GIVEN an SEO page with a showcase section (examples, realtor, lawyer, doctor)
WHEN viewed on mobile (< 480px)
THEN before/after images stack vertically within each showcase item
AND images use aspect-ratio + object-fit: cover
AND no content overflows the viewport

WHEN viewed on desktop (>= 768px)
THEN before/after pairs display side-by-side with clear visual comparison
```

#### AC-9: Dark/Light Theme Compatibility
```
GIVEN any SEO page
WHEN the theme is toggled between light and dark
THEN all text remains readable with sufficient contrast
AND backgrounds, borders, and shadows adapt correctly
AND no hardcoded color values break the theme switch
```

#### AC-10: SEO Mechanics Preserved
```
GIVEN any SEO page
WHEN rendered in the browser
THEN the page has exactly one <h1> matching the data h1 field
AND <meta name="description"> matches the data description field
AND <link rel="canonical"> points to the correct URL
AND JSON-LD WebPage structured data is present with correct values
AND JSON-LD FAQPage structured data is present when FAQ sections exist

GIVEN the static page generation script runs
WHEN it processes all pages
THEN it produces valid HTML files with correct metadata for all 19 pages
```

#### AC-11: Existing Tests Pass
```
GIVEN the implementation is complete
WHEN seo-metadata-smoke.spec.ts runs THEN all tests pass
WHEN seo-pages.data.spec.ts runs THEN all tests pass
WHEN seo-compare-static.spec.js runs THEN all tests pass
WHEN ng build runs THEN it completes with zero errors
```

#### AC-12: Content Restructure (3 Priority Pages)
```
GIVEN the ai-headshot-generator page
WHEN rendered
THEN a visual proof section (showcase/before-after) appears before text-only sections
AND the section order leads with visual evidence

GIVEN the linkedin-headshots page
WHEN rendered
THEN a hero image is present
AND the page has a before/after showcase section

GIVEN the realtor-headshots page
WHEN rendered
THEN the showcase (before/after) section appears as the first section after hero
```

#### AC-13: Performance
```
GIVEN any SEO page
WHEN measured
THEN no backdrop-filter is applied to more than 2 elements per page
AND scroll animations use transform/opacity only (GPU-accelerated)
AND prefers-reduced-motion is respected (all animations disabled)
```

## Additional Context

### Dependencies

- User to provide new after-image assets for `ai-headshot-generator` and `linkedin-headshots` hero images
- Existing before/after image sets at `/assets/marketing/before-after/` (3 sets: set-1, set-2, set-3)
- CDN-hosted role style previews at Azure Blob Storage (executive.jpg, medical.jpg)

### Testing Strategy

1. **Automated tests (existing):** Run all 3 test suites - they validate SEO metadata is intact
2. **Manual viewport audit:** Test every page at 320px, 375px, 414px, 768px, 1024px, 1440px for overflow
3. **Build verification:** `ng build` + static page generation must succeed
4. **Theme verification:** Toggle dark/light on each priority page, verify readability
5. **Reduced motion:** Enable `prefers-reduced-motion: reduce` in browser, verify no animations fire

### Notes

- **DATA FILE SPLIT (completed).** `seo-pages.data.ts` has been refactored into a barrel export + split record files to stay under the 1500-line CI lint limit. The structure is now: `seo-pages.data.ts` (barrel) -> `seo-pages.types.ts` (types) -> `seo-pages.records.data.ts` (aggregator) -> `part1a` (581 lines: how-it-works through linkedin-headshots), `part1b` (324 lines: professional through realtor), `part1c` (519 lines: lawyer through features), `part2` (389 lines: dating through help). Our 3 priority pages are in `part1a` (ai-headshot-generator, linkedin-headshots) and `part1b` (realtor-headshots). Keep each file under 1500 lines when adding content.
- The `pricing` entry in `seoPages` is an orphan - it exists in the data file but `/pricing` route uses `PremiumComponent`, not `SeoPageComponent`. Do not modify this entry.
- Compare pages (`compare/aragon-ai`, `compare/headshotpro`) use nested child routes with slug containing `/`. Canonical URLs are correctly generated.
- The `free-headshot-enhancer` page has special behavior: no "Try enhancer" CTA shown (it IS the enhancer page). This conditional logic in the template (`*ngIf="currentPage.slug !== 'free-headshot-enhancer'"`) should be removed as part of CTA cleanup since we're removing per-section enhancer CTAs entirely.
- 16 of 19 pages have NO hero image and NO showcase sections. These pages will benefit most from the editorial layout changes (cleaner card layouts, better typography, whitespace) but won't have dramatic visual proof sections until images are added later.
