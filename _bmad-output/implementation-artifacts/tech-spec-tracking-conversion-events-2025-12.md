---
docType: tech-spec
date: 2025-12-30
owner: Alan
status: draft
---

# Tracking + Conversion Events Tech Spec

## Goals
- Capture a reliable conversion funnel for paid/organic attribution.
- Standardize event names and payloads across GA4 and ad platforms.
- Preserve privacy and consent gating (analytics vs marketing).

## Non-goals
- Building new marketing pages or copy.
- Server-side tracking or offline conversion uploads.

## Dependencies
- `_bmad-output/implementation-artifacts/analytics-ga4-implementation-spec-2025-12-25.md`
- Existing CTA event: `seo_cta_click` in `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.ts`.
- Cookie consent model in `AI.ProfilePhotoMaker.UI/src/app/services/cookie-consent.service.ts`.

## Event Taxonomy (Client)

### GA4 (Analytics consent required)
| Event | Trigger | Required Params | Notes |
| --- | --- | --- | --- |
| page_view | SPA route change | page_path, page_location, page_title | Already implemented in AnalyticsService |
| seo_cta_click | Primary SEO CTA click | page, position, label, destination, utm_* | Already implemented |
| enhancer_start | Free enhancer flow start | entry_point, utm_* | Custom event |
| enhancer_complete | Free enhancer result rendered | result_count, utm_* | Custom event |
| sign_up | Account created | method, utm_* | GA4 recommended event |
| begin_checkout | Checkout initiated | value, currency, utm_* | GA4 recommended event; treat as checkout-start in reporting |
| purchase | Purchase confirmed | transaction_id, value, currency, items?, utm_* | GA4 recommended event |

### Ad Pixels (Marketing consent required)
| Platform | Event | Trigger | Notes |
| --- | --- | --- | --- |
| Meta Pixel | Purchase | Payment success | Send value + currency |
| LinkedIn Insight Tag | Conversion | Payment success | Configure conversion rule by URL or event |

## UTM + Attribution
- Capture utm_source, utm_medium, utm_campaign, utm_content, utm_term on first entry.
- Persist in sessionStorage for the session.
- Merge UTM params into all funnel events where possible.

## Implementation Plan (UI)
1. Add a small `TrackingContextService` that reads UTMs from URL and stores them for the session.
2. Extend `AnalyticsService` with a helper to attach UTMs by default (or wrap in a `trackWithContext`).
3. Instrument the funnel:
   - Free enhancer: start + complete.
   - Signup: sign_up on successful account creation.
   - Checkout: begin_checkout when user enters payment step.
   - Purchase: purchase on success/confirmation page.
4. Add Meta Pixel + LinkedIn Insight Tag snippets gated by marketing consent.
5. Add environment config for pixel IDs (production only), default empty in dev.

## Acceptance Criteria
- Events visible in GA4 DebugView with consent enabled.
- `begin_checkout` fires once per checkout start and includes value + currency.
- `purchase` fires once per successful purchase and includes transaction_id.
- Pixel tags do not load when marketing consent is denied.

## Testing Notes
- Manual verify in GA4 DebugView.
- Use Meta Pixel Helper + LinkedIn Tag Inspector to confirm events.
- Confirm UTMs are attached to conversion events.

## Open Questions
- Source of truth for purchase value and transaction_id (client or API response?).
- Exact component(s) for checkout start and success in UI (identify at implementation time).
