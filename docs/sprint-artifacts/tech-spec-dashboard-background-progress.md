# Tech-Spec: Persistent Dashboard Progress for Background Training & Generation

**Created:** 2025-12-13
**Status:** Completed

## Overview

### Problem Statement

Users can start model training and/or photo generation from `/app/dashboard`. If they click **Continue in Background**, navigate away, and later return to `/app/dashboard`, they currently lose visible progress/status. This creates uncertainty (“is anything happening?”) and a poor Headshot Studio workflow experience.

There is an existing progress UI while the user stays on the dashboard, but it is driven by in-memory client state. On navigation away, that state is disposed/reset, so returning users see no progress even when work is still running server-side.

### Solution

Make `/app/dashboard` always show the **latest active job** (training or generation) with a prominent status/progress UI that **automatically resumes** after navigation away and back. If the job fails, show an error state with details plus **credits charged/refunded** and a **purchase credits** CTA.

The source of truth for “what is running” should be server-side state (`/api/model-status` + persisted DB records). Client-side progress can still enhance UX while staying on the page, but it must not be lost on navigation.

### Scope (In/Out)

**In scope**
- Detect active training or generation when loading `/app/dashboard`.
- Show a prominent, persistent in-progress indicator (training/generating) and progress estimate.
- Resume status display automatically after navigating away and returning.
- Display error details for failures, including credits charged/refunded and a “Purchase Credits” button.
- Latest job only (no job history list).

**Out of scope**
- Multi-job history UI or managing multiple concurrent jobs.
- Overhauling Replicate workflows or changing the core training/generation algorithms.
- Adding a new global notifications system (reuse existing NotificationService + dashboard UI).

## Context for Development

### Codebase Patterns

- The dashboard premium workflow is driven by:
  - A coordinator state service (`DashboardCoordinatorService`) for profile/images/credits/model status.
  - A workflow service (`WorkflowOrchestrationService`) that manages training/generation progress with polling timers and a `BehaviorSubject`.
- Today, the “progress card” on the dashboard is bound to `workflowProgress$` (in-memory). The workflow service is disposed when the dashboard component is destroyed, which clears timers and resets progress.
- The API already exposes a unified “model status” endpoint and has persisted records for training and background generation.

### Files to Reference

**UI**
- `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.ts` (pauses workflow timers; `continueInBackground()` keeps user on dashboard and scrolls to active job banner)
- `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.html` (binds progress UI to `workflowProgress$`)
- `AI.ProfilePhotoMaker.UI/src/app/services/workflow-orchestration.service.ts` (training/generation progress logic, polling, background queue)
- `AI.ProfilePhotoMaker.UI/src/app/services/dashboard-coordinator.service.ts` (loads `getUnifiedModelStatus()`; current dashboard state source)
- `AI.ProfilePhotoMaker.UI/src/app/services/file-upload.service.ts` (`getUnifiedModelStatus()` client API)
- `AI.ProfilePhotoMaker.UI/src/app/services/model-status-mapper.service.ts` (fallback progress estimation for training)
- `AI.ProfilePhotoMaker.UI/src/app/components/dashboard/style-selector/style-selector.component.html` (progress UI + “Continue in Background” button)

**API**
- `AI.ProfilePhotoMaker.API/Controllers/ModelStatusController.cs` (`GET /api/model-status`, includes `CurrentRequest` + `GenerationStatus`)
- `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs` (training, queue generation, generation; credit consumption/refunds in some paths)
- `AI.ProfilePhotoMaker.API/Services/TrainingPollingService.cs` (finalizes training; refunds training credits on failure)
- `AI.ProfilePhotoMaker.API/Services/PendingGenerationService.cs` + `AI.ProfilePhotoMaker.API/Models/PendingGenerationRequest.cs` (queued background generation + refund on failure per style)
- `AI.ProfilePhotoMaker.API/Models/UsageLog.cs` + `AI.ProfilePhotoMaker.API/Services/BasicTierService.cs` (credit consumption + refund logging via `*_refund` actions)

