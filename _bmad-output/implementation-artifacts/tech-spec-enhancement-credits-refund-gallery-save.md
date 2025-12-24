# Tech-Spec: Enhancement Credits Persist After Gallery Save

**Created:** 2025-12-22
**Status:** Completed

## Overview

### Problem Statement
Credits are deducted when a user runs photo enhancement (OpenAI or Replicate), but credits are refunded when the user saves the generated image to the gallery. This makes successful enhancements effectively free and breaks credit accounting.

### Solution
Ensure the gallery save endpoint for enhanced images does not overwrite credit fields with stale user profile data. Save enhanced images without updating cached `UserProfile` credit fields, and avoid persisting stale credits back to the database.

### Scope (In/Out)

In:
- Enhancement flows (OpenAI + Replicate) through the gallery save step.
- Credit integrity across enhancement + save workflow.

Out:
- Other credit flows (training, styled generation, purchases).
- Non-enhancement gallery saves or uploads.

## Context for Development

### Codebase Patterns
- Enhancement endpoints consume credits before calling providers:
  - OpenAI: `EnhancementController.EnhancePhoto` uses `IBasicTierService.ConsumeCreditsAsync(..., "photo_enhancement")`.
  - Replicate: `ReplicateController.EnhancePhoto` uses `IBasicTierService.ConsumeCreditsAsync(..., "photo_enhancement")`.
- Gallery save for enhanced images is handled by `ImageController.SaveEnhancedImage`.
- `UserContextService.GetUserProfileAsync` caches the `UserProfile` for 5 minutes.
- `UserProfileRepository.UpdateAsync` updates the entire entity, including `Credits` and `PurchasedCredits`.

### Files to Reference
- `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`
- `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`
- `AI.ProfilePhotoMaker.API/Controllers/ImageController.cs` (SaveEnhancedImage)
- `AI.ProfilePhotoMaker.API/Services/UserContextService.cs` (cached profile)
- `AI.ProfilePhotoMaker.API/Data/UserProfileRepository.cs` (UpdateAsync)
- `AI.ProfilePhotoMaker.API/Services/BasicTierService.cs` (credit consumption)

### Technical Decisions
- Avoid using cached `UserProfile` when persisting enhanced gallery saves.
- Save the enhanced `ProcessedImage` directly without modifying `UserProfile` credit fields.
- If a profile lookup is required, use a light, fresh query (no cache) for the profile ID only.

## Implementation Plan

### Tasks
- [x] Update `ImageController.SaveEnhancedImage` to avoid calling `_userProfileRepository.UpdateAsync(profile)` with cached data.
- [x] Persist enhanced `ProcessedImage` directly via `_dbContext.ProcessedImages.Add(...)` or a new repository helper that does not touch profile credits.
- [x] Fetch profile ID from a non-cached source (e.g., `IUserProfileRepository.GetByUserIdLightAsync`) and use it to populate `UserProfileId` on `ProcessedImage`.
- [x] Add/adjust tests to ensure credits are not modified during `SaveEnhancedImage` (unit test or integration test with in-memory DB).

### Acceptance Criteria
- [x] When an enhancement completes successfully and the user saves to gallery, credits remain deducted (no refund).
- [x] Behavior is consistent for both OpenAI and Replicate enhancement providers.
- [x] Saving an enhanced image does not modify `Credits` or `PurchasedCredits` fields.

## Additional Context

### Dependencies
- None.

### Testing Strategy
- Add a unit or integration test for `ImageController.SaveEnhancedImage` that:
  - Seeds a user profile with known credit values.
  - Executes save-enhanced flow.
  - Verifies credit values remain unchanged in the database.
- Manual verification: run enhancement + save in dev and confirm credits remain deducted.

### Notes
- Likely root cause: cached `UserProfile` overwriting credit fields when saved during gallery save.
