---
title: 'Fix Double Image Generation on Continue-in-Background'
slug: 'fix-double-generation-continue-background'
created: '2026-02-18'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Angular 19', 'TypeScript', 'ASP.NET Core', 'C#', 'Entity Framework Core (InMemory + SQL)', 'xUnit + Moq + FluentAssertions', 'Jasmine + Karma']
files_to_modify: ['AI.ProfilePhotoMaker.UI/src/app/services/removed legacy workflow orchestration service', 'AI.ProfilePhotoMaker.UI/src/app/enhance/app/photo-enhancement.component.ts']
code_patterns: ['Angular services with eagerly-injected _deps struct + lazy-loaded individual services (_loadReplicateService)', 'setInterval-based polling with clearInterval cleanup', 'Fire-and-forget async calls (queueBackgroundGeneration)', 'Backend uses scoped DI services resolved on-demand in controller actions', 'PendingGenerationRequest status machine: Pending → Started → Succeeded/Failed']
test_patterns: ['xUnit + Moq + InMemoryDatabase for backend unit tests', 'Jasmine + Karma + TestBed for Angular specs', 'No existing tests for RemovedLegacyWorkflowOrchestration', 'PendingGenerationServiceTests.cs exists with good coverage patterns']
---

# Tech-Spec: Fix Double Image Generation on Continue-in-Background

**Created:** 2026-02-18

## Overview

### Problem Statement

When a user starts a combined training+generation session and clicks "Continue in Background", images are generated twice per style. The backend `PendingGenerationService.ProcessAsync` processes the queued `PendingGenerationRequest` (created when "Continue in Background" is clicked), AND the frontend `_generateImagesWithStyles` fires a direct `POST /replicate/generate/batch` call after `FinalizeTraining` returns a model version. Both paths execute nearly simultaneously, resulting in 2x the expected images per style (e.g., 4 instead of 2). Credits are deducted correctly — only the generation execution is doubled.

### Solution

Add a boolean guard flag (`_backgroundGenerationQueued`) to `RemovedLegacyWorkflowOrchestration`. The flag must be set **synchronously** as the very first operation in `queueBackgroundGeneration()` — before any `await` — to eliminate the race window between the fire-and-forget call from the Photo Workspace and the polling interval. In `_startTrainingStatusPolling`, check this flag before calling `_generateImagesWithStyles` in **both** the primary path and the 15-second retry/fallback path — if `true`, skip the direct frontend generation since the backend will handle it via `ProcessTrainingCompletion` → `ProcessAsync`. The flag is **not consumed** (not reset to `false`) at the check point; instead it is reset only at the start of a new training session (`_startModelTraining`) to ensure clean state. Additionally, guard the Photo Workspace's `continueInBackground()` to prevent multiple rapid clicks from queuing duplicate requests.

### Scope

**In Scope:**
- Fix double-generation when "Continue in Background" is used during a training+generation flow
- Guard against race conditions between fire-and-forget queue call and polling interval
- Guard against multiple rapid clicks on "Continue in Background"
- Ensure credits remain correctly deducted (already working — no changes needed)

**Out of Scope:**
- Refactoring the overall training/generation architecture
- Backend changes (backend path is correct)
- Credit calculation changes
- Server-authoritative `pendingGenerationProcessed` response field (tracked as future hardening — see Notes)

## Context for Development

### Codebase Patterns

