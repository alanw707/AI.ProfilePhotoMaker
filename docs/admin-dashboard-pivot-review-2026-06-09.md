# Admin dashboard pivot review — 2026-06-09

## Scope

Reviewed admin dashboard surfaces against the current product pivot from credit/headshot-volume mechanics toward instant profile-photo workflow outcomes, preview-first upgrade, and outcome packages.

Reviewed files:

- `CONTEXT.md`
- `docs/openai-images-2-pivot-implementation-plan.md`
- `docs/adr/0001-profile-photo-workflow-outcomes-over-headshot-volume.md`
- `docs/adr/0002-instant-portrait-styles-with-existing-prompts.md`
- `docs/adr/0003-preview-first-upgrade-flow-with-candidate-reuse.md`
- `AI.ProfilePhotoMaker.UI/src/app/admin/admin-dashboard/admin-dashboard.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.html`
- `AI.ProfilePhotoMaker.API/Models/DTOs/AdminDtos.cs`
- `AI.ProfilePhotoMaker.API/Services/AdminService.cs`

## Current admin dashboard state

The admin dashboard is still a general operations panel:

- Total users
- Active users
- Credits outstanding
- Credits purchased
- Active coupons
- Quick links to users, coupons, audit log, and campaigns

The backend dashboard DTO currently exposes only:

- `TotalUsers`
- `ActiveUsers`
- `TotalCreditsOutstanding`
- `TotalCreditsPurchased`
- `ActiveCoupons`

User diagnostics show useful account-level facts:

- Credits
- Usage balance
- Last activity
- Total / uploaded / generated images
- Purchases
- Last upload / generation
- Recent images
- Recent purchases
- Credit history
- Activity history
- Recent admin actions

## Pivot requirements that affect admin

The pivot changes what operators need to observe:

- Product is now outcome-package led, not raw headshot-volume led.
- Free Preview is the primary entry point.
- Starter and Pro packages are the paid conversion path.
- Free Preview candidates can be promoted into paid candidates to reduce provider cost.
- OpenAI instant generation is now primary; Replicate custom training is secondary / advanced.
- Operators need enough telemetry to decide whether Replicate can be retired.
- Metrics called out in pivot docs include upload-to-generation conversion, generation success rate, generation latency, cost per successful output, retry/regeneration rate, download rate, purchase conversion rate, and support/contact rate for quality complaints.

## Findings

### 1. Dashboard KPIs are no longer product-aligned

Current KPIs are credit/account/admin-operation oriented. They do not answer the pivot's core operating questions:

- Are Free Preview users converting to Starter/Pro?
- Which outcome packages sell?
- Are previews being promoted successfully?
- Is OpenAI generation succeeding quickly enough?
- Is provider cost improving after preview reuse?
- Is Replicate usage low enough to retire or reposition?

### 2. Credits remain over-emphasized

Credits are still useful for support and legacy compatibility, but the pivot language is outcome packages and package entitlements. The top-level dashboard still makes credits two of five primary metrics.

The legacy 25-credit grant for each new signup also conflicts with the Free Preview onboarding path. New users should enter through Free Preview, not through a generic credit balance.

Recommended direction: remove the 25-credit signup grant, keep credits in user diagnostics and finance/support sections, and replace top-level credit KPIs with package / funnel metrics.

### 3. No outcome-package admin visibility exists

Outcome package concepts exist in the domain model and docs, but admin overview does not surface:

- Active package definitions
- Free Preview / Starter / Pro purchase counts
- Package entitlement status counts
- Conversion by package
- Upgrade path from preview to paid
- Package fulfillment health

### 4. No provider/model operations visibility exists on overview

Pivot docs mark admin/debug visibility for provider/model as done, but the main dashboard does not expose provider split or model health. Operators still cannot quickly compare OpenAI vs Replicate usage from the dashboard.

### 5. User diagnostics need package context

User detail shows purchases and images, but the labels still center on credits, uploads, generations, and usage balance. For pivot-era support, each user detail page should clearly show:

- Current package entitlement(s)
- Free Preview status
- Starter/Pro upgrade status
- Candidate count generated / included
- Refinements used / included
- Whether preview candidate was reused/promoted
- Provider/model used for recent generations
- Watermark/export eligibility

### 6. Campaigns and coupons may need segmentation updates

Campaigns and coupons remain relevant, but pivot-specific segments are likely needed:

- Preview generated but not upgraded
- Uploaded but generation failed
- Starter purchased but unused refinements remain
- Pro users with unused augmentations
- Advanced training users / Replicate users
- Quality complaint or failed generation cohorts

## Recommended changes

### P0 — Add product-pivot KPI strip to admin overview

Replace or supplement current metrics with:

1. Free Preview starts, last 7 days
2. Successful preview generations, last 7 days
3. Preview-to-paid conversion rate
4. Starter purchases, last 7 days
5. Pro purchases, last 7 days
6. Generation success rate
7. Median / p95 generation time
8. OpenAI vs Replicate usage split

Keep total users and active users. Move credits purchased/outstanding below the fold or into finance/support.

### P1 — Add outcome package health panel

Add an overview panel showing:

- Active outcome packages
- Entitlements by status
- Package revenue / purchase counts by package
- Candidate fulfillment status
- Refinements used vs included
- Preview promotions / reuse count

### P1 — Add provider/model health panel

Add operational visibility:

- Generation count by provider and model
- Failure rate by provider
- Median / p95 latency by provider
- Retry/regeneration rate
- Cost estimate per successful output if available

This directly supports the pivot done criterion: decide whether to retire Replicate based on data.

### P1 — Update user diagnostics for package support

Add package-centric blocks to the user detail page:

- Package entitlements
- Preview status
- Upgrade path
- Candidate/refinement/augmentation consumption
- Export kit eligibility
- Recent generation provider/model
- Recent failures with reason codes

### P2 — Update copy and hierarchy

Change admin overview subtitle from:

> Manage users, credits, coupons, and operational audits.

To pivot-aligned language such as:

> Monitor preview-to-package conversion, generation health, users, promotions, and operational audits.

Change quick action copy for User Management from credit-balance centered to support/diagnostics centered.

### P2 — Add package-aware campaign/coupon shortcuts

Add quick links or filters for:

- Preview abandoners
- Failed generation recovery
- Starter upgrade candidates
- Pro upsell candidates
- Unused entitlement reminders

## Suggested implementation shape

Backend:

- Extend `AdminDashboardDto` with pivot metrics, or create `AdminProductDashboardDto` to avoid bloating legacy admin stats.
- Remove the legacy 25-credit default from new-user/profile creation paths (`UserProfile.Credits` default and registration-created profiles currently assign 25 credits).
- Add queries against outcome package definitions, user entitlements, credit purchases, image generation metadata, and activity/usage logs.
- Keep existing fields for compatibility with current UI tests.

Frontend:

- Add a new `Product Health` section above or beside existing metrics.
- Rename existing `Credits Purchased` / `Credits Outstanding` cards to a secondary `Finance / Credits` section.
- Add package/provider panels with empty states for environments without enough telemetry.

Testing:

- Add API tests for zero-data dashboard state.
- Add API tests for Free Preview / Starter / Pro counts.
- Add API tests for provider split and failed generation counts.
- Add UI tests for empty state and populated product-health state.

## Recommendation

Yes, admin dashboard changes are needed after the pivot.

The current admin dashboard is safe for basic account operations, but it is not sufficient for operating the pivoted product. It should be updated to make preview-to-package conversion, package entitlement health, and generation provider/model health first-class admin signals.
