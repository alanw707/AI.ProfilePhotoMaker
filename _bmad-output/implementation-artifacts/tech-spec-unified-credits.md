# Tech-Spec: Unified Credits System

**Created:** 2026-01-04
**Status:** In Progress

## Overview

### Problem Statement
The app currently splits credits into weekly free credits and purchased credits. Weekly credits can only be used for enhancements, while training and headshot generation require purchased credits. This creates a confusing paywall and duplicated credit logic across API and UI. Users already must purchase credits for training (15 credits), so the weekly-only restriction adds friction without real value.

### Solution
Unify credits into a single balance called Credits. All operations (enhancement, training, generation) consume from the same pool. Weekly top-up remains, but only to restore the balance to 5 if total credits are below 5. Credits never expire and roll over. All UI copy and API responses should reflect a single credit balance; remove references to free vs purchased credits and headshot eligibility rules.

### Scope (In/Out)

**In scope**
- Merge weekly and purchased credits into a single balance in the data model.
- Update credit consumption, refund, and weekly reset logic to use the unified balance.
- Remove purchased-only gating for training and generation.
- Update API responses and UI types to expose a single Credits value.
- Update UI copy and credit display to remove weekly vs purchased wording.
- Update tests that assume two credit buckets.

**Out of scope**
- Pricing changes to credit packages.
- Subscription tier changes or new plans.
- Changes to credit costs (still 1 enhancement, 15 training, 5 generation per image).

## Context for Development

### Codebase Patterns
- Credit logic centralized in `BasicTierService` with weekly reset background job.
- Controllers currently enforce purchased-only checks for training and generation.
- UI uses `CreditService` and dashboard state to calculate weekly vs purchased and enforce paywalls.
- Copy and SEO pages mention free weekly enhancement credits and headshot paywalls.

### Files to Reference
- API: `AI.ProfilePhotoMaker.API/Models/UserProfile.cs`
- API: `AI.ProfilePhotoMaker.API/Services/BasicTierService.cs`
- API: `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs`
- API: `AI.ProfilePhotoMaker.API/Controllers/CreditController.cs`
- API: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`
- API: `AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs`
- API: `AI.ProfilePhotoMaker.API/Services/TrainingPollingService.cs`
- UI: `AI.ProfilePhotoMaker.UI/src/app/services/credit.service.ts`
- UI: `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.ts`
- UI: `AI.ProfilePhotoMaker.UI/src/app/services/workflow-orchestration.service.ts`
- UI: `AI.ProfilePhotoMaker.UI/src/app/components/dashboard/credit-display/*`
- UI: `AI.ProfilePhotoMaker.UI/src/app/components/settings/credit-management/*`
- UI: `AI.ProfilePhotoMaker.UI/src/app/pages/landing/*`
- UI: `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts`
- UI: `AI.ProfilePhotoMaker.UI/src/index.html`
- UI: `AI.ProfilePhotoMaker.UI/public/manifest.json`
- UI: `AI.ProfilePhotoMaker.UI/public/free-headshot-enhancer/index.html`
- Tests: `AI.ProfilePhotoMaker.API.Tests/Services/BasicTierServiceTests.cs`
- Tests: `AI.ProfilePhotoMaker.API.Tests/Integration/*`
- Tests: `tests/e2e/*credit*`

### Technical Decisions
- Weekly top-up uses Option A: if total credits < 5, set credits to 5 on reset; otherwise leave unchanged.
- Remove weekly vs purchased buckets from the API model and UI. Expose a single Credits count.
- Keep `LastCreditReset` and `NextResetDate` to communicate weekly top-up timing.
- Cost config remains the same, but no operation is restricted to a specific bucket.

## Implementation Plan

### Tasks
- [x] Data model: merge `PurchasedCredits` into `Credits` for all users and remove `PurchasedCredits` column via EF migration. Update DTOs and repository mappings to single balance.
- [x] BasicTierService: simplify to single balance. Remove `CanUseWeeklyCredits` gating, remove weekly vs purchased breakdown, and update refund logic to add credits back to the single balance. Weekly reset should only top up to 5 if below.
- [x] CreditCostConfig: remove `CanUseWeeklyCredits` or return true for all operations; update `CreditController.GetCreditCosts` response accordingly.
- [x] ReplicateController: replace purchased-only checks for training/generation with total credits checks; update error messages to refer to total credits.
- [x] CreditController: update `GET /api/credit/status` response to return a single credits balance plus reset dates (no weekly/purchased split). Update purchase response to return updated total credits only.
- [x] Profile export and stats: remove purchased/weekly fields from profile export and stats DTOs.
- [x] UI credit logic: refactor `CreditService`, dashboard, and workflow orchestration to use total credits only. Remove headshot eligibility logic and any purchased-only paywall checks.
- [x] UI components: update credit display and settings components to show a single Credits value and reset timing; remove weekly/purchased breakdown UI.
- [x] Copy and SEO: remove mentions of free weekly enhancement credits and headshot paywalls from landing, SEO pages, meta tags, and public static pages.
- [x] Tests: update unit/integration/e2e tests to use unified credits and adjust assertions for new API responses.

#### Review Follow-ups (AI)
- [ ] [AI-Review][High] Enforce credit checks + deductions in profile generation to prevent zero-cost image generation. [AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs:208]
- [ ] [AI-Review][High] Align enhancement credit cost with unified pricing (1 credit) and update messaging. [AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs:205]
- [ ] [AI-Review][Medium] Make credit consumption atomic to avoid overspend during concurrent requests. [AI.ProfilePhotoMaker.API/Services/BasicTierService.cs:115]
- [ ] [AI-Review][Medium] Remove remaining "free enhancements/free credits" marketing copy to match unified credits messaging (landing + SEO + static free-headshot page). [AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts:401]
- [ ] [AI-Review][Low] Update docs credit cost examples to match current pricing (1/15/5). [docs/operations/CREDIT_SYSTEM.md:19]

### Acceptance Criteria
- [x] `GET /api/credit/status` returns a single credits balance and reset dates; no weekly/purchased fields.
- [x] Training and styled generation can use any available credits; no purchased-only gating remains.
- [x] Weekly reset tops up credits to 5 only when total credits are below 5; credits roll over indefinitely.
- [x] UI shows a single credits balance with updated messaging; no mention of weekly vs purchased credits.
- [x] Landing/SEO copy no longer states weekly credits are enhancement-only or headshot generation requires purchased credits.
- [x] All relevant tests pass with updated expectations.

## Additional Context

### Dependencies
- EF Core migration to merge and drop `PurchasedCredits` column.
- Coordinated API/Angular contract changes for credit status DTOs.

### Testing Strategy
- `dotnet test AI.ProfilePhotoMaker.API.Tests` with updated BasicTierService and controller tests.
- UI unit/integration tests for dashboard credit display and workflow gating.
- Playwright e2e flows in `tests/e2e` for credit purchase and enhancement.

### Notes
- Priority: ASAP.
- Keep log messaging consistent with single credits balance (no weekly/purchased breakdown).

## Review Notes
- Adversarial review completed.
- Findings: 10 total, 10 fixed, 0 skipped.
- Resolution approach: auto-fix.