- **`_deps` struct** is eagerly populated in the constructor (L227-233) with directly injected singletons. Separately, `_replicateService` and `_fileUploadService` are lazy-loaded via explicit `_loadReplicateService()` / `_loadFileUploadService()` methods.
- **Polling** uses `setInterval` with `clearInterval` cleanup, stored in `this._pollingInterval`
- **`queueBackgroundGeneration`** is `async` and called fire-and-forget (`queueWork()` without `await` in Photo Workspace L608). It awaits `_loadReplicateService()` internally, creating an async gap before the API call.
- **`PendingGenerationRequest`** follows a status machine: `Pending` → `Started` → `Succeeded`/`Failed`
- **`ProcessAsync`** filters by `Status == Pending` — provides idempotency for sequential calls but not concurrent ones
- **`ProcessTrainingCompletion`** guards with `CompletedAt.HasValue` — prevents re-finalization but has a narrow race window
- **Existing boolean flags** in the service follow the pattern of private class fields (e.g., `private _isResetting = false`)
- **`resetProgress()`** is only called from `dispose()` (L1783-1786). `dispose()` is never called from the Photo Workspace component. `ngOnDestroy` only calls `pause()` → `_clearAllIntervals()`.
- **`_clearAllIntervals()`** is called in multiple contexts: at the start of `_generateImagesWithStyles` (L1043), via `pause()` on `ngOnDestroy`, and elsewhere. It is NOT a safe place to reset generation-related state.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `AI.ProfilePhotoMaker.UI/src/app/services/removed legacy workflow orchestration service` | **Primary edit target** — contains `_startTrainingStatusPolling` (L911), `_generateImagesWithStyles` (L1033), `_attemptFinalizeTraining` (L1192), `queueBackgroundGeneration` (L1788), `_startModelTraining`, `resetProgress()` |
| `AI.ProfilePhotoMaker.UI/src/app/enhance/app/photo-enhancement.component.ts` | **Secondary edit target** — `continueInBackground()` (L589) needs a guard against multiple clicks |
| `AI.ProfilePhotoMaker.API/Services/TrainingPollingService.cs` | `ProcessTrainingCompletion` (L124) — calls `ProcessAsync` at L275. No changes needed. |
| `AI.ProfilePhotoMaker.API/Services/PendingGenerationService.cs` | `ProcessAsync` (L83) — processes queued generation. No changes needed. |

### Technical Decisions

- **Frontend-only fix**: The backend generation path via `ProcessTrainingCompletion` → `ProcessAsync` is correct. The bug is the frontend firing a redundant second generation. A boolean flag on the frontend service is the minimal, surgical fix.
- **Synchronous flag set (addresses F1)**: The flag MUST be set as the very first line of `queueBackgroundGeneration`, before any `await`, to close the race window between the fire-and-forget call and the 30-second polling interval.
- **Flag NOT consumed at check point (addresses F3)**: The flag stays `true` after being checked in the primary generation path. This ensures the 15-second retry/fallback path is also guarded. The flag is only reset at the start of a new training session.
- **Reset in `_startModelTraining`, NOT `resetProgress()` (addresses F2)**: Since `resetProgress()` is never called in normal operation (only via the never-called `dispose()`), the flag is reset at the start of `_startModelTraining` which is the actual entry point for every new training session. Also reset in `resetProgress()` as defense-in-depth.
- **Do NOT reset in `_clearAllIntervals()` (addresses F6)**: `_clearAllIntervals()` is called in too many contexts (including at the start of `_generateImagesWithStyles` itself). Resetting the flag there creates dangerous interactions with navigation timing.
- **No backend API changes**: `FinalizeTraining` response shape is unchanged.
- **Flag over polling stop**: We do NOT stop the polling when "Continue in Background" is clicked. The polling is still needed to detect training completion and call `_attemptFinalizeTraining` (which triggers `ProcessTrainingCompletion` on the backend). We only suppress the frontend's direct `_generateImagesWithStyles` call.

## Implementation Plan

### Tasks

- [x] **Task 1: Add `_backgroundGenerationQueued` flag to `RemovedLegacyWorkflowOrchestration`**
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/removed legacy workflow orchestration service`
  - Action: Add a private boolean field `private _backgroundGenerationQueued = false;` alongside the other private fields in the class
  - Notes: Follow existing naming convention for private fields (e.g., `_pollingInterval`, `_isResetting`)

- [x] **Task 2: Set the flag synchronously in `queueBackgroundGeneration`**
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/removed legacy workflow orchestration service`
  - Action: In `queueBackgroundGeneration` (~L1788), set `this._backgroundGenerationQueued = true;` as the **very first line** of the method body, before the `trainingId` read, before any `await`, before the `try` block. This closes the race window identified in F1 — since `continueInBackground()` calls this fire-and-forget, the flag must be set synchronously in the same microtask.
  - Error handling: In the `catch` block, reset `this._backgroundGenerationQueued = false;`. Also after the `firstValueFrom()` call, check if the response indicates failure (non-success status) and reset the flag if so. Example:
    ```typescript
    async queueBackgroundGeneration(selectedStyles: StyleOption[], imagesPerStyle: number): Promise<void> {
      this._backgroundGenerationQueued = true; // MUST be first — before any await
      const trainingId = this.getProgress().trainingId;
      if (!trainingId || selectedStyles.length === 0) {
        this._backgroundGenerationQueued = false;
        return;
      }
      try {
        const replicateService = await this._loadReplicateService();
        const result = await firstValueFrom(
          replicateService.queueGeneration(trainingId, selectedStyles.map(s => s.name), imagesPerStyle)
        );
        if (result && !result.success) {
          this._backgroundGenerationQueued = false;
        }
      } catch (error) {
        this._backgroundGenerationQueued = false;
        // existing error notification...
      }
    }
    ```

