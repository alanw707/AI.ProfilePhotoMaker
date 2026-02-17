---
title: 'Add Google OAuth to Registration + Sample Images to SEO Pages'
slug: 'google-oauth-registration-seo-sample-images'
created: '2026-02-15'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack:
  - Angular 15+ (standalone components)
  - TypeScript 4.8+
  - RxJS 7+
  - HTML5/SASS
  - Reactive Forms
  - ASP.NET Core backend (OAuth endpoints ready)
files_to_modify:
  - AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.html
  - AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.ts
  - AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1a.data.ts
  - AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1b.data.ts
code_patterns:
  - Standalone Angular components with OnPush change detection
  - Reactive Forms with formControlName binding
  - Event binding for OAuth button (click) handlers
  - Lazy-loaded auth module at /auth route
  - Data-driven SEO pages via Route data injection
  - SeoShowcaseSection type with before/after image pairs
  - Consistent OAuth flow - redirect to backend endpoint with returnUrl
  - Age confirmation required for all auth methods (COPPA compliance)
  - ConfigService for environment-aware OAuth base URLs
  - Reusable component patterns (no new components needed)
test_patterns:
  - Manual E2E testing for OAuth flow
  - Visual regression testing for SEO pages
  - No unit tests required (UI-only changes)
---

# Tech-Spec: Add Google OAuth to Registration + Sample Images to SEO Pages

**Created:** 2026-02-15

## Overview

### Problem Statement

1. **Critical Conversion Gap:** Users clicking "Get Started" in the header are directed to the registration form, which only supports email/password registration. Google OAuth is already fully implemented in the backend and available on the login page, but missing from the registration UI. This creates unnecessary friction and likely causes user drop-off.

2. **SEO Page Credibility Gap:** Three key SEO landing pages (Professional headshots, Headshots for job search, Headshot enhancer) display only text content with no example results. This reduces credibility and conversion potential compared to competitor pages that showcase actual AI-generated results.

### Solution

1. **Add Google OAuth to Registration:** Implement a "Create account via Google" button on the registration form, matching the existing login page's OAuth implementation pattern including age confirmation checkbox validation.

2. **Add Sample Images to SEO Pages:** Add before/after showcase sections to the three identified SEO pages by extending their route data configurations with the existing `showcase` section type and available image assets (set-1, set-2, set-3).

### Scope

**In Scope:**
- Registration component (`register.component.html`): Add Google OAuth UI section with divider, button, and age checkbox validation
- Registration component (`register.component.ts`): Verify `registerWithGoogle()` method is properly wired and functional
- Three SEO page data files: Add `showcase` sections to Professional headshots, Headshots for job search, and Headshot enhancer pages
- Reuse existing before/after image sets from `/assets/marketing/before-after/`
- Ensure OAuth button is disabled until age confirmation checkbox is checked (matching login behavior)

**Out of Scope:**
- Backend changes (Google OAuth endpoints already fully implemented)
- SEO pages not listed in the Use Cases dropdown
- Creating new image assets (use existing sets)
- Changes to login page OAuth (already working)
- Auth flow logic modifications

## Context for Development

### Codebase Patterns

**Authentication Pattern:**
- Angular standalone components with lazy-loaded auth module
- Reactive Forms for email/password registration
- Event handlers for OAuth (separate from form submission)
- Age confirmation checkbox required before enabling OAuth buttons (regulatory compliance)
- OAuth flow: Frontend redirects to backend endpoint → Backend redirects to Google → Callback sets JWT cookie → Frontend handles post-auth routing

**SEO Page Pattern:**
- Data-driven architecture: Page content defined in TypeScript data files, injected via Angular route data
- Reusable `SeoPageComponent` renders content based on data structure
- Section types: `hero`, `content`, `showcase`, `features`, `comparison`, `pricing`, `faq`
- Showcase sections display before/after image pairs with labels
- Images lazy-loaded with fallback handling