### Technical Decisions

1. **Dashboard resume is server-driven**
   - Use `GET /api/model-status` as the primary “what’s running” signal on dashboard load/refresh.
   - Enhance the response to include resume-friendly IDs and credit impact fields required for the UI.

2. **Do not lose client progress on navigation**
   - Do not hard-reset workflow progress state when navigating away from the dashboard.
   - Prefer a “pause” behavior (stop timers) over “dispose/reset” (clears progress), then “resume” on return.

3. **Progress is an estimate, but must be stable**
   - Training: provide an estimated percent + ETA based on server state (and optionally Replicate training status if ID available).
   - Generation: provide an estimated percent + status based on the latest prediction status and timestamps.

4. **Credits on failure must be explicit**
   - Server must return enough data to show “charged / refunded / net” for the latest job.
   - If a job fails with no deliverables, credits should be fully refunded.
   - If a job has partial deliverables (some styles succeeded), refund only the failed portion; show the breakdown (or clearly state partial refund).

## Implementation Plan

- [x] Task 1: Expose resume + credit fields in `GET /api/model-status`
  - Added `currentRequest.trainingRequestId` for training polling/resume.
  - Added `creditImpact` for failure states (charged/refunded/net).
  - Adjusted generation status selection to prefer newer queued background generation.

- [x] Task 2: Provide credit impact for latest job (API)
  - Implemented refund logging in `UsageLog` and tagged credit logs with `correlationId` in `UsageLog.Details` for accurate job attribution.
  - `GET /api/model-status` uses correlationId first, then falls back to time-window heuristics for legacy rows.

- [x] Task 3: Add a dashboard-level “Active Job” UI block (UI)
  - Add a prominent banner/card near the top of `/app/dashboard` that shows:
    - Training vs generation label
    - Progress bar + percent
    - “Running in background” copy
    - ETA (if available)
    - A link/button to relevant screen (dashboard, gallery) as appropriate
  - When failed:
    - Show error message + details (safe, user-facing)
    - Show credits charged/refunded/net
    - Show “Purchase Credits” button (reuse existing navigation to pricing)

- [x] Task 4: Resume progress automatically on return (UI)
  - On dashboard init:
    - Call `getUnifiedModelStatus()` immediately.
    - If `activeJob.type` is training/generation, render the active job UI block immediately.
  - Fix the local reset bug:
    - Replace dashboard `ngOnDestroy()` behavior so it does not call a destructive reset of workflow progress for in-flight jobs.
    - Add an explicit `resume()` call on init that restarts any needed polling (or relies purely on `getUnifiedModelStatus()` polling).

- [x] Task 5: Add lightweight polling while dashboard is visible (UI)
  - While `activeJob.type !== none`, poll `getUnifiedModelStatus()` every 10–15 seconds to refresh:
    - Progress/ETA estimate
    - Failure/completion state
    - Credit impact fields (especially on failure)
  - Stop polling when job completes or fails and the user dismisses the message (optional).

- [x] Task 6: Validation and tests
  - Add/extend a repo Playwright test to cover:
    - Start training, click “Continue in Background”, navigate away, return to `/app/dashboard`, assert active job UI visible.
    - If feasible: simulate a failure state and assert error UI shows refund information.

### Acceptance Criteria

- [x] AC 1: Given a user has an in-progress training job, when they leave `/app/dashboard` and later return, then the dashboard immediately shows “Training in progress” with a progress bar/status without requiring clicks.
- [x] AC 2: Given a user has an in-progress generation job, when they leave `/app/dashboard` and later return, then the dashboard immediately shows “Generating in progress” with a progress bar/status without requiring clicks.
- [x] AC 3: Given the latest job fails, when the user returns to `/app/dashboard`, then the dashboard shows the failure message plus credits charged/refunded/net and a “Purchase Credits” CTA.
- [x] AC 4: Given the latest job completes, when the user returns to `/app/dashboard`, then the dashboard no longer shows an in-progress card and the “Photos Generated” count refreshes normally.
- [x] AC 5: Latest job only: if multiple jobs exist historically, dashboard surfaces only the latest active/failed/completed job relevant to the current moment.