- [x] **Task 3: Guard `_generateImagesWithStyles` in BOTH generation paths**
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/removed legacy workflow orchestration service`
  - Action: In `_startTrainingStatusPolling`, guard `_generateImagesWithStyles` in **both** the primary path (~L985-986) AND the 15-second `setTimeout` retry/fallback path (~L1000-1005). Do NOT reset the flag at the check point — it must stay `true` to guard both paths:
    ```typescript
    // Primary path (~L985-986):
    if (!this._backgroundGenerationQueued) {
      await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, finalVersion);
    }

    // Retry/fallback path inside setTimeout (~L1000-1005):
    if (!this._backgroundGenerationQueued) {
      await this._generateImagesWithStyles(selectedStyles, imagesPerStyle, retryVersion);
    }
    ```
  - Notes: The flag is NOT consumed here. It stays `true` to guard both paths. Log when skipping for observability: `console.debug('[Workflow] Skipping frontend generation — background generation queued');`

- [x] **Task 4: Reset the flag at session start**
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/removed legacy workflow orchestration service`
  - Action: Add `this._backgroundGenerationQueued = false;` in TWO places:
    1. At the start of `_startModelTraining()` — this is the actual entry point for every new training session, guaranteeing clean state regardless of how the previous session ended.
    2. In `resetProgress()` — as defense-in-depth, even though this method is not called in normal operation.
  - Notes: Do NOT add to `_clearAllIntervals()` — that method is called in too many contexts and would create dangerous interactions (F6).

- [x] **Task 5: Guard against multiple rapid clicks on "Continue in Background"**
  - File: `AI.ProfilePhotoMaker.UI/src/app/enhance/app/photo-enhancement.component.ts`
  - Action: In `continueInBackground()` (~L589), add an early return if `_backgroundGenerationQueued` is already true. Since `_backgroundGenerationQueued` lives on the workflow service, check it via a public getter or check a local component flag:
    - **Option A (preferred)**: Add a component-level `_backgroundQueued = false` flag. Set to `true` at the start of `continueInBackground()`, check it as the first line.
    - **Option B**: Expose a public `get backgroundGenerationQueued()` getter on the workflow service and check it.
  - Notes: This prevents the multiple-click scenario (F4) where duplicate `PendingGenerationRequest` records could be created. The simplest approach is a component-level guard since `continueInBackground` is a component concern.

### Acceptance Criteria

- [ ] **AC 1**: Given a user starts training+generation with 2 styles and 2 images per style, when they click "Continue in Background" and training completes while they remain on the page, then exactly 2 images per style are generated (not 4).

- [ ] **AC 2**: Given a user starts training+generation with 2 styles and 2 images per style, when they do NOT click "Continue in Background" and training completes normally, then exactly 2 images per style are generated via the direct frontend path (existing behavior unchanged).

- [ ] **AC 3**: Given a user clicks "Continue in Background" but the queue API call fails (throws or returns non-success), when training completes, then the frontend falls back to direct generation via `_generateImagesWithStyles` (flag reset on error).

- [ ] **AC 4**: Given a user completes one training+generation session with "Continue in Background", when they start a NEW training+generation session, then the `_backgroundGenerationQueued` flag is `false` (reset at the start of `_startModelTraining`).

- [ ] **AC 5**: Given a user clicks "Continue in Background", when training completes, then `_attemptFinalizeTraining` is still called (ensuring the backend marks the model as Ready and processes the pending generation). Only the direct `_generateImagesWithStyles` call is suppressed.

