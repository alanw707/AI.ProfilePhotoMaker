---
title: 'Landing Page Image-Led Redesign'
slug: 'landing-page-image-led-redesign'
created: '2026-01-20T05:18:11-08:00'
status: 'in-progress'
stepsCompleted: [1]
tech_stack: []
files_to_modify: []
code_patterns: []
test_patterns: []
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

Use the existing landing page structure in the Angular UI and keep sections largely intact while reworking visual presentation. Maintain responsive behavior and theme support.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html | Landing page markup and section layout |
| AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.sass | Landing page styling and animations |
| AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts | Landing page data models and behaviors |

### Technical Decisions

- Visual direction: minimal/pro with stronger image presence
- Use before/after headshot imagery for hero/sections
- Keep existing sections where possible; allow light reordering if needed

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