**Component Structure:**
```
auth/
├── register/
│   ├── register.component.html    (Form + OAuth UI - MODIFY)
│   ├── register.component.ts      (registerWithGoogle() method exists - VERIFY)
│   └── register.component.sass    (Styles - REFERENCE)
├── login/
│   ├── login.component.html       (OAuth UI pattern - REFERENCE)
│   └── login.component.ts         (loginWithGoogle() pattern - REFERENCE)
pages/marketing/
├── seo-page/
│   └── seo-page.component.html    (Showcase rendering - REFERENCE)
├── seo-pages.records.part1a.data.ts  (Professional headshots - MODIFY)
├── seo-pages.records.part1b.data.ts  (Headshots for job search - MODIFY)
└── seo-pages.records.part1c.data.ts  (Headshot enhancer - MODIFY)
```

### Files to Reference

| File | Purpose | Key Lines |
| ---- | ------- | --------- |
| `AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.html` | OAuth UI pattern reference | Lines 95-114 (divider, age checkbox, Google button) |
| `AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.ts` | `loginWithGoogle()` implementation | Lines 271-291 |
| `AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.ts` | `registerWithGoogle()` method | Lines 215-222 (VERIFIED: correctly constructs OAuth URL) |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html` | Showcase section rendering | Lines 104-155 |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.types.ts` | TypeScript interfaces | `SeoShowcaseSection`, `SeoShowcaseItem` definitions |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1a.data.ts` | SEO data - Headshot enhancer | Lines 305-387 (ADD showcase here) |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1b.data.ts` | SEO data - Professional + Job search | Lines 10-105 (Professional), Lines 106-183 (Job search) |
| `AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/` | Available image assets | set-1, set-2, set-3 (before/after pairs) |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.data.ts` | Route data aggregation | Imports and merges part1a, part1b, part1c |

### Technical Decisions

**1. Google OAuth Button Position:**
- Place OAuth section ABOVE the email/password form (reduces friction for users who prefer social login)
- Add visual divider with "OR" text between OAuth and form sections (standard UX pattern)
- Use "Create account via Google" text (distinct from login's "Continue with Google")

**2. Age Confirmation Handling:**
- Reuse existing `ageConfirmed` form control (line 56 in register.component.ts)
- Button disabled state bound to `f['ageConfirmed'].value !== true` (matching login page lines 104, 88)
- Checkbox label links to Children's Privacy Policy (already implemented line 147)
- **CRITICAL:** Must maintain COPPA compliance - age confirmation required before any auth method

**3. OAuth Implementation Verification:**
- `registerWithGoogle()` method (lines 215-222) is ALREADY IMPLEMENTED and correct
- Constructs: `${oauthBaseUrl}/api/auth/external-login/Google?returnUrl=${fullReturnUrl}`
- Backend endpoint `/api/auth/external-login/Google` is ready and working (used by login)
- Only UI HTML changes needed - no TypeScript changes required

**4. Image Set Assignment for SEO Pages:**
| SEO Page | Page Key | File | Line Range | Image Set | Rationale |
|----------|----------|------|------------|-----------|-----------|
| Professional headshots | `professional-headshots` | part1b.data.ts | 10-105 | set-1 | Generic professional appearance |
| Headshots for job search | `headshots-for-job-search` | part1b.data.ts | 106-183 | set-2 | Modern, approachable look |
| Headshot enhancer | `free-headshot-enhancer` | part1a.data.ts | 305-387 | set-3 | Clear enhancement visible |

**5. Showcase Section Structure:**
- Insert `showcase` section AFTER hero/highlights, BEFORE other content sections
- Structure: `{ type: 'showcase', title: string, intro: string, items: SeoShowcaseItem[] }`
- Each item: `{ title, description, beforeImage, afterImage, beforeAlt, afterAlt }`
- Use 2 image pairs per page for visual impact without overwhelming
- Image paths: `/assets/marketing/before-after/set-{N}-{before|after}.{jpg|png}`

## Implementation Plan

### Tasks

#### Task 1: Add Google OAuth UI to Registration Form
**File:** `AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.html`
**Lines to modify:** Insert after line 21 (after `<p>Join us to create amazing AI-generated profile photos</p>`), before `<form>` tag

**Action:** Add OAuth section with:
1. Age confirmation checkbox section (copy structure from lines 59-81 of login.component.html)
2. "Create account via Google" button with Google SVG logo
3. Visual divider `<div class="social-login-divider"><span>OR</span></div>`
4. Button disabled state: `[disabled]="f['ageConfirmed'].value !== true"`

**Exact code pattern to copy:** Lines 59-114 from `login.component.html` (age checkbox + OAuth button + divider)
**Modification:** Change button text from "Continue with Google" to "Create account via Google"

#### Task 2: Verify registerWithGoogle() Method
**File:** `AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.ts`
**Lines:** 215-222 (already exists)

**Verification checklist:**
- [ ] Method uses `this._configService.getOAuthBaseUrl()`
- [ ] Constructs URL: `${oauthBaseUrl}/api/auth/external-login/Google?returnUrl=...`
- [ ] Return URL points to `/app/dashboard`
- [ ] Uses `window.location.href` for redirect

**Status:** ✅ VERIFIED - Method is correct, no changes needed

#### Task 3: Add Sample Images to Professional Headshots SEO Page
**File:** `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1b.data.ts`
**Page key:** `professional-headshots` (lines 10-105)
**Insert location:** After `hero` object (line 26), as first element in `sections` array

**Action:** Add showcase section:
```typescript
{
  type: 'showcase',
  title: 'Before and after results',
  intro: 'See how casual photos become polished professional headshots.',
  items: [
    {
      title: 'Professional studio look',
      description: 'Clean lighting and background for a credible business presence.',
      beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
      afterImage: '/assets/marketing/before-after/set-1-after.jpg',
      beforeAlt: 'Casual photo before professional headshot',
      afterAlt: 'Professional studio headshot after',
    },
    {
      title: 'Corporate ready',
      description: 'Balanced exposure and professional styling for team pages.',
      beforeImage: '/assets/marketing/before-after/set-2-before.jpg',
      afterImage: '/assets/marketing/before-after/set-2-after.png',
      beforeAlt: 'Original photo before enhancement',
      afterAlt: 'Corporate-ready headshot after',
    },
  ],
}
```

#### Task 4: Add Sample Images to Headshots for Job Search SEO Page
**File:** `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1b.data.ts`
**Page key:** `headshots-for-job-search` (lines 106-183)
**Insert location:** After `hero` object (line 122), as first element in `sections` array

**Action:** Add showcase section:
```typescript
{
  type: 'showcase',
  title: 'Job search transformation',
  intro: 'Stand out in applicant tracking systems with a polished professional photo.',
  items: [
    {
      title: 'Approachable professional',
      description: 'Warm, confident look that builds trust with recruiters.',
      beforeImage: '/assets/marketing/before-after/set-2-before.jpg',
      afterImage: '/assets/marketing/before-after/set-2-after.png',
      beforeAlt: 'Casual photo before job search headshot',
      afterAlt: 'Approachable professional headshot after',
    },
    {
      title: 'LinkedIn optimized',
      description: 'Clean framing and lighting designed for profile visibility.',
      beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
      afterImage: '/assets/marketing/before-after/set-1-after.jpg',
      beforeAlt: 'Original selfie before optimization',
      afterAlt: 'LinkedIn-optimized headshot after',
    },
  ],
}
```

#### Task 5: Add Sample Images to Headshot Enhancer SEO Page
**File:** `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.part1a.data.ts`
**Page key:** `free-headshot-enhancer` (lines 305-387)
**Insert location:** After first section (currently bullets at line 329), or as new first section

**Action:** Add showcase section:
```typescript
{
  type: 'showcase',
  title: 'Enhancement results',
  intro: 'See the difference AI enhancement makes to existing photos.',
  items: [
    {
      title: 'Lighting and clarity improvement',
      description: 'Balanced exposure with preserved natural detail.',
      beforeImage: '/assets/marketing/before-after/set-3-before.jpg',
      afterImage: '/assets/marketing/before-after/set-3-after.png',
      beforeAlt: 'Original photo before enhancement',
      afterAlt: 'Enhanced photo with better lighting',
    },
    {
      title: 'Background refinement',
      description: 'Cleaner background for professional presentation.',
      beforeImage: '/assets/marketing/before-after/set-1-before.jpg',
      afterImage: '/assets/marketing/before-after/set-1-after.jpg',
      beforeAlt: 'Photo with distracting background',
      afterAlt: 'Photo with clean professional background',
    },
  ],
}
```

### Acceptance Criteria

#### AC1: Google OAuth Button Visible on Registration
**Given** a user navigates to `/auth/register`
**When** the page loads
**Then** they see a "Create account via Google" button above the email form
**And** the button includes the Google logo
**And** a visual "OR" divider separates OAuth from the email form

#### AC2: Age Confirmation Required for OAuth
**Given** a user on the registration page
**When** they have NOT checked the age confirmation checkbox
**Then** the "Create account via Google" button is disabled
**And** the button appears visually disabled (reduced opacity)

#### AC3: Google OAuth Flow Works from Registration
**Given** a user on the registration page with age confirmation checked
**When** they click "Create account via Google"
**Then** they are redirected to Google's OAuth consent screen
**And** after successful authentication, they are logged in and redirected appropriately

#### AC4: Professional Headshots Page Shows Samples
**Given** a user navigates to `/professional-headshots`
**When** the page loads
**Then** they see a before/after showcase section with 2 image pairs
**And** images are from set-1 (before-1, after-1)

#### AC5: Headshots for Job Search Page Shows Samples
**Given** a user navigates to `/headshots-for-job-search`
**When** the page loads
**Then** they see a before/after showcase section with 2 image pairs
**And** images are from set-2 (before-2, after-2)

#### AC6: Headshot Enhancer Page Shows Samples
**Given** a user navigates to `/free-headshot-enhancer`
**When** the page loads
**Then** they see a before/after showcase section with 2 image pairs
**And** images are from set-3 (before-3, after-3)

#### AC7: All SEO Pages Load Without Errors
**Given** all three modified SEO pages
**When** loaded in a browser
**Then** no console errors appear
**And** images load correctly
**And** responsive layout works on mobile and desktop

## Additional Context

### Dependencies

**No new dependencies required.** All functionality uses existing:
- Angular framework (v15+)
- Existing auth service and config service
- Existing SEO page component and types
- Existing image assets

### Testing Strategy

**Manual Testing Required:**
1. **OAuth Flow Test:**
   - Navigate to registration
   - Verify button disabled without age confirmation
   - Check age confirmation enables button
   - Click OAuth button → verify redirect to Google
   - Complete OAuth → verify successful login

2. **SEO Page Visual Test:**
   - Visit each of the 3 SEO pages
   - Verify showcase section appears
   - Check before/after images display correctly
   - Test responsive behavior (mobile/desktop)

**No unit tests required** (UI-only changes, existing patterns)

### Notes

- **Backend is ready:** The Google OAuth endpoints (`/api/auth/external-login/Google`) are fully implemented and working (login page uses them)
- **Image assets available:** All before/after images are in `src/assets/marketing/before-after/`
- **Login page reference:** Use `login.component.html` lines 100-113 as the exact pattern to copy
- **Age compliance:** Critical to maintain age confirmation requirement for OAuth (COPPA compliance)
- **SEO page routing:** Changes to data files will reflect immediately on page reload (no rebuild required for data changes)

### Files Modified Summary

| # | File | Change Type | Details |
|---|------|-------------|---------|
| 1 | `register.component.html` | Add OAuth UI section | Insert age checkbox + Google button + OR divider after header |
| 2 | `register.component.ts` | Verify method | Confirm `registerWithGoogle()` lines 215-222 (no changes needed) |
| 3 | `seo-pages.records.part1b.data.ts` | Add showcase section | Professional headshots page - 2 image pairs (set-1, set-2) |
| 4 | `seo-pages.records.part1b.data.ts` | Add showcase section | Headshots for job search page - 2 image pairs (set-2, set-1) |
| 5 | `seo-pages.records.part1a.data.ts` | Add showcase section | Free headshot enhancer page - 2 image pairs (set-3, set-1) |
