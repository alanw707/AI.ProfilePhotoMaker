---
title: 'Marketing selfie count copy update'
slug: 'marketing-selfie-count-copy-update'
created: '2026-01-09T21:26:37Z'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Angular', 'TypeScript']
files_to_modify: ['AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts']
code_patterns: ['seoPages marketing content data in seo-pages.data.ts']
test_patterns: ['Manual UI verification for marketing pages']
---

# Tech-Spec: Marketing selfie count copy update

**Created:** 2026-01-09T21:26:37Z

## Overview

### Problem Statement

Marketing pages currently communicate an outdated selfie count (8-12), which conflicts with the actual minimum requirement of 10. This misinformation needs correction to avoid confusing users.

### Solution

Update all selfie-count copy in `seo-pages.data.ts` to communicate "Upload at least 10 clear selfies" and related wording for marketing pages, including the How-it-works page highlight/steps and any other mentions in that file. Leave Premium page copy unchanged.

### Scope

**In Scope:**
- Update all marketing page copy in `seo-pages.data.ts` that references selfie counts (How-it-works highlights/steps + other sections)
- Use "at least 10" phrasing consistently in titles, descriptions, and FAQ answers

**Out of Scope:**
- Changes to the Premium page copy (`premium.component.ts`)
- Updates to other non-marketing pages not defined in `seo-pages.data.ts`
- Backend validation or upload limits

## Context for Development

### Codebase Patterns

- Marketing pages are configured in `seo-pages.data.ts` under the `seoPages` record.
- Highlights, steps, and FAQs are defined in structured arrays and rendered by shared marketing components.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts | Marketing page hero, highlights, steps, and FAQ copy |

### Technical Decisions

- Use plain copy updates only; no UI component changes.
- Use "at least 10" wording for clarity and simplicity.
- Leave Premium page copy unchanged.

## Implementation Plan

### Tasks

- [ ] Task 1: Update How-it-works highlight and step title copy
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts`
  - Action: Change the How-it-works highlight value and the step title from "8-12" to "at least 10" wording.
  - Notes: Keep labels intact; only update the value and title text.
- [ ] Task 2: Update other marketing steps/FAQs that reference selfie counts
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts`
  - Action: Replace "8-12" in the relevant step description and FAQ answer with "at least 10" phrasing.
  - Notes: Ensure consistent tone and grammatical flow.

### Acceptance Criteria

- [ ] AC 1: Given the How-it-works page is rendered, when the highlight tiles display, then the selfie count shows "at least 10" instead of "8-12".
- [ ] AC 2: Given the How-it-works page is rendered, when the first step is shown, then the title reads "Upload at least 10 clear selfies".
- [ ] AC 3: Given marketing pages using `seo-pages.data.ts` are rendered, when selfie-count descriptions or FAQs are displayed, then they use "at least 10" wording and no "8-12" remains.

## Additional Context

### Dependencies

- None

### Testing Strategy

- Manually verify How-it-works and the other marketing page sections that contain selfie count copy.
- Confirm no "8-12" references remain in `seo-pages.data.ts` for marketing content.

### Notes

- Keep copy consistent across all marketing mentions in `seo-pages.data.ts`.