## Additional Context

### Dependencies
- API: `GET /api/model-status` (and any additions to its response shape)
- DB models: `ModelCreationRequest`, `PendingGenerationRequest`, possibly `UsageLog`
- Existing endpoints used for deeper status:
  - `GET /api/replicate/train/status/{trainingId}` (ownership-checked)

### Testing Strategy
- UI unit tests (if present) for new status component mapping logic.
- Repo Playwright (`tests/e2e`) smoke test covering navigation away and back and verifying the active job UI is shown.

### Notes
- Current “training progress %” is partly time-based client logic; this spec intentionally treats progress as an estimate, but ensures visibility and continuity.
- Credit refund behavior must be consistent with UX copy:
  - If the system fully refunds on job failure, say so and show amounts.
  - If partial success is possible, the UI must not claim a full refund when images were delivered.

---

## Dev Agent Record

### File List
- `.bmad/bmm/workflows/4-implementation/code-review/checklist.md`
- `AI.ProfilePhotoMaker.API.Tests/Performance/UserProfileRepositoryPerformanceTests.cs`
- `AI.ProfilePhotoMaker.API.Tests/Unit/ModelStatusCreditImpactTests.cs`
- `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientUrlNormalizationTests.cs`
- `AI.ProfilePhotoMaker.API.Tests/Unit/StripeClientFactoryTests.cs`
- `AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs`
- `AI.ProfilePhotoMaker.API/Controllers/ImageController.cs`
- `AI.ProfilePhotoMaker.API/Controllers/ModelStatusController.cs`
- `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`
- `AI.ProfilePhotoMaker.API/Middleware/StorageProxyMiddleware.cs`
- `AI.ProfilePhotoMaker.API/Models/DTOs/ModelStatusDto.cs`
- `AI.ProfilePhotoMaker.API/Program.cs`
- `AI.ProfilePhotoMaker.API/Services/BasicTierService.cs`
- `AI.ProfilePhotoMaker.API/Services/CreditConsumptionResult.cs`
- `AI.ProfilePhotoMaker.API/Services/IBasicTierService.cs`
- `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs`
- `AI.ProfilePhotoMaker.API/Services/ImageProcessing/IReplicateApiClient.cs`
- `AI.ProfilePhotoMaker.API/Services/ImageProcessing/MockReplicateApiClient.cs`
- `AI.ProfilePhotoMaker.API/Services/PendingGenerationService.cs`
- `AI.ProfilePhotoMaker.API/Services/Payments/StripeClientFactory.cs`
- `AI.ProfilePhotoMaker.API/Services/Storage/AzureBlobStorageService.cs`
- `AI.ProfilePhotoMaker.API/Services/TrainingPollingService.cs`
- `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/services/image-url.service.ts`
- `AI.ProfilePhotoMaker.UI/src/app/services/workflow-orchestration.service.ts`
- `dev-start.sh`
- `docker-compose.yml`
- `docs/sprint-artifacts/tech-spec-dashboard-background-progress.md`
- `tests/e2e/dashboard-background-status.spec.js`

### Change Log
- Dashboard shows an “Active Job” banner from server-driven status and stops losing state on navigation.
- Training/generation status UI no longer lingers on a stale “Training failed” when a new job begins.
- Replicate ZIP URLs are normalized to the reserved ngrok domain and preflight-checked before training.
- Local Azurite ZIP creation avoids transient 404 windows (overwrite upload behavior).
- Storage proxy streams blob content (avoids buffering large ZIPs into memory).
- Replicate training request records no longer get stuck in `Pending` when Replicate fails early.
- Credit impact is correlated per job via `correlationId`, with legacy time-window fallback.
- Production safety hardening: restrict forwarded host handling, re-enable startup environment validation for non-development, and lock down debug model-status endpoint.
