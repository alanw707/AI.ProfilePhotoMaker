---
title: 'Facebook Marketing Channel Strategy — Organic + Paid'
slug: 'facebook-marketing-channel-strategy'
created: '2026-02-11'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
adversarial_review: 'completed — 17 findings, all Critical/High resolved'
tech_stack: ['Angular 19', 'ASP.NET Core 8', 'Meta Pixel', 'Meta Conversions API', 'Meta Ads Manager', 'Facebook Business Page', 'GA4', 'Stripe', 'Cookie Consent']
files_to_modify: ['AI.ProfilePhotoMaker.UI/src/index.html', 'AI.ProfilePhotoMaker.UI/src/environments/environment.mvp-v1.ts', 'AI.ProfilePhotoMaker.UI/src/environments/environment.prod.ts', 'AI.ProfilePhotoMaker.UI/src/environments/environment.ts', 'AI.ProfilePhotoMaker.UI/src/environments/environment.docker.ts', 'AI.ProfilePhotoMaker.UI/src/app/services/meta-pixel.service.ts', 'AI.ProfilePhotoMaker.UI/src/app/app.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/pages/premium/premium.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/auth/complete-profile/complete-profile.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/components/dashboard/file-upload-section/file-upload-section.component.ts', 'AI.ProfilePhotoMaker.API/Services/MetaConversionsService.cs', 'AI.ProfilePhotoMaker.API/Services/Payments/StripeWebhookService.cs', 'AI.ProfilePhotoMaker.API/appsettings.json', 'AI.ProfilePhotoMaker.API/Program.cs', 'AI.ProfilePhotoMaker.UI/src/app/pages/privacy/privacy.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/pages/cookies/cookies.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/pages/subprocessors/subprocessors.component.ts', 'scripts/validate-secrets.sh', '_bmad-output/implementation-artifacts/marketing-execution-playbook-master-2025-12.md']
code_patterns: ['Consent-gated tracking (CookieConsentService.marketing category)', 'AnalyticsService wraps gtag with consent checks', 'Environment config for measurement IDs (analytics.ga4MeasurementId pattern)', 'Google Consent Mode v2 defaults in index.html', 'Standalone Angular components with dependency injection', 'Stripe webhook event processing in StripeWebhookService']
test_patterns: ['Playwright E2E tests in AI.ProfilePhotoMaker.UI/tests/', 'Unit tests with Karma/Jasmine (*.spec.ts)', 'Backend unit tests with xUnit/Moq/FluentAssertions']
---

# Tech-Spec: Facebook Marketing Channel Strategy — Organic + Paid

**Created:** 2026-02-11

## Overview

### Problem Statement

The AI Profile Photo Maker Facebook page (facebook.com/aiprofilephotomaker) exists but is dormant — 2 followers, 1 auto-generated post (cover photo update), no conversion tracking, and no content pipeline. There is no Meta Pixel installed on aiprofilephotomaker.com, meaning zero attribution data is being collected from Facebook traffic. The product needs user acquisition and conversion, but Facebook as a channel is completely untapped despite the page already being live.

### Solution

Stand up Facebook as a full acquisition channel by:
1. Installing Meta Pixel + Conversions API on aiprofilephotomaker.com for conversion tracking
2. Optimizing the Facebook Business Page for credibility and conversion (CTA button, services, about section)
3. Establishing an organic content cadence by cross-posting LinkedIn founder-led content
4. Launching Meta Ads with audience targeting and creatives adapted from existing LinkedIn ad playbook
5. Setting up conversion event tracking (view → upload → checkout → purchase)
6. Integrating with the existing master marketing execution playbook

### Scope

**In Scope:**
- Meta Pixel + Conversions API installation on aiprofilephotomaker.com
- Facebook Business Page optimization (CTA button, about section, services)
- Organic content cadence (cross-post from LinkedIn)
- Meta Ads campaign structure (audiences, creatives, budget framework)
- Ad creative adaptation from existing LinkedIn ad briefs
- Conversion event setup (view → upload → checkout → purchase)
- Integration with existing master marketing playbook
- KPI targets and reporting cadence
- Privacy Policy, Cookie Policy, and Subprocessors page updates for Meta data sharing disclosure

**Out of Scope:**
- Facebook Groups strategy
- Facebook Reels / video-first content (future iteration)
- Facebook-native unique content creation (cross-post only for now)
- Instagram (separate channel, currently paused)
- Messenger bots or automated chat
- Facebook Shop / Commerce Manager

## Context for Development

### Codebase Patterns

**Consent-Gated Tracking Pattern (established):**
- `CookieConsentService` manages categories: `essential`, `preferences`, `analytics`, `marketing`
- `AnalyticsService` subscribes to `consent$`, enables/disables tracking based on `analytics` category
- Google Consent Mode v2 defaults are set in `index.html` (all denied: `analytics_storage`, `ad_storage`, `ad_user_data`, `ad_personalization`)
- On consent grant, `AnalyticsService.enableAnalytics()` calls `gtag('consent', 'update', { analytics_storage: 'granted' })`
- **Critical (F1 fix):** The `ad_storage`, `ad_user_data`, and `ad_personalization` consent signals are currently NEVER updated by any service. The new `MetaPixelService` MUST update these via `gtag('consent', 'update', ...)` when marketing consent is granted/revoked. Without this, browsers honoring Consent Mode v2 will suppress the `_fbp` cookie, silently breaking pixel attribution even when consent is given.

**Environment Configuration Pattern:**
- `environment.*.ts` files contain `analytics.ga4MeasurementId`
- Meta Pixel ID should be added as `analytics.metaPixelId` to follow this convention
- Four environment files: `environment.ts` (local), `environment.docker.ts`, `environment.mvp-v1.ts`, `environment.prod.ts`

