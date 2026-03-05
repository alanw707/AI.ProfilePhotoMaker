---
title: 'Improve Signup-to-Activation Conversion Flow'
slug: 'signup-activation-conversion-flow'
created: '2026-03-04'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack:
  - 'Angular 18+ (standalone components)'
  - 'TypeScript'
  - 'RxJS'
  - 'Angular Router'
  - 'SCSS/SASS'
files_to_modify:
  - 'seo-page.component.ts'
  - 'seo-page.component.html'
  - 'seo-pages.records.*.data.ts (add CTA types)'
  - 'marketing-header.component.ts'
  - 'premium.component.ts'
  - 'register.component.ts'
  - 'verify-email.component.ts'
  - 'verify-email.component.html'
  - 'verify-email.component.sass'
  - 'dashboard.component.ts'
  - 'dashboard.component.html'
  - 'app.component.ts (or new intent-tracking.service.ts)'
code_patterns:
  - 'Standalone components with ChangeDetectionStrategy.OnPush'
  - 'Reactive forms with FormBuilder'
  - 'RxJS observables with finalize, catchError operators'
  - 'Router navigation with queryParams'
  - 'sessionStorage for redirectUrl (existing pattern in app.guard.ts)'
  - 'localStorage for currentUser, theme preferences'
test_patterns:
  - 'Jasmine/Karma test framework'
  - 'xdescribe for skipped test suites'
  - 'jasmine.createSpyObj for mock services'
  - 'TestBed.configureTestingModule for component setup'
  - 'of() for mock observable returns'
---

# Tech-Spec: Improve Signup-to-Activation Conversion Flow

**Created:** 2026-03-04

## Overview

### Problem Statement

New users sign up (8 so far) but 0 have tried the photo features. The current flow has friction at email verification and lacks post-verification guidance, causing drop-off before users experience value.

### Solution

Enhance the signup flow with (1) intent tracking from SEO page CTAs, (2) improved verify-email UX with urgency messaging, and (3) contextual onboarding on dashboard based on user's original intent.

### Scope

**In Scope:**
- Add CTA intent tracking (source page + CTA type) via URL params before register
- Enhance `/auth/verify-email` page with urgency/preview messaging
- Modify `/app/dashboard` to show contextual CTAs based on tracked intent
- Store intent in session/localStorage through signup flow

