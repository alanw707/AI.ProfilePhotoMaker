# Tech-Spec: Refund guard on failed generation

**Created:** 2025-12-27
**Status:** Ready for Development

## Overview

### Problem Statement
- Local style generation failures (500) can trigger refund logic even when no credits were charged, increasing user balances.
- Refund calls can be passed a fabricated CreditConsumptionResult (for example, background polling), so refunds are not always tied to a real charge.
- The site is live, so the fix must be safe and minimize regressions.

### Solution
- Require a verifiable charge before refunding by checking UsageLog entries tied to a correlation id.
- Standardize correlation ids for all credit-consuming endpoints (styled generation, batch generation, Replicate enhancement, OpenAI enhancement) and pass them into ConsumeCreditsAsync.
- Ensure refund calls always use the original charge action plus the same correlation id; add idempotency so repeated refunds do not double-credit.

### Scope (In/Out)
In scope:
- API-side refund guard in BasicTierService.
- Correlation id generation and propagation for credit-consuming endpoints.
- Align refund creation in background services to original charge actions.
- Unit tests covering guarded refunds and idempotency.

Out of scope:
- UI changes.
- New payment or credit purchase flows.
- Backfilling historical UsageLog data.
## Context for Development

### Codebase Patterns
- Credit consumption and refund logic lives in BasicTierService and uses UserProfile.Credits and UserProfile.PurchasedCredits.
- Charges are logged in UsageLog with Action and Details; ConsumeCreditsAsync can append correlationId to Details.
- Refunds currently rely on CreditConsumptionResult without verifying a prior charge.

### Files to Reference
- AI.ProfilePhotoMaker.API/Services/BasicTierService.cs
- AI.ProfilePhotoMaker.API/Services/CreditConsumptionResult.cs
- AI.ProfilePhotoMaker.API/Models/UsageLog.cs
- AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs
- AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs
- AI.ProfilePhotoMaker.API/Services/TrainingPollingService.cs
- AI.ProfilePhotoMaker.API/Services/PendingGenerationService.cs
- AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs
- AI.ProfilePhotoMaker.API/Controllers/ModelStatusController.cs (correlationId log pattern)

### Technical Decisions
- Use existing UsageLog records and correlationId markers (Details contains "correlationId=<id>") as the source of truth for whether a charge occurred.
- Add correlation ids at the controller/service layer rather than creating new tables or migrations.
- Refund guard should be idempotent: if a refund for the same correlationId already exists, skip without altering credits.
- When creating refund CreditConsumptionResult for partial or background refunds, use the original charge action (for example, "styled_generation" or "model_training") and the same correlationId.
## Implementation Plan

### Tasks
- [ ] Add correlation id generation for all credit-consuming endpoints (ReplicateController.GenerateImages, ReplicateController.GenerateBatchImages, ReplicateController.EnhancePhoto, EnhancementController.EnhancePhoto; keep existing training and pending generation correlation ids).
- [ ] Update refund creation to carry original charge action and correlationId (TrainingPollingService refunds use action "model_training"; batch partial refunds use action "styled_generation" with creditConsumed.CorrelationId).
- [ ] Add guarded refund logic in BasicTierService.RefundCreditsAsync:
  - If correlationId present, verify a matching charge UsageLog (Action = charge action, CreditsCost > 0, Details contains correlationId).
  - If no matching charge, log warning and return success without refund.
  - If matching refund log already exists for the same correlationId, skip refund.
- [ ] Add tests in BasicTierServiceTests for guarded refunds (missing charge log, successful refund with charge log, duplicate refund no-op).
- [ ] Update any controller/service test mocks if new correlation ids are required.

### Acceptance Criteria
- [ ] If a generation or enhancement request fails before credits are charged, credits do not increase and a warning is logged.
- [ ] If credits were charged and the request fails, the correct amount is refunded and UsageLog records a *_refund entry with the same correlationId.
- [ ] Duplicate refund attempts with the same correlationId do not change credit balances.
- [ ] Styled batch partial refunds only return credits for failed styles and are guarded by the original charge.
- [ ] Unit tests cover guarded refunds and idempotency.
## Additional Context

### Dependencies
- No external payment providers; this is internal credit accounting only.

### Testing Strategy
- Run `dotnet test AI.ProfilePhotoMaker.API.Tests`.
- Add or extend BasicTierServiceTests to validate guard behavior.
- Spot-check styled generation and enhancement endpoints in dev using simulated failures.

### Notes
- Refund logging is already duplicated in EnhancementController (openai_enhancement_refund plus BasicTierService *_refund). The guard should rely on the BasicTierService action for charge and refund pairing.
- UsageLog writes are best-effort; if logging fails, the guard may skip refunds. Log a warning so operators can investigate.