**Event Tracking Pattern:**
- `AnalyticsService.trackEvent(eventName, params)` fires GA4 events
- Currently used sparingly (SEO CTA clicks only)
- Meta Pixel events use a separate `MetaPixelService` (separation of concerns — different event schemas)

**Backend Payment Processing:**
- `StripeWebhookController` receives Stripe webhook events
- `StripeWebhookService` processes `payment_intent.succeeded`, updates user credits
- This is the anchor point for server-side Conversions API `Purchase` events

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `UI/src/index.html` | GA4 gtag + consent defaults — Meta Pixel base code goes here |
| `UI/src/app/services/analytics.service.ts` | GA4 tracking service — pattern to mirror for Meta Pixel |
| `UI/src/app/services/cookie-consent.service.ts` | Consent management — `marketing` category already exists |
| `UI/src/app/components/shared/cookie-consent/` | Consent UI — marketing toggle already wired |
| `UI/src/environments/environment.mvp-v1.ts` | Production env — needs `metaPixelId` |
| `UI/src/environments/environment.prod.ts` | Production env — needs `metaPixelId` |
| `UI/src/app/app.component.ts` | App bootstrap — where AnalyticsService.init() is called |
| `UI/src/app/components/credit-packages/credit-packages.component.ts` | Stripe checkout — `createPaymentIntent` at line 279 |
| `UI/src/app/auth/register/register.component.ts` | Email/password registration |
| `UI/src/app/auth/complete-profile/complete-profile.component.ts` | OAuth (Google) registration — success at line 102 |
| `UI/src/app/pages/premium/premium.component.ts` | Pricing page — ViewContent event |
| `UI/src/app/components/dashboard/file-upload-section/file-upload-section.component.ts` | Photo upload — Lead event |
| `API/Controllers/StripeWebhookController.cs` | Stripe webhooks — Conversions API hook point |
| `API/Services/Payments/StripeWebhookService.cs` | Payment processing — Purchase event source |
| `API/Controllers/CreditController.cs` | Credit purchase flow |
| `UI/src/app/pages/privacy/privacy.component.ts` | Privacy Policy page — needs Meta disclosure |
| `UI/src/app/pages/cookies/cookies.component.ts` | Cookie Policy page — needs `_fbp`/`_fbc` listing |
| `UI/src/app/pages/subprocessors/subprocessors.component.ts` | Subprocessors page — needs Meta as subprocessor |
| `_bmad-output/implementation-artifacts/marketing-execution-playbook-master-2025-12.md` | Master playbook — needs Facebook channel integration |

### Technical Decisions

