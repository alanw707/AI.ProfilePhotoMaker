# Admin product health grill decisions — 2026-06-09

## Decisions

1. Scope is **whole admin area plus a new Product Health admin page**.
   - Keep existing admin operations dashboard useful for users, credits, coupons, campaigns, and audit logs.
   - Add Product Health as the pivot-specific surface for funnel, package, and provider health.

2. Product Health serves **both founder/operator and support/debug decisions**.
   - First section: business funnel and pivot viability.
   - Second section: operational health and support/debug queues.

3. Default time window is **selectable, default 7 days**.
   - Include 24h, 7d, 30d, and all-time.
   - Default 7d balances low-volume product signal with operational recency.

4. Canonical V1 funnel is **upload → preview generated → paid package purchased → export downloaded**.
   - Do not make visit/landing the V1 funnel start because analytics/cookie-consent reliability is a separate concern.
   - Add landing/CTA metrics later when analytics trust is established.

5. Top Product Health metrics:
   - Uploads
   - Successful Free Preview generations
   - Preview generation success rate
   - Preview-to-paid conversion rate
   - Starter purchases
   - Pro purchases
   - Export downloads
   - Median / p95 generation time
   - OpenAI vs Replicate usage split

6. Existing `/admin/dashboard` changes:
   - Keep total users and active users.
   - Move credit metrics lower or into a finance/support group.
   - Add a small Product Health summary card and link.
   - Update copy away from credit-first positioning.

7. Whole admin area changes:
   - User list: add pivot-aware filters later, not V1 table bloat.
   - User detail: add package entitlements, preview state, upgrade path, candidate/refinement/augmentation consumption, export eligibility, provider/model, and recent generation failures.
   - Campaigns: add segments for preview abandoners, failed generation recovery, unused entitlements, Starter upgrade candidates, and Pro upsell candidates.
   - Coupons: connect coupons to outcome package promotion where supported.
   - Audit log: ensure package/entitlement/admin credit adjustments remain traceable.

8. Product Health page sections:
   - Funnel health
   - Package mix and revenue proxy
   - Package fulfillment
   - Provider/model health
   - Failure/support queues
   - Replicate retirement signal

9. Replicate retirement signal requires:
   - OpenAI success rate acceptable
   - OpenAI median/p95 latency acceptable
   - Low advanced custom photoshoot usage
   - Low fallback use
   - Quality/support complaints not worse than baseline

10. V1 implementation should prefer existing persisted data and structured logs before adding a new analytics dependency.
    - If a metric cannot be computed reliably, show unavailable/empty state rather than fake precision.

11. New signups should no longer receive the legacy **25-credit signup grant**.
    - Free Preview replaces free signup credits as the onboarding value path.
    - Existing paid package and admin credit-adjustment flows remain available.
    - Product Health should expose zero-credit/new-user states clearly so support can distinguish intentional no-grant behavior from credit provisioning bugs.

## Documentation updates made

- Added glossary terms to `CONTEXT.md`:
  - Product health
  - Product funnel
  - Package fulfillment
- Added ADR:
  - `docs/adr/0004-product-health-beside-admin-operations.md`

## Implementation recommendation

Build in this order:

1. Backend DTO and service for Product Health summary using existing data.
2. New `/admin/product-health` route and nav item.
3. Product Health page with time-window selector and empty states.
4. Update existing admin overview copy and add Product Health summary card.
5. Remove the legacy 25-credit signup grant from new-user creation paths.
6. Extend user detail with package entitlement, generation provider context, and intentional no-grant/zero-credit state.
7. Add campaign/user filters once the underlying query contracts are stable.