**Out of Scope:**
- Changing credit amounts (already bumped to 25)
- Email deliverability investigation (not going to spam)
- Analytics/telemetry backend (if needed, that's separate)
- A/B testing infrastructure

## Context for Development

### Codebase Patterns

- Angular standalone components with `ChangeDetectionStrategy.OnPush`
- Reactive forms with `FormBuilder` and `Validators`
- RxJS observables for async operations (`finalize`, `catchError`)
- Router navigation with query params
- LocalStorage for session persistence (`auth_token`, `currentUser`, `theme`)
- sessionStorage for `redirectUrl` (existing pattern in `app.guard.ts` lines 61, 78, 125)
- Service-based state management (no NgRx)
- Analytics tracking via `AnalyticsService.trackEvent()` for CTA clicks

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.*.data.ts` | SEO page definitions with CTAs - ALL route to `/pricing` currently |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.ts` | SEO page component - handles CTA clicks, UTM params (lines 72-83, 93-100) |
| `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html` | CTA bindings with queryParams (lines 38-58, 241-248) |
| `AI.ProfilePhotoMaker.UI/src/app/shared/marketing-header/marketing-header.component.ts` | Header "Get Started" button - links to `/auth/register` (line 149) |
| `AI.ProfilePhotoMaker.UI/src/app/pages/premium/premium.component.ts` | Pricing page - uses `<app-credit-packages>` |
| `AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.ts` | Registration - redirects to `/auth/verify-email` (lines 180-181) |
| `AI.ProfilePhotoMaker.UI/src/app/auth/verify-email/verify-email.component.ts` | Email verification - uses `sessionStorage.getItem('redirectUrl')` (lines 179-183) |
| `AI.ProfilePhotoMaker.UI/src/app/auth/verify-email/verify-email.component.html` | Template with header, actions, messages (41 lines) |
| `AI.ProfilePhotoMaker.UI/src/app/auth/verify-email/verify-email.component.sass` | Styles with mixins: `shared.card-base`, `shared.btn-base` (86 lines) |
| `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.ts` | Dashboard - `isPremiumWorkflow()` determines view (lines 435-438) |
| `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.html` | "Get Started" section for new users (lines 83-149) |
| `AI.ProfilePhotoMaker.UI/src/app/guards/app.guard.ts` | Sets `redirectUrl` in sessionStorage (lines 61, 78, 125) |

### Technical Decisions

1. **Intent Storage:** Use `sessionStorage` with key `signupIntent` (follows existing `redirectUrl` pattern)
   - Survives page refresh
   - Cleared after use on dashboard
   - Not persisted across sessions (privacy-friendly)

2. **Intent Data Structure:**
   ```typescript
   interface SignupIntent {
     sourcePage: string;        // e.g., 'linkedin-headshots', 'professional-headshots'
     ctaType: 'headshots' | 'enhance' | 'pricing';
     timestamp: number;         // For expiration check
   }
   ```

3. **CTA Type Routing:**
   - SEO pages with "Get your headshot" CTAs → `ctaType: 'headshots'`
   - New "Enhance Photo" CTAs (to add) → `ctaType: 'enhance'`
   - Pricing page (fallback) → `ctaType: 'pricing'`

4. **Verify-Email Enhancements:**
   - Add urgency banner: "Your 25 free credits expire in 7 days"
   - Add preview teaser: Blurred example images showing what they'll get
   - Add progress indicator: "Step 2 of 3: Verify email"

5. **Dashboard Personalization:**
   - Read `signupIntent` from sessionStorage
   - Show contextual welcome: "Ready for your LinkedIn headshots?" vs "Let's enhance your photos"
   - Pre-select appropriate styles if `ctaType === 'headshots'`
   - Clear intent after displaying personalized content

6. **Flow Change - SEO Page CTA Routing:**
   - SEO page CTA will route directly to `/auth/register?intent={encodedIntent}`
   - Bypasses `/pricing` for headshots intent (faster path to signup)
   - Pricing page still available for users who want to compare

### Key Investigation Findings

**Current Flow Gaps:**
1. SEO page CTAs all route to `/pricing` with no intent tracking
2. Pricing page "Get Started" button links to `/auth/register` without params
3. Register component ignores query params, always redirects to `/auth/verify-email`
4. No first-time user detection on dashboard
5. No onboarding state tracking exists

**Existing Patterns to Follow:**
- UTM params: Already extracted in `seo-page.component.ts` (lines 283-293)
- Query param passing: `getQueryParamsForHref()` pattern (lines 93-100)
- Redirect URL: `sessionStorage.getItem('redirectUrl')` in verify-email (lines 179-183)
- Analytics tracking: `AnalyticsService.trackEvent()` for CTA clicks (lines 72-83)

## Implementation Plan

### [x] Task 1: Create Intent Tracking Service
**File:** `AI.ProfilePhotoMaker.UI/src/app/services/intent-tracking.service.ts` (NEW FILE)
**Action:** Create new service to manage signup intent across the flow
**Notes:**
- Create `SignupIntent` interface with `sourcePage`, `ctaType`, `timestamp`
- Implement `storeIntent()` method using sessionStorage
- Implement `getIntent()` method with expiration check (7 days)
- Implement `clearIntent()` method
- Add helper methods: `isValidIntent()`, `hasHeadshotsIntent()`
- Follow existing service pattern with proper TypeScript types
- Add unit tests following jasmine.createSpyObj pattern

### [x] Task 2: Modify SEO Page Data to Include CTA Intent
**File:** `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.types.ts`
**Action:** Add `ctaIntent` property to SeoPageContent interface
**Notes:**
- Add optional `ctaIntent: 'headshots' | 'enhance' | 'pricing'` to SeoPageContent
- Update SeoPageHero to include ctaIntent
- Keep backward compatibility (optional property)

### [x] Task 3: Update SEO Page Component to Pass Intent
**File:** `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.ts`
**Action:** Modify CTA routing to pass intent as query param
**Notes:**
- Update `getQueryParamsForHref()` to include intent when routing to `/auth/register`
- Add `getRouterLinkForHref()` logic: if ctaHref is '/pricing' and page has ctaIntent='headshots', route to '/auth/register' instead
- Add `onCtaClick()` to store intent in sessionStorage before navigation
- Import IntentTrackingService
- Update query param handling to encode intent object

### [x] Task 4: Update SEO Page Template Intent Binding
**File:** `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html`
**Action:** Bind intent data to CTA buttons
**Notes:**
- Ensure both hero CTA (line 38-46) and footer CTA (line 241-248) pass intent
- Update routerLink bindings to use conditional routing based on ctaIntent
- Verify queryParams binding includes intent parameter

### [x] Task 5: Add CTA Intent to SEO Page Records
**Files:** `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.records.*.data.ts`
**Action:** Add ctaIntent property to all headshot-focused pages
**Notes:**
- Update ALL pages in seo-pages.records.*.data.ts files
- Pages focused on headshots: add `ctaIntent: 'headshots'`
- Pages like '/how-it-works': add `ctaIntent: 'pricing'`
- Leave existing ctaHref as '/pricing' (Task 3 will handle routing)
- Ensure consistency across all 5 record files

### [x] Task 6: Modify Register Component to Capture Intent
**File:** `AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.ts`
**Action:** Read intent from query params and store in sessionStorage
**Notes:**
- Import IntentTrackingService in constructor
- Add ngOnInit to extract 'intent' query param from ActivatedRoute
- Call `intentService.storeIntent()` if intent param exists
- After successful registration, ensure intent is preserved
- Handle error case: if intent parsing fails, log warning but continue

### [x] Task 7: Enhance Verify-Email Page with Urgency Messaging
**File:** `AI.ProfilePhotoMaker.UI/src/app/auth/verify-email/verify-email.component.ts`
**Action:** Add intent-aware urgency banner and preview content
**Notes:**
- Import IntentTrackingService
- Add `signupIntent` property and load on init
- Add computed property `getUrgencyMessage()` based on intent
- Add computed property `getPreviewImages()` based on ctaType
- Add `getProgressStep()` returning "Step 2 of 3"
- Update `continue()` method to pass intent context to dashboard

### [x] Task 8: Update Verify-Email Template
**File:** `AI.ProfilePhotoMaker.UI/src/app/auth/verify-email/verify-email.component.html`
**Action:** Add urgency banner, preview section, and progress indicator
**Notes:**
- After header (line 9): Add urgency banner div with conditional display
- Before actions (line 19): Add preview-teaser section with blurred images
- In header (line 5-8): Add progress indicator "Step 2 of 3: Verify Email"
- Use existing `.note` class for urgency message styling
- Use conditional rendering with *ngIf="signupIntent"

### [x] Task 9: Add Verify-Email Styles
**File:** `AI.ProfilePhotoMaker.UI/src/app/auth/verify-email/verify-email.component.sass`
**Action:** Add CSS for urgency banner and preview teaser
**Notes:**
- Add `.urgency-banner` with gradient background (use --accent-gradient-1)
- Add `.preview-teaser` with grid layout for preview images
- Add `.progress-indicator` styling (subtle, centered)
- Use existing color variables: --text-primary, --text-secondary
- Add animation for urgency banner (pulse-glow keyframe)
- Keep consistent with existing .verify-container card styling

### [x] Task 10: Modify Dashboard Component for Intent Handling
**File:** `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.ts`
**Action:** Read intent and show personalized welcome
**Notes:**
- Import IntentTrackingService in constructor
- Add `signupIntent` property
- Add `isFirstTimeUser()` method checking uploadedImages === 0 AND generatedPhotosCount === 0
- Add `getWelcomeMessage()` based on intent: headshots → "Ready for your [source] headshots?", enhance → "Let's enhance your photos", pricing → "Welcome! Get started with..."
- Add `getPrimaryCta()` and `getSecondaryCta()` methods
- In ngOnInit, load intent and set flags
- After showing personalized content, call `intentService.clearIntent()`

### [x] Task 11: Update Dashboard Template with Contextual CTAs
**File:** `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.html`
**Action:** Replace generic welcome with intent-aware content
**Notes:**
- In "get-started-section" (line 83-149): Update header to show dynamic welcome message
- Replace static h2/p with interpolated values from component
- Conditionally show different option cards based on ctaType:
  - headshots: Emphasize "Premium Studio" card, show "Upload your photos"
  - enhance: Emphasize "Basic Enhancement" card
  - pricing: Show both cards equally
- Add quick-start CTA buttons at top of get-started-card
- Use *ngIf="isFirstTimeUser() && signupIntent" to show personalized view

### [x] Task 12: Add Dashboard Styles for Personalization
**File:** `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.sass`
**Action:** Add styles for welcome banner and contextual CTAs
**Notes:**
- Add `.personalized-welcome` styling (larger text, accent color)
- Add `.quick-start-cta` button styles (prominent primary action)
- Add `.option-card.highlighted` for emphasized option
- Use existing card-base and btn-base mixins

### [x] Task 13: Update Marketing Header Intent Passing
**File:** `AI.ProfilePhotoMaker.UI/src/app/shared/marketing-header/marketing-header.component.ts`
**Action:** Pass default intent when "Get Started" button clicked
**Notes:**
- Add click handler for "Get Started" button
- Store intent with `ctaType: 'pricing'` and `sourcePage: 'header'`
- Navigate to `/auth/register` with intent param

### [x] Task 14: Add Unit Tests for Intent Tracking Service
**File:** `AI.ProfilePhotoMaker.UI/src/app/services/intent-tracking.service.spec.ts` (NEW FILE)
**Action:** Create comprehensive test suite
**Notes:**
- Test storeIntent/getIntent/clearIntent methods
- Test expiration logic (7 days)
- Test edge cases: invalid JSON, missing data
- Use jasmine.createSpyObj for Window mock
- Follow existing test patterns from auth.service.spec.ts

### [x] Task 15: Update Verify-Email Component Tests
**File:** `AI.ProfilePhotoMaker.UI/src/app/auth/verify-email/verify-email.component.spec.ts` (NEW FILE)
**Action:** Add tests for intent integration
**Notes:**
- Test component displays urgency banner when intent present
- Test preview images show for headshots intent
- Test progress indicator renders
- Mock IntentTrackingService
- Test intent is passed to continue() method

### [x] Task 16: Update Dashboard Component Tests
**File:** `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.spec.ts`
**Action:** Add tests for personalized welcome
**Notes:**
- Test welcome message changes based on intent
- Test first-time user detection
- Test intent is cleared after display
- Mock IntentTrackingService and DashboardStateService

## Acceptance Criteria

### AC 1: Intent Tracking from SEO Page CTAs
**Given** a user visits `/linkedin-headshots` and clicks "Get your headshot in minutes"
**When** the CTA is clicked
**Then** the user is redirected to `/auth/register?intent={"sourcePage":"linkedin-headshots","ctaType":"headshots","timestamp":...}`
**And** the intent is stored in sessionStorage

### AC 2: Intent Preserved Through Registration
**Given** a user arrives at `/auth/register` with intent query param
**When** they complete registration
**Then** the intent remains in sessionStorage
**And** they are redirected to `/auth/verify-email`

### AC 3: Verify-Email Shows Urgency for Headshots Intent
**Given** a user with headshots intent on `/auth/verify-email`
**When** the page loads
**Then** they see "Step 2 of 3: Verify Email" progress indicator
**And** they see urgency banner: "Your 25 free credits are waiting! Complete verification to start your professional headshots."
**And** they see preview images (blurred) showing headshot examples

### AC 4: Verify-Email Shows Different Content for Enhance Intent
**Given** a user with enhance intent on `/auth/verify-email`
**When** the page loads
**Then** they see urgency banner: "Your 25 free credits are waiting! Complete verification to enhance your photos."
**And** they see preview images showing photo enhancement examples

### AC 5: Dashboard Personalization for Headshots
**Given** a verified user with headshots intent arrives at `/app/dashboard`
**When** they are first-time users (no uploads, no generated photos)
**Then** they see welcome message: "Ready for your LinkedIn headshots?"
**And** they see prominent "Start Training Your Model" CTA
**And** the intent is cleared from sessionStorage after display

### AC 6: Dashboard Personalization for Enhance
**Given** a verified user with enhance intent arrives at `/app/dashboard`
**When** they are first-time users
**Then** they see welcome message: "Let's enhance your photos!"
**And** they see prominent "Enhance Your First Photo" CTA
**And** the Basic Enhancement option is highlighted

### AC 7: Intent Expiration
**Given** a user has an intent stored more than 7 days ago
**When** they visit `/app/dashboard`
**Then** the intent is considered expired
**And** they see generic welcome message
**And** the expired intent is cleared from sessionStorage

### AC 8: Fallback for No Intent
**Given** a user arrives at `/app/dashboard` without any stored intent
**When** they are first-time users
**Then** they see generic welcome: "Welcome to AI Profile Photo Maker"
**And** both Premium Studio and Basic Enhancement options are shown equally

### AC 9: Intent Cleared After Use
**Given** a user with intent visits `/app/dashboard`
**When** the personalized welcome is displayed
**Then** the intent is immediately cleared from sessionStorage
**And** subsequent visits show generic welcome

### AC 10: Register Component Handles Intent Error
**Given** a user arrives at `/auth/register` with malformed intent query param
**When** registration is submitted
**Then** registration succeeds
**And** error is logged to console
**And** user proceeds to verify-email without intent (graceful degradation)

## Additional Context

### Dependencies
- **IntentTrackingService**: New service (no external dependencies)
- **sessionStorage**: Native browser API (already used in app.guard.ts)
- **ActivatedRoute**: Angular router (already imported in register, verify-email)
- **No new external libraries required**

### Testing Strategy

**Unit Tests:**
1. IntentTrackingService - all methods, expiration logic, edge cases
2. VerifyEmailComponent - intent display, urgency banner, preview images
3. DashboardComponent - welcome personalization, first-time detection
4. SEO Page components - intent parameter generation

**Integration Tests:**
1. Full flow: SEO page CTA → Register → Verify Email → Dashboard
2. Intent persistence through page refreshes
3. Intent expiration after 7 days

**Manual Testing:**
1. Test each SEO page (linkedin-headshots, professional-headshots, etc.)
2. Test with and without intent parameters
3. Test edge cases: malformed intent, expired intent, missing intent
4. Verify email deliverability not affected
5. Test across browsers: Chrome, Firefox, Safari

### Notes

**High-Risk Items:**
- **Breaking existing analytics**: Ensure `AnalyticsService.trackEvent()` still fires for CTA clicks
- **SEO page routing changes**: Verify `/pricing` still works for direct visits
- **sessionStorage limits**: Test intent size (should be small, < few KB)

**Known Limitations:**
- Intent only persists for current session (cleared when browser closed)
- 7-day expiration is arbitrary (could be adjusted based on data)
- No backend persistence of intent data

**Future Considerations:**
- Add A/B test for urgency messaging copy
- Track conversion rates per source page
- Add email reminder if user doesn't verify within 24 hours
- Consider persisting intent in URL throughout flow (more complex but survives browser close)
- Add onboarding progress bar visible in dashboard header

## Review Notes

- Adversarial review completed
- Findings: 10 total, 10 fixed
- Resolution approach: auto-fix + frontend personalization/taxonomy pass