- Cross-post LinkedIn content to Facebook (minimize content creation overhead)
- Adapt existing LinkedIn ad creatives for Meta Ads (don't create from scratch)
- Follow master playbook's phased approach — Facebook becomes a parallel channel alongside LinkedIn
- Meta Pixel must be installed before any ad spend (Phase 0 gate)
- Create a dedicated `MetaPixelService` rather than overloading `AnalyticsService` (separation of concerns — GA4 and Meta have different event schemas)
- Gate Meta Pixel behind `marketing` consent category (already exists in CookieConsentService)
- **(F2 fix)** Server-side Conversions API MUST check stored user consent before sending events to Meta. Hashed email is pseudonymized personal data under GDPR — sending it without consent is a potential Article 6 violation. Store marketing consent state on the user profile and gate the Conversions API call behind it.
- **(F4 fix)** Do NOT hardcode Meta Pixel ID in `index.html`. Instead, dynamically inject the pixel via `MetaPixelService` using the environment config as the single source of truth. This prevents configuration drift between index.html and environment files.
- Add `metaPixelId` to environment configs following the `ga4MeasurementId` pattern

### Facebook Page Current State (Verified via Playwright, 2026-02-11)
- **URL:** facebook.com/aiprofilephotomaker
- **Page Name:** AI Profile Photo Maker
- **Title:** AI Profile Photo Maker | Las Vegas NV
- **Followers:** 2, Following: 0
- **Posts:** 1 (cover photo auto-update, Jan 15 2026 4:44 AM)
- **Category:** Page · Software
- **Location:** Las Vegas, NV, United States, Nevada
- **Phone:** (702) 389-3416
- **Email:** support@aiprofilephotomaker.com
- **Website:** aiprofilephotomaker.com (linked)
- **Hours:** Always open
- **Reviews:** Not yet rated (0 Reviews)
- **Photos:** 4 uploaded (profile pic, cover photo, 2 others)
- **Intro:** "Transform your selfies into professional headshots with AI. Perfect for LinkedIn, resumes, and personal branding. Studio-quality results at a fraction of the cost."
- **CTA Button:** Not configured or not visible to logged-out users
- **Tabs visible:** Posts, About, Photos
- **Meta Pixel:** NOT installed on aiprofilephotomaker.com

## Implementation Plan

### Phase 0: Tracking Foundation (Must complete before any ad spend)

- [ ] Task 1: Create Meta Pixel in Meta Events Manager
  - Action: Log into Meta Business Suite → Events Manager → Connect Data Sources → Web → Create Pixel
  - Name the pixel: "AI Profile Photo Maker"
  - Record the Pixel ID (format: 15-16 digit number)
  - Generate a Conversions API access token with **minimal permissions** (ads_management, ads_read) — do NOT use admin-level tokens
  - Record the access token securely
  - Notes: This is a manual step in the Meta dashboard, not code. The Pixel ID and access token are needed for all subsequent tasks.

- [ ] Task 2: Add `metaPixelId` to environment configuration files and update TypeScript types
  - File: `AI.ProfilePhotoMaker.UI/src/environments/environment.mvp-v1.ts`
  - File: `AI.ProfilePhotoMaker.UI/src/environments/environment.prod.ts`
  - File: `AI.ProfilePhotoMaker.UI/src/environments/environment.ts`
  - File: `AI.ProfilePhotoMaker.UI/src/environments/environment.docker.ts`
  - Action: Add `metaPixelId` field to the `analytics` object in each environment file
  - For production/mvp-v1: set to the actual Pixel ID from Task 1
  - For local/docker: set to empty string `''` (disabled in non-production)
  - **(F13 fix)** If a shared TypeScript type/interface defines the environment shape, update it to include `metaPixelId: string` in the `analytics` object. Check for type definitions before modifying environment files.
  - Example:
    ```typescript
    analytics: {
      ga4MeasurementId: 'G-FYQMYY2PJD',
      metaPixelId: '123456789012345', // production only
    },
    ```

- [ ] Task 3: Add Meta Pixel base code to `index.html` (dynamic injection approach)
  - File: `AI.ProfilePhotoMaker.UI/src/index.html`
  - **(F4 fix)** Do NOT hardcode the Pixel ID in `index.html`. Instead:
    - Add ONLY the `fbevents.js` script loader to `<head>` (after the GA4 gtag block, after line 20):
      ```html
      <!-- Meta Pixel base code (ID injected dynamically by MetaPixelService) -->
      <script async defer src="https://connect.facebook.net/en_US/fbevents.js"></script>
      ```
    - Do NOT call `fbq('init', ...)` in the HTML — this is handled by `MetaPixelService` using the environment config as the single source of truth
    - **(F9 fix)** The `MetaPixelService` MUST call `fbq('consent', 'revoke')` BEFORE `fbq('init', PIXEL_ID)` to prevent the `_fbp` cookie from being set before consent is granted. Under ePrivacy Directive, setting marketing cookies before consent is a violation regardless of whether events are fired.
  - Include the `<noscript>` fallback image tag before `</head>` (uses Pixel ID — this is acceptable as it's a static tracking pixel for non-JS browsers)
  - Notes: This approach ensures the Pixel ID is defined in exactly ONE place (environment config) and eliminates configuration drift between index.html and environment files.

- [ ] Task 4: Create `MetaPixelService`
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/meta-pixel.service.ts` (new file)
  - Action: Create a new Angular service:
    - Injectable, providedIn: 'root'
    - Constructor injects `Router`, `CookieConsentService`, `DOCUMENT`
    - Reads `metaPixelId` from environment config — if empty, service is a complete no-op
    - **(F8 fix)** All calls to `fbq` MUST be wrapped in a null-safe getter method (mirror `AnalyticsService.getGtag()` pattern at lines 106-111). `fbevents.js` WILL be blocked by uBlock Origin, Brave, and Firefox Enhanced Tracking Protection. Without null checks, the app crashes for these users. Implementation:
      ```typescript
      private getFbq(): FbqFunction | null {
        if (typeof window === 'undefined') return null;
        return typeof window.fbq === 'function' ? window.fbq : null;
      }
      ```
    - `init()` method:
      1. Check `metaPixelId` is not empty, otherwise return early
      2. **(F9 fix)** Call `fbq('consent', 'revoke')` FIRST (before init) to prevent premature cookie setting
      3. Call `fbq('init', metaPixelId)` to initialize the pixel
      4. Subscribe to `cookieConsentService.consent$` and check `marketing` category
    - When marketing consent is granted:
      - Call `fbq('consent', 'grant')`
      - **(F1 fix)** ALSO update Google Consent Mode v2 ad signals via gtag:
        ```typescript
        gtag('consent', 'update', {
          ad_storage: 'granted',
          ad_user_data: 'granted',
          ad_personalization: 'granted',
        });
        ```
      - Fire initial `PageView` event
    - When marketing consent is denied/revoked:
      - Call `fbq('consent', 'revoke')`
      - **(F1 fix)** ALSO revoke Google Consent Mode v2 ad signals:
        ```typescript
        gtag('consent', 'update', {
          ad_storage: 'denied',
          ad_user_data: 'denied',
          ad_personalization: 'denied',
        });
        ```
    - Exposes `trackEvent(eventName: string, params?: Record<string, unknown>)` for standard Meta Pixel events
    - Exposes `trackCustomEvent(eventName: string, params?: Record<string, unknown>)` for custom events
    - Tracks `NavigationEnd` router events to fire `PageView` on SPA route changes (only when consent granted)
  - Standard events to support: `PageView`, `ViewContent`, `InitiateCheckout`, `Lead`, `CompleteRegistration`
  - Note: `Purchase` is server-side only via Conversions API — NOT fired client-side (see Task 7)
  - Declare `fbq` function on Window interface (similar to `gtag` declaration in AnalyticsService)

- [ ] Task 5: Initialize `MetaPixelService` in app bootstrap
  - File: `AI.ProfilePhotoMaker.UI/src/app/app.component.ts`
  - Action: Inject `MetaPixelService` and call `init()` in the same location where `AnalyticsService.init()` is called
  - Notes: Follow the exact same initialization pattern as AnalyticsService

- [ ] Task 6: Add frontend conversion events
  - Action: Fire Meta Pixel events at key conversion funnel points. **All file paths are definitive — no hedging.**
    - **`ViewContent`**: When user views the pricing/premium page
      - File: `UI/src/app/pages/premium/premium.component.ts`
      - Trigger: In `ngOnInit()` or equivalent lifecycle hook
      - Params: `{ content_name: 'Premium Credits', content_category: 'Pricing' }`
    - **`InitiateCheckout`**: When user begins Stripe checkout
      - File: `UI/src/app/components/credit-packages/credit-packages.component.ts`
      - Trigger: At line 279, inside the `createPaymentIntent` call (when the payment intent is created successfully, before Stripe Elements mount)
      - Params: `{ value: selectedPackage.price, currency: 'USD', content_name: selectedPackage.name }`
    - **`CompleteRegistration`**: When user completes signup — **TWO paths exist, BOTH need the event:**
      - File 1: `UI/src/app/auth/register/register.component.ts` — email/password registration success handler
      - File 2: `UI/src/app/auth/complete-profile/complete-profile.component.ts` — OAuth (Google) registration, at line 102 inside the `response.success` block
      - Params: `{ content_name: 'User Registration', status: true }`
    - **`Lead`**: When user uploads their first photo (high-intent signal)
      - File: `UI/src/app/components/dashboard/file-upload-section/file-upload-section.component.ts`
      - Trigger: On successful file upload completion
      - Params: `{ content_name: 'Photo Upload' }`
  - Notes: All events respect marketing consent via MetaPixelService. If consent not granted, events are silently dropped (no-op via null-safe fbq getter).

- [ ] Task 7: Add Conversions API backend service with consent gating and failure isolation
  - **New file:** `AI.ProfilePhotoMaker.API/Services/IMetaConversionsService.cs` (interface)
  - **New file:** `AI.ProfilePhotoMaker.API/Services/MetaConversionsService.cs` (implementation)
  - Action: Create a server-side service for Meta Conversions API:
    - **(F2 fix — CRITICAL)** The service MUST check the user's stored marketing consent before sending ANY event to Meta. Hashed email is pseudonymized personal data under GDPR. Implementation:
      - Add a `MarketingConsentGranted` boolean field to the user profile (or leverage existing consent tracking if available)
      - In `MetaConversionsService.SendPurchaseEvent()`, check the user's consent state FIRST. If consent was never granted or was revoked, do NOT send the event.
      - Log a warning when a purchase event is skipped due to missing consent (for debugging, not PII)
    - **(F3 fix — CRITICAL)** Failure isolation:
      - The Conversions API call MUST be fire-and-forget (async, non-blocking). A hanging or failed HTTP call to `graph.facebook.com` MUST NOT block or delay payment webhook processing.
      - Use `Task.Run()` or a background queue (e.g., `IBackgroundTaskQueue`) to decouple the Meta API call from the Stripe webhook handler.
      - Implement a 5-second HTTP timeout on the Meta API call.
      - On failure: log the error and swallow it. Never throw from within the Stripe webhook flow.
    - On purchase success, send to Meta Conversions API:
      - Event: `Purchase`
      - Value: purchase amount
      - Currency: USD
      - User data: SHA-256 hashed email (required for matching)
      - Event ID: payment transaction ID (for potential future client-side dedup if `Purchase` is ever added client-side)
    - HTTP POST to `https://graph.facebook.com/v21.0/{PIXEL_ID}/events`
    - Auth: Conversions API access token from Task 1
  - Notes: `Purchase` is server-side ONLY. There is no client-side `Purchase` event. InitiateCheckout (client) and Purchase (server) are different event types in Meta's taxonomy and are NOT deduplicated against each other — this is by design.

- [ ] Task 8: Add Conversions API configuration and infrastructure
  - **(F6 fix)** This task covers ALL infrastructure needed for Task 7:
  - **8a: Create configuration POCO**
    - New file: `AI.ProfilePhotoMaker.API/Configuration/MetaConversionsApiOptions.cs`
    - Properties: `PixelId` (string), `AccessToken` (string), `Enabled` (bool, default false)
  - **8b: Add to appsettings.json**
    - File: `AI.ProfilePhotoMaker.API/appsettings.json`
    - Add section:
      ```json
      "MetaConversionsApi": {
        "PixelId": "",
        "AccessToken": "",
        "Enabled": false
      }
      ```
  - **8c: Register in DI**
    - File: `AI.ProfilePhotoMaker.API/Program.cs`
    - Add: `services.Configure<MetaConversionsApiOptions>(configuration.GetSection("MetaConversionsApi"));`
    - Add: `services.AddScoped<IMetaConversionsService, MetaConversionsService>();`
    - Add: `services.AddHttpClient<MetaConversionsService>()` with 5-second timeout
  - **8d: Wire into StripeWebhookService**
    - File: `AI.ProfilePhotoMaker.API/Services/Payments/StripeWebhookService.cs`
    - Inject `IMetaConversionsService` and call `SendPurchaseEventAsync()` after successful payment processing (fire-and-forget)
  - **8e: Update validate-secrets.sh**
    - File: `scripts/validate-secrets.sh`
    - Add validation for `META_CONVERSIONS_API_ACCESS_TOKEN` (required in Production when `MetaConversionsApi:Enabled` is true)
    - Add validation for `META_CONVERSIONS_API_PIXEL_ID`
  - **8f: Update deployment configuration**
    - Add `MetaConversionsApi:PixelId` and `MetaConversionsApi:AccessToken` to:
      - GitHub Actions workflow secrets mapping
      - Bicep template environment variables (or Azure Container Apps configuration)
    - **(F3 fix)** Document token rotation: Conversions API tokens should be rotated every 60 days. Add a comment in the config and a note in the deployment docs.

- [ ] Task 9: Verify Meta Pixel installation
  - **Owner:** Developer who implements Tasks 3-6
  - Action: Use Meta Pixel Helper (Chrome extension) and Meta Events Manager to verify:
    - `fbevents.js` loads on all pages without console errors
    - `PageView` fires on page load ONLY when marketing consent is granted
    - `PageView` fires on SPA route changes (when consented)
    - `ViewContent` fires on pricing page visit
    - `InitiateCheckout` fires when starting purchase
    - `Purchase` event appears in Events Manager (from Conversions API, for consented users only)
    - Events are NOT fired when marketing consent is denied
    - `_fbp` cookie is NOT set before marketing consent is granted (F9 verification)
    - Google Consent Mode v2 signals update correctly: `ad_storage`, `ad_user_data`, `ad_personalization` toggle with marketing consent (F1 verification)
    - App does NOT crash or throw errors when ad blockers are active (F8 verification)
  - Notes: Meta Pixel Helper is a free Chrome extension from Meta. Events Manager has a "Test Events" tool for real-time verification.

- [ ] Task 10: Update Privacy Policy, Cookie Policy, and Subprocessors pages
  - **(F12 fix)** Legal compliance for Meta data sharing:
  - **10a: Privacy Policy**
    - File: `UI/src/app/pages/privacy/privacy.component.ts` (or its template)
    - Add disclosure that Meta Pixel collects browsing data for advertising purposes when marketing consent is granted
    - Add that hashed email may be shared with Meta for purchase attribution (Conversions API)
  - **10b: Cookie Policy**
    - File: `UI/src/app/pages/cookies/cookies.component.ts` (or its template)
    - Add `_fbp` (Meta first-party cookie, 90-day expiry, used for ad attribution)
    - Add `_fbc` (Meta click-ID cookie, 90-day expiry, set when user clicks a Facebook ad)
    - Classify both under the "Marketing" consent category
  - **10c: Subprocessors**
    - File: `UI/src/app/pages/subprocessors/subprocessors.component.ts` (or its template)
    - Add Meta Platforms, Inc. as a subprocessor: purpose "Advertising analytics and conversion tracking", data shared "Hashed email, browsing behavior (with consent)", location "United States"

### Phase 1: Facebook Page Optimization

- [ ] Task 11: Configure Facebook Page CTA button
  - Action: In Facebook Page settings → Add Action Button
  - Set CTA to "Sign Up" pointing to `https://aiprofilephotomaker.com?utm_source=facebook&utm_medium=organic&utm_campaign=page_cta`
  - Notes: "Sign Up" aligns with the primary goal of user registration.

- [ ] Task 12: Optimize Facebook Page About section
  - Action: In Facebook Page → Edit Page Info:
    - **Category**: Keep "Software" (accurate)
    - **Additional categories**: Add "Photography" and "Artificial Intelligence"
    - **About (short)**: Keep existing intro (it's good)
    - **Description (long)**: Add expanded description:
      "AI Profile Photo Maker transforms your everyday selfies into studio-quality professional headshots using AI. Upload your photos, choose a style, and get LinkedIn-ready results in minutes — at a fraction of traditional photography costs. Perfect for job seekers, founders, freelancers, and anyone who wants to make a strong first impression online."
    - **Price range**: Set to "$" (affordable positioning)
    - **Products/Services**: Add "AI Headshots", "Professional Photo Enhancement", "LinkedIn Profile Photos"

- [ ] Task 13: Add proof assets to Facebook Page
  - Action: Upload 5-10 before/after transformation images to the Photos section
  - Use only consented proof assets (same assets approved for LinkedIn)
  - Create a photo album called "Transformations" for organized display
  - Notes: A page with visual proof converts better than one with just text. These assets already exist from the LinkedIn creative system.

### Phase 2: Organic Content (Cross-Post from LinkedIn)

- [ ] Task 14: Establish Facebook cross-posting cadence
  - **Owner:** Alan (content owner)
  - Action: After each LinkedIn post (Mon-Wed-Fri), cross-post to Facebook within 2-4 hours
  - Format adaptation rules for Facebook:
    - Keep full LinkedIn post text (Facebook supports long-form)
    - Remove LinkedIn-specific hashtags (#LinkedInProfile) and replace with Facebook-friendly ones
    - Add any relevant images/carousels that accompany the LinkedIn post
    - Keep engagement questions (Facebook algorithm also rewards comments)
  - Suggested hashtags for Facebook: #AIHeadshots #ProfessionalPhoto #HeadshotGenerator #ProfilePhoto #JobSearch
  - Notes: Facebook's algorithm differs from LinkedIn — video and image posts get higher organic reach than text-only. Attach an image whenever possible.

- [ ] Task 15: Backfill Facebook with initial content
  - **Owner:** Alan (content owner)
  - Action: Post 3-5 of the best-performing LinkedIn posts to Facebook immediately
  - Selection criteria: Pick posts with highest engagement (comments > likes) from the LinkedIn content history
  - Space them 1-2 days apart to avoid looking like a content dump
  - Notes: This addresses the "dormant page" problem — visitors who check the page should see recent activity.

### Phase 3: Meta Ads Setup

- [ ] Task 16: Set up Meta Ads Manager campaign structure
  - Action: In Meta Ads Manager, create the following campaign hierarchy:
  - **(F14 fix)** Set account-level spending limit before creating any campaigns. Recommended starting limit: match total LinkedIn monthly budget. This prevents runaway costs from misconfigured campaigns.
  - **Campaign 1: Conversions — Job Seekers (AU)**
    - Objective: Conversions (optimize for Purchase or InitiateCheckout)
    - Daily budget: Start at $10-20/day per ad set (adjust based on LinkedIn baseline)
    - **Ad Set 1A: Job Seekers — Broad**
      - Location: Australia
      - Age: 22-45
      - Interests: Job search, Resume writing, Career development, LinkedIn
      - Placements: Automatic placements (Facebook Feed, Marketplace, Right Column — let Meta optimize)
    - **Ad Set 1B: Job Seekers — Lookalike** (activate after 100+ pixel events)
      - Lookalike audience based on website visitors or purchasers
  - **Campaign 2: Conversions — Founders/Freelancers (AU)**
    - Objective: Conversions
    - Daily budget: $10-20/day per ad set
    - **Ad Set 2A: Founders — Interest-based**
      - Interests: Entrepreneurship, Small business, Freelancing, Personal branding
    - **Ad Set 2B: Founders — Lookalike** (activate after 100+ events)
  - Exclusions (all ad sets): Current customers (upload email list), low-intent audiences
  - **(F10 note)** The Facebook page location is "Las Vegas, NV" while ads target Australia. This is acceptable — Meta does not require page location to match ad targeting geo. However, if Meta flags this, consider adding a secondary page location or noting "Serves customers worldwide" in the page description.
  - Notes: Start with 2-3 creatives per ad set. Don't launch until Pixel is verified (Task 9).

- [ ] Task 17: Create Facebook ad creatives
  - Action: Adapt the 3 existing LinkedIn creative briefs for Facebook format:
  - **Creative 1: Before/After Carousel**
    - Format: Facebook carousel (2-4 cards)
    - Card 1: Original selfie with text overlay "Before"
    - Card 2: Enhanced headshot with text overlay "After"
    - Card 3: CTA card "Get yours in minutes"
    - Headline: "From selfie to hire-ready"
    - Primary text: Adapt LinkedIn Ad A copy — "Interview coming up? Get a studio-quality headshot in minutes."
    - CTA button: "Sign Up"
  - **Creative 2: Single Image (Value)**
    - Format: Single image (1200x628 or 1080x1080)
    - Image: Final headshot centered, price/value messaging
    - Headline: "Studio-quality without the studio"
    - Primary text: Adapt LinkedIn Ad B copy — "Look confident and current. Studio-quality headshots without a studio session."
    - CTA button: "Learn More"
  - **Creative 3: Social Proof**
    - Format: Single image or video (15s loop if available)
    - Image: Grid of multiple transformation examples
    - Headline: "Trusted by professionals"
    - Primary text: "Join thousands upgrading their online presence with AI headshots."
    - CTA button: "Sign Up"
  - Notes: Use only consented proof assets. Facebook image ad specs: 1080x1080 (square) or 1200x628 (landscape). Text on image should be < 20% of area for best delivery.

- [ ] Task 18: Configure UTM tracking for Facebook ads
  - Action: Set up UTM parameters on all ad destination URLs:
  - Standard: `utm_source=facebook&utm_medium=paid&utm_campaign={campaign_name}&utm_content={ad_name}`
  - Use Facebook's dynamic URL parameters where possible: `{{campaign.name}}`, `{{ad.name}}`
  - Example: `https://aiprofilephotomaker.com?utm_source=facebook&utm_medium=paid&utm_campaign=conversions_jobseekers_au&utm_content=beforeafter_carousel_v01`
  - Notes: Must align with the UTM standard in the master playbook: `utm_source`, `utm_medium`, `utm_campaign`, `utm_content`.

### Phase 4: Playbook Integration & Reporting

- [ ] Task 19: Update master marketing playbook with Facebook channel
  - File: `_bmad-output/implementation-artifacts/marketing-execution-playbook-master-2025-12.md`
  - Action: Add a "Facebook / Meta" section parallel to the LinkedIn section:
    - Facebook organic cross-posting cadence (3x/week, mirrors LinkedIn)
    - Meta Ads campaign structure summary
    - Facebook-specific creative guidelines
    - Facebook KPI targets (see Task 20)
    - Update Phase 0 checklist to include: Meta Pixel verified, Facebook CTA configured
    - Update Phase 1 to include: Facebook organic cadence active, Meta Ads campaign created
    - Update Content Calendar to include Facebook cross-posting
    - Update Channel Guardrails with Facebook-specific rules

- [ ] Task 20: Define Facebook KPI targets and reporting cadence
  - Action: Add to the master playbook and tracker:
  - **Facebook Organic KPIs:**
    - Post reach: Baseline + 10% WoW growth
    - Engagement rate: > 2% (comments + reactions / reach)
    - Page followers: Track WoW growth
    - Link clicks to website: Track weekly
  - **Meta Ads KPIs (aligned with LinkedIn targets):**
    - CTR: >= 1%
    - Checkout-start rate: >= 3%
    - Purchase rate: >= 1.5%
    - CPA (cost per acquisition): Track and compare to LinkedIn CPA
    - ROAS (return on ad spend): Track weekly
  - **Pause rules (same as LinkedIn):**
    - If CTR < 0.7% for 3 days → rotate creative
    - If purchase rate > 2% for 3 days → increase budget 20%
    - If creative CTR drops 30% WoW → replace top 2 creatives
  - **Reporting cadence:**
    - Daily: CTR, checkout-start, purchase rate by creative ID
    - Weekly: Top 2 creatives, underperformers, next tests, Facebook vs LinkedIn comparison

- [ ] Task 21: Add Facebook tasks to marketing execution tracker
  - File: `_bmad-output/implementation-artifacts/marketing-execution-tracker-template-2025-12.md`
  - **(F15 fix)** Phase labels now match Implementation Plan phases:
  - Action: Add seed rows for Facebook tasks:
    - FB-001: Install Meta Pixel on website (Phase 0, P0)
    - FB-002: Configure Facebook Page CTA button (Phase 1, P1)
    - FB-003: Upload proof assets to Facebook Page (Phase 1, P1)
    - FB-004: Backfill 3-5 posts to Facebook (Phase 2, P1)
    - FB-005: Establish cross-posting cadence (Phase 2, P1)
    - FB-006: Create Meta Ads campaigns (Phase 3, P0)
    - FB-007: Launch Meta Ads — Job Seekers AU (Phase 3, P0)
    - FB-008: Launch Meta Ads — Founders AU (Phase 3, P1)
    - FB-009: Set up daily KPI logging for Facebook (Phase 4, P1)
    - FB-010: Update Privacy/Cookie/Subprocessors pages (Phase 0, P0)

### Acceptance Criteria

**Meta Pixel Installation:**
- [ ] AC 1: Given a user visits aiprofilephotomaker.com and accepts marketing cookies, when the page loads, then the Meta Pixel `PageView` event fires and appears in Meta Events Manager.
- [ ] AC 2: Given a user visits aiprofilephotomaker.com and rejects marketing cookies, when the page loads, then no Meta Pixel events fire and the `_fbp` cookie is NOT set.
- [ ] AC 3: Given a user navigates between pages within the SPA (e.g., home → pricing → dashboard), when each route change completes, then a new `PageView` event fires for each page (if marketing consent is granted).
- [ ] AC 4: Given Meta Pixel base code is loaded, when the page loads but consent has not been granted, then `fbevents.js` is loaded but no events fire AND the `_fbp` cookie is NOT set (verified via browser DevTools Application tab).

**Google Consent Mode v2 Integration (F1 fix):**
- [ ] AC 5: Given a user grants marketing consent, when the `MetaPixelService` processes the consent change, then `gtag('consent', 'update')` is called with `ad_storage: 'granted'`, `ad_user_data: 'granted'`, `ad_personalization: 'granted'`.
- [ ] AC 6: Given a user revokes marketing consent, when the `MetaPixelService` processes the consent change, then `gtag('consent', 'update')` is called with `ad_storage: 'denied'`, `ad_user_data: 'denied'`, `ad_personalization: 'denied'`.

**Conversion Events:**
- [ ] AC 7: Given a consented user views the pricing/premium page, when the page component initializes, then a `ViewContent` event fires with `content_name: 'Premium Credits'`.
- [ ] AC 8: Given a consented user clicks to begin Stripe checkout, when `createPaymentIntent` succeeds in `credit-packages.component.ts`, then an `InitiateCheckout` event fires with the package value and currency.
- [ ] AC 9: Given a user completes registration via email/password, when signup succeeds, then a `CompleteRegistration` event fires. Given a user completes registration via Google OAuth, when `complete-profile` succeeds (line 102), then a `CompleteRegistration` event also fires.
- [ ] AC 10: Given a consented user completes a purchase via Stripe, when `payment_intent.succeeded` is processed AND the user has marketing consent stored, then a `Purchase` event is sent to Meta Conversions API with the correct value, currency, and SHA-256 hashed email.
- [ ] AC 11: Given a user completes a purchase but has NOT granted marketing consent, when `payment_intent.succeeded` is processed, then NO `Purchase` event is sent to Meta Conversions API.

**Ad-Blocker Resilience (F8 fix):**
- [ ] AC 12: Given a user has an ad blocker active (uBlock Origin, Brave, Firefox ETP), when they browse the site, then the app functions normally with no console errors related to `fbq` being undefined.

**Consent Integration:**
- [ ] AC 13: Given the cookie consent banner is shown, when the user opens preferences, then a "Marketing" toggle is visible and functional.
- [ ] AC 14: Given a user initially grants marketing consent and then later revokes it via settings, when consent is revoked, then Meta Pixel stops firing events on subsequent page views AND Google Consent Mode ad signals revert to denied.

**Failure Isolation (F3 fix):**
- [ ] AC 15: Given the Meta Conversions API is unreachable (timeout, 500 error), when a purchase is processed, then the Stripe webhook completes successfully regardless. The Meta API failure is logged but does not affect payment processing.

**Legal Compliance (F12 fix):**
- [ ] AC 16: Given the Privacy Policy page, when a user reads it, then it discloses Meta Pixel data collection and Conversions API data sharing.
- [ ] AC 17: Given the Cookie Policy page, when a user reads it, then `_fbp` and `_fbc` cookies are listed under the Marketing category.
- [ ] AC 18: Given the Subprocessors page, when a user reads it, then Meta Platforms, Inc. is listed as a subprocessor.

**Facebook Page:**
- [ ] AC 19: Given a visitor lands on the Facebook page, when they view the page, then they see a "Sign Up" CTA button linking to aiprofilephotomaker.com.
- [ ] AC 20: Given the Facebook page Photos section, when a visitor browses photos, then they see at least 5 before/after transformation examples in a "Transformations" album.

**Organic Content:**
- [ ] AC 21: Given the cross-posting cadence is established, when reviewing Facebook page posts after one week, then at least 3 posts are present matching the LinkedIn Mon-Wed-Fri cadence. (Operational verification, not automated.)

**Meta Ads:**
- [ ] AC 22: Given Meta Ads campaigns are created, when the campaigns are reviewed, then each ad set has correct targeting (geo: Australia, age: 22-45, relevant interests), exclusions (current customers), and a daily budget cap.
- [ ] AC 23: Given Meta Ads are running for 3+ days, when daily KPIs are reviewed, then CTR, checkout-start, and purchase rate data is available per creative ID.

## Additional Context

### Dependencies

**External Services (must be set up before code changes):**
- Meta Business Suite account linked to Facebook page `facebook.com/aiprofilephotomaker`
- Meta Pixel ID generated from Meta Events Manager (Task 1)
- Meta Conversions API access token generated from Meta Events Manager (Task 1) — minimal permissions, not admin
- Meta Ads Manager account (for campaign creation in Phase 3)

**Internal Code Dependencies:**
- `CookieConsentService` with `marketing` category — already exists, no changes needed
- Cookie consent UI with marketing toggle — already exists, no changes needed
- `AnalyticsService` — used as reference pattern, no changes needed (but MetaPixelService calls gtag for consent mode signals)
- Stripe webhook processing — already exists, extend with Conversions API call
- User profile model — may need `MarketingConsentGranted` field for server-side consent gating

**Infrastructure:**
- Conversions API access token stored as a secret (same pattern as Stripe webhook secret)
- `MetaConversionsApi:PixelId` and `MetaConversionsApi:AccessToken` in appsettings / environment variables
- `scripts/validate-secrets.sh` updated with META_CONVERSIONS_API_ACCESS_TOKEN and META_CONVERSIONS_API_PIXEL_ID
- GitHub Actions secrets and Bicep deployment config updated
- Token rotation: rotate Conversions API token every 60 days

**Assets:**
- 5-10 consented before/after transformation images (may already exist from LinkedIn)
- Ad creative images in Facebook specs: 1080x1080 (square), 1200x628 (landscape)
- Customer email list for ad exclusions (export from database)

### Testing Strategy

**Unit Tests:**
- `MetaPixelService` unit tests (`meta-pixel.service.spec.ts`):
  - Test: Service initializes without error when `metaPixelId` is empty (local dev — complete no-op)
  - Test: `trackEvent` is a no-op when consent is not granted
  - Test: `trackEvent` calls `fbq` when consent is granted
  - Test: Consent revocation stops event firing
  - Test: Route changes trigger `PageView` when enabled
  - Test: Service does not throw when `window.fbq` is undefined (ad-blocker scenario — F8)
  - Test: `gtag('consent', 'update')` is called with `ad_storage: 'granted'` when marketing consent granted (F1)
  - Test: `gtag('consent', 'update')` is called with `ad_storage: 'denied'` when marketing consent revoked (F1)
  - Test: `fbq('consent', 'revoke')` is called BEFORE `fbq('init')` during initialization (F9)
- `MetaConversionsService` unit tests (backend):
  - Test: Purchase event sends correct payload to Meta API
  - Test: User email is SHA-256 hashed before sending
  - Test: Service handles API errors gracefully (doesn't break payment flow — returns without throwing)
  - Test: Service is a no-op when Pixel ID / access token are not configured
  - Test: Service does NOT send event when user has not granted marketing consent (F2)
  - Test: Service respects 5-second HTTP timeout
  - Test: Service runs asynchronously and does not block the calling method

**E2E Tests (Playwright):**
- Test: Meta Pixel script loads on page without console errors
- Test: Cookie consent banner marketing toggle controls pixel behavior
- Test: No network requests to `connect.facebook.net/en_US/fbevents.js` when... (Note: the script tag is always loaded, but no requests to `facebook.com/tr` should occur without consent)
- Test: App functions normally with no console errors when ad blocker is simulated (block `connect.facebook.net` via route)

**Manual Verification:**
- Meta Pixel Helper Chrome extension confirms events fire correctly
- Meta Events Manager test events tool shows incoming events
- Meta Events Manager shows server events (Conversions API) alongside browser events
- `_fbp` cookie NOT present in browser before marketing consent granted
- App tested with uBlock Origin, Brave Shield, and Firefox ETP active — no crashes

### Notes

**Adversarial Review Findings Resolved:**
- F1 (Critical): Google Consent Mode v2 `ad_*` signals now updated by MetaPixelService
- F2 (Critical): Server-side Conversions API now gated behind stored user consent
- F3 (Critical): Token rotation documented, failure isolation via fire-and-forget, validate-secrets.sh updated, permissions scoped
- F4 (High): Pixel ID dynamically injected from environment config, not hardcoded in index.html
- F5 (High): All file paths definitive — both registration paths (email + OAuth) specified
- F6 (High): Full infrastructure pipeline: appsettings, POCO, DI, Bicep, GitHub Actions, validate-secrets.sh
- F7 (High): Deduplication claim removed — InitiateCheckout (client) and Purchase (server) are different events by design
- F8 (High): Explicit null-safety requirement for fbq, mirroring getGtag() pattern
- F9 (Medium): `fbq('consent', 'revoke')` called before `fbq('init')` to prevent premature cookies
- F12 (Medium): Privacy Policy, Cookie Policy, Subprocessors pages all updated
- F13 (Medium): TypeScript interface update included in Task 2
- F15 (Low): Tracker Phase labels corrected to match Implementation Plan

**Remaining Low/Medium Findings (Accepted Risk):**
- F10 (Medium): Las Vegas page location vs Australia ads — noted in Task 16, acceptable per Meta policy
- F11 (Medium): Cross-posting is manual — accepted as operational process, not automated
- F14 (Medium): Ad spend cap now specified ($10-20/day per ad set + account-level limit)
- F16 (Low): Manual verification has no CI/CD path — accepted, pixel verification is inherently manual
- F17 (Low): CSP consideration noted for future — not blocking current implementation

**Known Limitations:**
- Facebook organic reach for business pages is typically 2-5% of followers — paid is the primary driver
- Lookalike audiences require 100+ source users (from Pixel data) to be effective — will take time to build
- Conversions API requires user consent AND hashed email — anonymous or non-consented purchases cannot be attributed server-side
- `MarketingConsentGranted` field may require a database migration if not already present on user profile

**Future Considerations (Out of Scope):**
- Facebook Reels for video content (when/if video assets are produced)
- Instagram Ads via Meta Ads Manager (when Instagram channel is activated)
- Advanced Conversions API integration with more event types
- Custom audiences from CRM data
- A/B testing landing pages specifically for Facebook traffic
- Facebook Pixel server-side via Google Tag Manager Server Container (for cookie-less tracking)
- Content Security Policy (CSP) headers: if CSP is ever implemented, `connect.facebook.net` and `facebook.com/tr` must be whitelisted
