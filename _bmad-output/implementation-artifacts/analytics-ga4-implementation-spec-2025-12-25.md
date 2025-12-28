---
docType: analytics-implementation-spec
date: 2025-12-25
owner: Codex (party-mode)
status: draft
---

# GA4 Analytics Implementation Spec

## Goals
- Capture reliable page-view data for SEO launch pages.
- Attribute conversion funnel: free enhancer start -> paid purchase.
- Respect user consent for analytics cookies.

## Configuration
- Environment key: `environment.analytics.ga4MeasurementId`.
- Development default: empty string (no tracking).
- Production: set GA4 Measurement ID (e.g., `G-XXXXXXXXXX`).
- Google tag snippet is embedded in `AI.ProfilePhotoMaker.UI/src/index.html`.

## Consent model
- Analytics only load after the user opts in via Cookie Preferences.
- Consent is stored in `cookie-consent-v1` and can be revoked at any time.
- When revoked, analytics storage is denied and tracking stops.

## Implementation summary
- Google tag snippet added to `AI.ProfilePhotoMaker.UI/src/index.html`.
- New service: `AI.ProfilePhotoMaker.UI/src/app/services/analytics.service.ts`.
- Tracks SPA route changes using `NavigationEnd`.
- Sends page views via `gtag('config', measurementId, { page_path })`.

## Event taxonomy (future instrumentation)
- `page_view` (auto via SPA route changes).
- `enhancer_start` (when user clicks or submits the free enhancer flow).
- `enhancer_complete` (when a preview/result is rendered).
- `signup_start` / `signup_complete` (auth flow).
- `purchase_start` / `purchase_complete` (checkout flow).

## Required UI instrumentation points
- Free enhancer CTA buttons and form submit.
- Sign-up/login button and success callback.
- Checkout initiation and success confirmation.

## Verification steps
1. Set GA4 Measurement ID in `environment.prod.ts`.
2. Open the app in a browser, accept analytics cookies.
3. Confirm `gtag` is loaded (`window.gtag` exists) and events appear in GA DebugView.
4. Navigate between routes and verify page_view events.

## Compliance updates
- Cookie Policy updated to mention GA4 analytics and consent.
- Privacy Policy updated to mention GA4 data and opt-out controls.
