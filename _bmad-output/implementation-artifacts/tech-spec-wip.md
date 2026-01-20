---
title: 'Landing Page Image-Led Redesign'
slug: 'landing-page-image-led-redesign'
created: '2026-01-20T05:18:11-08:00'
status: 'in-progress'
stepsCompleted: [1, 2]
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

{tasks}

### Acceptance Criteria

{acceptance_criteria}

## Additional Context

### Dependencies

{dependencies}

### Testing Strategy

{testing_strategy}

### Notes

{notes}
