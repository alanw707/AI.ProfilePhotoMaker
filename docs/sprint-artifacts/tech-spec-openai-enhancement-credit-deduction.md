# Tech-Spec: Fix OpenAI Enhancement Credit Deduction Persistence

**Created:** 2025-12-20
**Status:** Ready for Development

## Overview

### Problem Statement
OpenAI enhancement styles (e.g., `pixar_3d`) via `POST /api/enhancement/enhance` return success but user weekly credits remain unchanged in `UserProfiles`. Production evidence shows a `photo_enhancement` usage log with `CreditsRemaining=3`, yet the profile row still reports `Credits=5` with no `UpdatedAt` change. Replicate enhancement (`/api/replicate/enhance`) deducts credits correctly. This causes revenue leakage and inconsistent credit balances.

### Solution
Harden credit consumption persistence in `BasicTierService` so credit deductions are atomically written to `UserProfiles` and validated. Keep OpenAI enhancements at 2 weekly credits. Add regression tests for OpenAI enhancement success/refund and ensure Replicate enhancement remains unchanged.

### Scope (In/Out)

**In Scope**
- Persist credit deductions atomically in `BasicTierService`.
- OpenAI enhancement path uses the same hardened credit consumption logic.
- Logging/guardrails when persistence fails (rows affected != 1).
- Tests for OpenAI enhancement deduction and refund behavior.
- Confirm Replicate enhancement still deducts 1 weekly credit.

**Out of Scope**
- UI changes or credit display updates.
- Pricing changes (OpenAI remains 2 credits).
- Cancellation flows or new refund policies.
- Email verification flow changes.

## Context for Development

### Codebase Patterns
- Credits handled via `IBasicTierService` / `BasicTierService`.
- Enhancement endpoints:
  - Replicate: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs` (`/api/replicate/enhance`)
  - OpenAI: `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs` (`/api/enhancement/enhance`)
- UI routing for OpenAI styles in `AI.ProfilePhotoMaker.UI/src/app/services/replicate.service.ts`.
- Credit costs defined in `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs`.
- Usage logging in `BasicTierService.LogUsageAsync`.

### Files to Reference
- `AI.ProfilePhotoMaker.API/Services/BasicTierService.cs`
- `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`
- `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`
- `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs`
- `AI.ProfilePhotoMaker.API.Tests/`

### Technical Decisions
- Use EF Core `ExecuteUpdateAsync` (net8) to persist `Credits`, `PurchasedCredits`, and `UpdatedAt` in a single atomic update.
- Validate `rowsAffected == 1`. If not, return a failed consumption result and surface `CreditConsumptionFailed`.
- Keep action name `photo_enhancement` and OpenAI cost at 2 weekly credits.
- Preserve existing refund flow via `RefundCreditsAsync`.

## Implementation Plan

### Tasks
- [ ] Update `BasicTierService.ConsumeCreditsInternalAsync` to persist credit changes using `ExecuteUpdateAsync` (or equivalent atomic update) and validate rows affected.
- [ ] Ensure remaining credits used for logging match the persisted values.
- [ ] Log and return failure when persistence does not affect exactly one row.
- [ ] Add integration tests for OpenAI enhancement credit deduction and refund paths.
- [ ] Add regression test confirming Replicate enhancement still deducts 1 weekly credit.

### Acceptance Criteria
- [ ] OpenAI enhancement (`/api/enhancement/enhance`) deducts 2 weekly credits on success and the DB reflects the change.
- [ ] OpenAI enhancement failure refunds credits; DB returns to pre-call state.
- [ ] Replicate enhancement (`/api/replicate/enhance`) continues deducting 1 weekly credit.
- [ ] Email verification guard remains unchanged and functional.
- [ ] If persistence fails, API returns `CreditConsumptionFailed` and logs an error.

## Additional Context

### Dependencies
- EF Core `ExecuteUpdateAsync` (net8). If unsupported in environment, implement explicit update with row-count validation.

### Testing Strategy
- Integration test: OpenAI enhancement success -> credits -2, usage log created.
- Integration test: OpenAI enhancement failure -> credits restored.
- Regression test: Replicate enhancement success -> credits -1.

### Notes
- Production evidence: `UsageLogs` show `photo_enhancement` consumption for OpenAI (2 credits), but `UserProfiles.Credits` remains 5.