- [ ] **AC 6**: Given training completes and `_attemptFinalizeTraining` returns `null` on first attempt, when the 15-second retry fires and succeeds, then `_generateImagesWithStyles` is still suppressed if "Continue in Background" was clicked (flag guards both paths).

- [ ] **AC 7**: Given the user rapidly clicks "Continue in Background" multiple times, then only one `PendingGenerationRequest` is created (guard prevents duplicate queue calls).

- [ ] **AC 8**: Given the user clicks "Continue in Background" and the flag is set synchronously, when the 30-second polling interval fires in the same event loop cycle, then the flag is already `true` (no race window).

## Additional Context

### Dependencies

- No new dependencies required
- No database migrations needed
- No API contract changes
- No backend code changes

### Testing Strategy

**Manual Testing (Primary):**
1. Start a training+generation session with 2 styles, 2 images per style
2. Click "Continue in Background" immediately
3. Wait for training to complete
4. Verify exactly 2 images per style appear in the gallery (not 4)
5. Repeat without clicking "Continue in Background" — verify 2 images per style (regression check)
6. Rapidly click "Continue in Background" multiple times — verify no duplicate generation
7. Start a second training+generation session after the first completes — verify normal behavior

**Unit Testing (Recommended):**
- No spec file exists for `RemovedLegacyWorkflowOrchestration` yet
- Create `removed legacy workflow orchestration service spec` with Jasmine/Karma or add focused tests
- Test that `queueBackgroundGeneration` sets the flag synchronously (before any async operation)
- Test that `queueBackgroundGeneration` resets the flag on API failure
- Test that `_startModelTraining` resets the flag
- Test that the polling success handler skips generation when flag is `true`
- Test that the retry/fallback handler also skips generation when flag is `true`

### Adversarial Review Findings Addressed

| Finding | Severity | Resolution |
|---------|----------|------------|
| F1: Race condition on flag set | Critical | Flag set synchronously as first line of `queueBackgroundGeneration`, before any `await` (Task 2) |
| F2: `resetProgress()` never called | Critical | Flag reset in `_startModelTraining` instead — the actual session entry point (Task 4) |
| F3: Fallback retry path unguarded | Critical | Both primary and retry paths explicitly guarded; flag NOT consumed on check (Task 3) |
| F4: Multiple rapid clicks | High | Component-level guard in `continueInBackground` (Task 5) |
| F5: Non-throwing API failures | High | Response success check added alongside catch block (Task 2) |
| F6: `_clearAllIntervals()` dangerous | High | Explicitly excluded as reset point (Task 4 notes) |
| F7: Wrong `_deps` description | Medium | Corrected in Codebase Patterns section |
| F8: Navigate-away-and-back | Medium | Addressed by resetting in `_startModelTraining` rather than lifecycle hooks |
| F9: Testing gaps | Medium | Unit testing upgraded from "Optional" to "Recommended" with specific test cases |
| F10: Premature flag reset | Medium | Flag is no longer consumed at check point — stays `true` until next session |

### Notes

- **Low-medium risk**: Single frontend change with a boolean guard, but requires careful placement to avoid race conditions. The adversarial review identified and resolved the key risks.
- **Root cause**: Two independent generation triggers — backend `PendingGenerationService.ProcessAsync` via `ProcessTrainingCompletion`, and frontend `_generateImagesWithStyles` via direct batch call after `_attemptFinalizeTraining` returns.
- **Credits are correct**: The frontend deducts credits once during `startTrainingWithStyles`. The doubled images are "free" from the credit perspective but wasteful of Replicate API usage and confusing to the user.
- **Future hardening (tech debt)**: A more robust long-term fix would have `FinalizeTraining` return a `pendingGenerationProcessed: true` field, allowing the frontend to make a server-authoritative decision instead of relying on client-side flag synchronization. This should be tracked as a follow-up item.

## Review Notes

- Adversarial code review completed (2 rounds: spec review + code review)
- Findings: 11 total, 7 fixed, 4 acknowledged (F3 edge-case, F8 unlikely re-entry, F9 pre-existing stale test, F11 pre-existing inconsistency)
- Resolution approach: auto-fix
- Fixed: F1 (component flag reset on new session), F2 (misleading comment), F4 (catch block reset), F5 (null check inversion), F6 (naming noted), F7 (console.debug → _logger), F10 (covered by _startModelTraining reset)
