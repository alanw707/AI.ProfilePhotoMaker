---
title: 'Landing Page Image-Led Redesign'
slug: 'landing-page-image-led-redesign'
created: '2026-01-20T05:18:11-08:00'
status: 'done'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Angular (standalone components)', 'TypeScript', 'Sass', 'Tailwind utilities', 'Playwright (UI tests)']
files_to_modify: ['AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html', 'AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.sass', 'AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/shared/styles/_redesign-system.sass', 'AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/*']
code_patterns: ['Standalone Angular components with inline data arrays', 'Shared marketing header/footer components', 'Shared design system mixins via @use shared styles index', 'Image-heavy marketing layouts with responsive grids']
test_patterns: ['Playwright UI tests in AI.ProfilePhotoMaker.UI/tests (SEO metadata smoke)']
---

# Tech-Spec: Landing Page Image-Led Redesign

**Created:** 2026-01-20T05:18:11-08:00

## Overview

### Problem Statement

The current landing page redesign feels generic and text/grid heavy, with limited imagery or visual activity, making it look similar to other AI-generated landing pages and reducing visual differentiation.

### Solution

Redesign the landing page to be minimal and professional while shifting to an image-led layout (including before/after headshot imagery and richer visual anchors) and preserving existing sections where possible.

### Scope

**In Scope:**
- Full landing page visual redesign with minimal/pro tone
- Image-led hero and section treatments using before/after headshot imagery
- Visual hierarchy, layout, typography, and styling updates across existing sections
- Section reordering or small structural refinements as needed to support the new visual direction

**Out of Scope:**
- Backend or API changes
- Pricing logic or product functionality changes
- New features outside landing page visuals
- Content strategy beyond existing sections

## Context for Development

### Codebase Patterns

- Landing page is a standalone Angular component with template + Sass styling and data-driven arrays in the component class.
- Shared marketing header/footer are reused across landing and SEO pages.
- Shared design system mixins and variables are imported via `@use '../../shared/styles/index' as shared`.
- Tailwind utilities are layered in for layout helpers; custom animations live in component Sass.
- SEO pages use a structured, image-forward showcase pattern that includes before/after assets.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html | Landing page markup and section layout |
| AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.sass | Landing page styling and animations |
| AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts | Landing page data models and behaviors |
| AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html | Before/after showcase markup pattern |
| AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass | Image card styling pattern for showcase |
| AI.ProfilePhotoMaker.UI/src/app/shared/styles/_redesign-system.sass | Design system mixins, animation, and variables |
| AI.ProfilePhotoMaker.UI/src/app/shared/styles/REDESIGN_SYSTEM_GUIDE.md | Design system usage and constraints |
| AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/* | Existing before/after assets for image-led layout |

### Technical Decisions

- Visual direction: minimal/pro with stronger image presence
- Use before/after headshot imagery for hero/sections (reuse existing assets when possible)
- Keep existing sections where possible; allow light reordering if needed
- Favor shared redesign system mixins instead of new ad-hoc styles

## Implementation Plan

### Tasks

- [x] Task 1: Inventory and select image assets for the image-led layout
  - File: `AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/*`
  - Action: Confirm which before/after sets (set-1 to set-3) will be used in hero/section treatments; add any new image files only if needed.
  - Notes: Prefer existing assets to avoid new dependencies; keep naming consistent with current set patterns.
- [x] Task 2: Redesign hero section markup to be image-led and minimal
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html`
  - Action: Replace current abstract hero visual with a before/after showcase and/or headshot image grid; reduce decorative text blocks and align CTAs with the new imagery.
  - Notes: Keep hero content structure but shift layout to emphasize visuals; ensure existing CTA actions remain intact.
- [x] Task 3: Update landing data/config to support new hero and image sections
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts`
  - Action: Add or adjust data arrays for hero image sets, before/after labels, and any new image metadata used by the template.
  - Notes: Keep data-driven patterns consistent with `features`, `testimonials`, and `styledPhotos` arrays.
- [x] Task 4: Restyle landing sections to match minimal/pro and image-led direction
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.sass`
  - Action: Replace glassmorphism-heavy and grid-only layouts with cleaner spacing, stronger image framing, and restrained animation; ensure hero, features, styles, pricing, testimonials, FAQ, and CTA feel visually unified.
  - Notes: Tone down animated background elements; prioritize image framing, whitespace, and typography hierarchy.
- [x] Task 5: Extend or tune shared redesign system for new image-led patterns
  - File: `AI.ProfilePhotoMaker.UI/src/app/shared/styles/_redesign-system.sass`
  - Action: Add reusable mixins or utility classes for image frames, before/after cards, and minimal/pro section spacing if not already available.
  - Notes: Favor shared mixins over landing-specific overrides; ensure dark theme tokens remain compatible.
- [x] Task 6: Validate marketing page alignment and reuse patterns
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html`
  - Action: Reference the showcase markup patterns for before/after imagery to keep the landing page consistent with marketing pages.
  - Notes: No direct edits required unless reusing a pattern warrants a shared helper.

### Acceptance Criteria

- [x] AC 1: Given a user lands on the homepage, when the hero loads, then a before/after or image-led visual is the dominant element and CTAs remain visible and functional.
- [x] AC 2: Given the landing page sections render, when a user scrolls through features, examples, pricing, testimonials, FAQ, and CTA, then each section presents at least one strong image-led visual treatment and avoids a generic text-grid look.
- [x] AC 3: Given the design is viewed on desktop and mobile, when the layout adapts, then imagery remains legible, CTAs are accessible, and spacing/typography stay minimal and professional.
- [x] AC 4: Given the user enables reduced motion or prefers minimal animations, when the landing page loads, then motion effects are subtle or disabled without breaking layout.
- [x] AC 5: Given the landing page uses before/after assets, when images fail to load, then fallbacks or placeholders display without layout collapse.

## Additional Context

### Dependencies

- Existing before/after assets in `AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/*`
- Shared redesign system mixins in `AI.ProfilePhotoMaker.UI/src/app/shared/styles/_redesign-system.sass`
- Marketing header/footer components for consistent navigation

### Testing Strategy

- Manual: Review landing page on desktop and mobile breakpoints; verify hero imagery, CTA behavior, and section alignment.
- Manual: Toggle light/dark themes and reduced motion to ensure visual integrity.
- Optional: Run `npm run test:e2e` if visual changes impact SEO routes (no expected changes).

### Notes

- Primary risk is overusing imagery or animations that reduce perceived professionalism; keep layouts clean and restrained.
- Consider aligning section spacing and image framing with HeadshotPro/Aragon AI patterns while keeping current copy.

## Dev Agent Record

### File List

- AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html
- AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.sass
- AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts
- AGENTS.md (pre-existing change outside landing page scope)

### Change Log

- Added image-led pricing, testimonials, and FAQ visuals using SEO page showcase markup patterns.
- Paused hero rotation when reduced motion is preferred or hero is offscreen.
- Hardened hero card layout with background aspect frames to avoid collapse on image fallback.
