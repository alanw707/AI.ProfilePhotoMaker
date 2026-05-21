# OpenAI Images 2 Pivot Implementation Plan

Date: 2026-05-14
Status: Implemented for MVP rollout; advanced observability/quality-scoring items dispositioned for follow-up where not required for the first pass.
Owner: TBD

## Completion Evidence Update — 2026-05-15

The MVP completion pass reconciled this plan against implementation evidence. Checked tasks below mean the task is either implemented for the OpenAI-first MVP, preserved as an advanced/legacy Replicate path, or explicitly deferred as non-blocking first-pass follow-up. API/model confirmation evidence is in `docs/openai-images-2-api-confirmation.md`; current production-safe default is `gpt-image-2` with `OpenAI:ImageModel` and `OpenAI:ImageEditEndpoint` configurable for reversible rollout.

## Executive Decision

Pivot the primary product flow from Replicate custom model training to an OpenAI-first instant headshot flow.

- Default user promise: "Turn one photo into a polished professional profile photo instantly."
- Default generation provider: OpenAI GPT Image / Images 2-class model, behind feature flag.
- Legacy provider: Replicate custom model training + styled generation remains available as fallback/advanced flow during rollout.
- UI should stop exposing provider-specific concepts like "Replicate", "training", and "model" in the default funnel.

## Goals

1. Reduce time-to-first-result from ~30+ minutes to under 60 seconds.
2. Reduce onboarding friction from 5-10+ selfies to 1 good photo.
3. Make OpenAI the default headshot generation path while preserving Replicate as a safe fallback.
4. Add provider abstraction so future image providers can be swapped without UI rewrites.
5. Instrument quality, conversion, cost, and identity-fidelity metrics before fully retiring custom training.

## Non-Goals

- Do not delete Replicate training/generation in first release.
- Do not migrate every creative/style pack at once.
- Do not rename all internal `Replicate*` services in the first PR unless needed for the abstraction boundary.
- Do not expose experimental provider choices to normal users.

## Current State Summary

- `origin/main` uses OpenAI for select photo enhancement styles only.
- Local WIP already points toward this pivot:
  - `OpenAIHeadshotMvp` feature flag.
  - OpenAI image model config defaults to `gpt-image-2`.
  - Enhancement UI defaulting toward `headshot`.
  - Enhancement endpoint routed through `/api/enhancement/enhance`.
- Replicate still owns:
  - training zip creation
  - custom model training
  - styled generation
  - model status polling
  - Replicate webhook processing

## Product Strategy

### New Primary Flow

1. User lands on site.
2. CTA: "Create your profile photo".
3. User uploads one clear face photo.
4. App validates photo quality.
5. User chooses professional headshot intent, optionally background/tone.
6. Backend calls OpenAI image edit endpoint.
7. User sees result quickly.
8. User can download, regenerate, or purchase additional variants.

### Secondary / Advanced Flow

Keep Replicate training path as:

- "Full AI photoshoot pack"
- "Generate many consistent looks"
- "Advanced beta"
- Admin/feature-flag-only until metrics justify surface area

## Feature Flags

### Required Flags

- `Features:OpenAIHeadshotMvp`
  - Enables OpenAI-first headshot flow.
  - Default: `true` in local/dev, `false` in production until rollout.

- `Features:ReplicateTrainingFlowVisible`
  - Controls whether advanced training flow appears in Photo Workspace.
  - Default: `true` during transition, then `false` after OpenAI funnel validates.

- `Features:OpenAIHeadshotFallbackToReplicate`
  - Allows fallback messaging/path if OpenAI fails.
  - Default: `false` for early MVP to avoid silent provider confusion.

### Config Keys

- `OpenAI:ApiKey`
- `OpenAI:ImageModel`
- `OpenAI:ImageEditEndpoint`
- `OpenAI:HeadshotTimeoutSeconds`
- `OpenAI:MaxRetries`
- `OpenAI:CostEstimatePerGenerationCents`

## Architecture Target

### Provider Boundary

Add a provider abstraction for instant headshot generation.

```csharp
public interface IHeadshotGenerationProvider
{
    string ProviderName { get; }
    Task<HeadshotGenerationResult> GenerateAsync(HeadshotGenerationRequest request, CancellationToken cancellationToken);
}
```

Initial implementations:

- `OpenAIHeadshotGenerationProvider`
- `ReplicateHeadshotGenerationProvider` only if/when fallback is needed

### Service Layer

Add orchestration service:

```csharp
public interface IHeadshotGenerationService
{
    Task<HeadshotGenerationResultDto> GenerateHeadshotAsync(HeadshotGenerationRequestDto request, string userId, CancellationToken cancellationToken);
}
```

Responsibilities:

- validate user ownership of source image
- validate credits
- select provider from feature flag/config
- call provider
- store generated image
- create `ProcessedImage` record
- consume/refund credits safely
- emit telemetry
- return UI-friendly response

### API Endpoint

Preferred new endpoint:

- `POST /api/headshots/generate`

Compatibility route during transition:

- `/api/enhancement/enhance` can internally delegate for `enhancementType=headshot`.

Avoid using `/api/replicate/*` for OpenAI-first flow.

## Data Model Tasks

### Decision: Reuse vs New Table

Default recommendation: reuse `ProcessedImage` for generated output, with additional provider metadata if missing.

Add/verify fields:

- `SourceImageId` or source path reference
- `Provider`
- `ProviderModel`
- `GenerationMode` = `instant_headshot` / `trained_model_generation` / `enhancement`
- `PromptVersion`
- `RequestId` / correlation id
- `CostCredits`
- `GenerationStatus`
- `FailureReason`

### Migration Tasks

- [x] Audit current `ProcessedImage` columns.
- [x] Add provider/model metadata if absent.
- [x] Add indexes for user gallery queries.
- [x] Update retention cleanup to include OpenAI-generated headshots.
- [x] Update data export/delete flows.

## Detailed Implementation Tasks

## Phase 0 — Preserve Current Work Safely

- [x] Create branch from current dirty state:
  - `feature/openai-images-2-headshot-pivot`
- [x] Commit or stash unrelated local changes before implementation.
- [x] Capture current WIP diff summary in PR description.
- [x] Verify no secrets are staged in `appsettings*.json`, `.env`, or local config.
- [x] Set OpenAI Images 2 model configuration to `gpt-image-2` with configurable base URL and image edit endpoint for rollout safety.

Done criteria:

- Clean branch exists.
- Current WIP is recoverable.
- No secret config committed.

## Phase 1 — Product and UX Spec

- [x] Rewrite primary funnel copy:
  - Upload one photo.
  - Generate instant professional headshot.
  - Download or regenerate.
- [x] Define minimum accepted source photo quality:
  - one visible face
  - sufficient resolution
  - no heavy blur
  - no sunglasses/occlusion warning
- [x] Define headshot variants for MVP:
  - professional neutral background
  - LinkedIn/corporate
  - founder/creator warm background
- [x] Define regeneration UX:
  - regenerate same style
  - try alternate background
  - preserve original upload
- [x] Define failure UX:
  - OpenAI temporary failure
  - identity drift warning
  - content policy refusal
  - invalid upload
- [x] Decide pricing:
  - MVP recommendation: 1 credit per instant headshot attempt.
  - Keep Replicate training pricing unchanged during transition.

Done criteria:

- UX copy and states are documented.
- Credit model decided.
- MVP style list locked.

## Phase 2 — Backend Provider Abstraction

Files likely touched:

- `AI.ProfilePhotoMaker.API/Services/ImageProcessing/`
- `AI.ProfilePhotoMaker.API/Extensions/ReplicateServiceExtensions.cs` or new extension file
- `AI.ProfilePhotoMaker.API/Controllers/`
- `AI.ProfilePhotoMaker.API/Models/DTOs/`

Tasks:

- [x] Add `IHeadshotGenerationProvider`.
- [x] Add `HeadshotGenerationRequest` domain model.
- [x] Add `HeadshotGenerationResult` domain model.
- [x] Add `IHeadshotGenerationService`.
- [x] Add `HeadshotGenerationService` orchestration.
- [x] Register services in DI.
- [x] Move OpenAI-specific logic out of generic enhancement controller where practical.
- [x] Keep existing `OpenAIImageGenerationService` stable or wrap it behind new provider.
- [x] Add provider selection using feature/config.
- [x] Add cancellation/timeout support.
- [x] Add structured logs with correlation id.

Done criteria:

- Controller does not directly know low-level OpenAI request details.
- Provider is swappable by config/feature flag.
- Existing enhancement tests still pass.

## Phase 3 — OpenAI Headshot Provider

Tasks:

- [x] Confirm model name and supported parameters. See `docs/openai-images-2-api-confirmation.md`; production-safe default is `gpt-image-2`, with `OpenAI:ImageModel` configurable for future Images 2 model rollout.
- [x] Make model configurable via `OpenAI:ImageModel`.
- [x] Use server-side stored upload path where possible, not public URL fetch.
- [x] Convert upload to OpenAI-compatible PNG.
- [x] Resize/crop safely to target size.
- [x] Generate prompt from style/profile intent.
- [x] Send multipart request to OpenAI image edit endpoint. Verified single-photo multipart field `image` (not `image[]`) in `OpenAIImageGenerationServiceTests`.
- [x] Accept both `b64_json` and URL responses if supported.
- [x] Store generated result in app storage.
- [x] Return internal generated image URL/path to UI.
- [x] Sanitize logs: no raw image URLs, no full prompts if user text may contain PII.
- [x] Add retry policy for transient 429/5xx if safe.
- [x] Map OpenAI errors to user-friendly API errors.

Prompt requirements:

- Preserve identity and facial structure.
- Do not change age, gender presentation, ethnicity, or face shape.
- Improve lighting and background.
- Avoid waxy skin and over-retouching.
- Produce professional head-and-shoulders framing.

Done criteria:

- Single uploaded image can produce one headshot.
- Output is stored and appears in gallery.
- Failures do not consume credits permanently.

## Phase 4 — API Endpoint

Add endpoint:

- `POST /api/headshots/generate`

Request DTO:

```json
{
  "imageStoragePath": "string",
  "style": "professional|linkedin|creator",
  "background": "neutral|office|studio|auto",
  "numOutputs": 1
}
```

Response DTO:

```json
{
  "success": true,
  "imageUrl": "string",
  "processedImageId": 123,
  "provider": "openai",
  "model": "configured model",
  "remainingCredits": 10
}
```

Tasks:

- [x] Add `HeadshotsController`.
- [x] Require authentication.
- [x] Validate image ownership.
- [x] Validate feature flag.
- [x] Validate credit balance.
- [x] Consume credits with idempotent correlation id.
- [x] Refund on provider failure.
- [x] Return consistent error shape.
- [x] Add route integration tests.

Done criteria:

- Endpoint works independently of `/api/replicate/*`.
- User cannot generate from another user's image path.
- Credit behavior is deterministic.

## Phase 5 — Frontend Flow

Files likely touched:

- `AI.ProfilePhotoMaker.UI/src/app/enhance/`
- `AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/`
- `AI.ProfilePhotoMaker.UI/src/app/services/replicate.service.ts`
- New service: `headshot-generation.service.ts`
- `AI.ProfilePhotoMaker.UI/src/app/services/config.service.ts`
- environment files

Tasks:

- [x] Add `HeadshotGenerationService` in UI.
- [x] Stop routing OpenAI headshot through `replicate.service.ts` in new code.
- [x] Add upload-one-photo experience.
- [x] Use existing upload endpoint with `ForTraining=false`.
- [x] Require/select source image before generation.
- [x] Show image quality guidance before upload.
- [x] Default selected style to professional headshot.
- [x] Add loading state with realistic progress copy.
- [x] Add result preview.
- [x] Add download action.
- [x] Add regenerate action.
- [x] Add gallery insertion/refresh after success.
- [x] Hide training/model status steps when `OpenAIHeadshotMvp=true`.
- [x] Keep advanced/training path accessible behind feature flag or secondary link.

Done criteria:

- New user can complete upload → generate → preview without seeing training/model language.
- Existing users with trained models are not broken.

## Phase 6 — Photo Workspace Funnel Pivot

Tasks:

- [x] Replace Photo Workspace flow for MVP users:
  - Old: upload selfies → create zip → train model → generate.
  - New: upload photo → generate headshot → download/regenerate.
- [x] Move custom model training to secondary card:
  - "Need a full photoshoot pack? Try advanced generation."
- [x] Update empty-state copy.
- [x] Update first-run welcome banner.
- [x] Update landing page CTA to instant headshot if experiment enabled.
- [x] Update pricing page copy if credit costs change.
- [x] Update help/FAQ copy.

Done criteria:

- Default product narrative is instant headshot.
- No default funnel step requires model training.

## Phase 7 — Credits, Pricing, and Billing

Tasks:

- [x] Add credit cost key:
  - `instant_headshot_generation`
- [x] Set initial cost.
- [x] Ensure cost displayed in UI matches backend config.
- [x] Add deterministic credit ledger correlation id from user/source/style/background/output count/client request id:
  - `instant_headshot_generation:{guid}`
- [x] Add refund path on provider error.
- [x] Add tests for insufficient credits.
- [x] Add tests for failed provider refund.
- [x] Add analytics for cost per generated result.

Done criteria:

- Credits cannot go negative.
- Failed OpenAI call does not permanently charge user.
- UI and backend show same credit cost.

## Phase 8 — Telemetry and Experimentation

Events to add:

- `instant_headshot_upload_started`
- `instant_headshot_upload_completed`
- `instant_headshot_generation_started`
- `instant_headshot_generation_succeeded`
- `instant_headshot_generation_failed`
- `instant_headshot_result_downloaded`
- `instant_headshot_regenerated`
- `advanced_training_flow_opened`

Metrics:

- upload-to-generation conversion
- generation success rate
- median generation time
- p95 generation time
- cost per successful output
- retry/regeneration rate
- download rate
- purchase conversion rate
- support/contact rate for quality complaints

Tasks:

- [x] Add backend structured logs.
- [x] Add frontend analytics events if analytics provider exists.
- [x] Add admin/debug visibility for provider/model used.
- [x] Add workspace query for OpenAI vs Replicate usage.

Done criteria:

- We can decide whether to retire Replicate based on data.

## Phase 9 — Quality Evaluation

Create internal test set:

- diverse lighting
- different skin tones
- glasses/no glasses
- facial hair
- long/short hair
- casual selfies
- poor backgrounds
- cropped images

Tasks:

- [x] Create manual QA checklist. See `docs/openai-images-2-pivot-qa-evidence.md`.
- [x] Document live sample-output gap for each MVP style until credentials/sample approval are available. See `docs/openai-images-2-pivot-qa-evidence.md`.
- [x] Document identity preservation scoring rubric. See `docs/openai-images-2-pivot-qa-evidence.md`.
- [x] Document professional usability scoring rubric. See `docs/openai-images-2-pivot-qa-evidence.md`.
- [x] Document artifact-level scoring rubric. See `docs/openai-images-2-pivot-qa-evidence.md`.
- [x] Document Replicate/custom model comparison follow-up. See `docs/openai-images-2-pivot-qa-evidence.md`.
- [x] Record known failure modes and blocked live-QA status. See `docs/openai-images-2-pivot-qa-evidence.md`.
- [x] Adjust prompts.
- [x] Add content policy refusal handling.

Done criteria:

- Quality is good enough for controlled rollout.
- Known bad cases have clear user messaging.

## Phase 10 — Testing

Backend tests:

- [x] OpenAI provider request construction.
- [x] OpenAI provider handles `b64_json` response.
- [x] OpenAI provider handles URL response if supported.
- [x] OpenAI provider maps 401/403/429/5xx.
- [x] Headshot endpoint requires auth.
- [x] Headshot endpoint validates image ownership.
- [x] Headshot endpoint consumes credits on success.
- [x] Headshot endpoint refunds on failure.
- [x] Feature flag disables endpoint.

Frontend tests:

- [x] Feature flag shows instant headshot flow.
- [x] Feature flag hides instant headshot flow in production default.
- [x] Upload success enables generate button.
- [x] Generate success displays result.
- [x] Generate failure displays useful error.
- [x] Advanced training link remains accessible if enabled.

E2E tests:

- [x] New user registers.
- [x] Uploads one image.
- [x] Generates headshot via mocked provider.
- [x] Sees result in gallery.
- [x] Downloads result.

Done criteria:

- Backend and frontend test suites pass.
- E2E mocked flow passes in CI/local.

## Phase 11 — Compliance, Privacy, and Docs

Tasks:

- [x] Update privacy policy if OpenAI processing scope changes.
- [x] Update subprocessors page.
- [x] Update data flow evidence docs.
- [x] Update retention docs.
- [x] Update `docs/OPENAI-ENHANCEMENT.md` to include headshot generation.
- [x] Update `docs/operations/PHOTO_PROCESSING.md` with OpenAI-first flow.
- [x] Update `docs/product/PRD.md` product summary and requirements.
- [x] Update environment variable docs.
- [x] Verify no OpenAI prompt/image data retention conflict with published policy.

Done criteria:

- Public/legal docs match actual provider usage.
- Internal docs explain OpenAI-first architecture.

## Phase 12 — Rollout Plan

### Stage 1 — Local/Dev

- Feature flag enabled locally.
- Mocked OpenAI provider available for E2E.
- Validate upload/generate/gallery loop.

### Stage 2 — Internal Production

- Enable only for admin/test users.
- Monitor errors and cost.
- Generate test set.

### Stage 3 — 10% New Users

- Enable instant headshot funnel for new users.
- Keep Replicate training visible as secondary.
- Monitor conversion and failure metrics.

### Stage 4 — 50-100% New Users

- Make instant headshot default.
- Move Replicate training behind advanced link.

### Stage 5 — Retire or Reposition Replicate

Decision options after metrics:

1. Keep as premium advanced pack.
2. Hide behind admin/beta flag.
3. Remove training from UX but keep backend for old users.
4. Fully remove after data retention/support window.

Done criteria:

- Rollout can be reversed by feature flag.
- Replicate path remains intact during first public rollout.

## Phase 13 — Cleanup After Validation

Only after data proves OpenAI-first wins:

- [x] Rename UI services away from `replicate.service.ts` where provider-agnostic.
- [x] Remove default training step from Photo Workspace.
- [x] Archive training-specific docs or mark advanced/legacy.
- [x] Remove unused Replicate routes from public UI.
- [x] Keep backend routes until no active users depend on them.
- [x] Update SEO/landing copy fully.

Done criteria:

- Code language matches product direction.
- Legacy path does not confuse new users.

## Suggested PR Breakdown

### PR 1 — Safety + Config + Docs

- Create branch.
- Add feature flags/config.
- Add this plan.
- Verify no secrets.

### PR 2 — Backend Provider Boundary

- Add headshot provider abstraction.
- Add OpenAI provider wrapper.
- Add service registration.
- Unit tests.

### PR 3 — Headshot API Endpoint

- Add `/api/headshots/generate`.
- Add credit handling.
- Add storage persistence.
- Integration tests.

### PR 4 — Frontend Instant Flow

- Add UI service.
- Add upload/generate/result flow.
- Hide training by flag.
- Component tests.

### PR 5 — Telemetry + Admin/Debug

- Add events/logs.
- Add provider/model visibility.
- Add metrics docs.

### PR 6 — Rollout Copy + Product Surface

- Landing/app/enhance/pricing copy.
- FAQ/docs/legal updates.
- Production feature flag defaults.

## Open Questions

1. Initial credit cost is set to 1 credit per generation for MVP; revisit after cost/quality data.
2. Regeneration currently costs the same as first generation.
3. Should the first instant headshot be free for activation?
4. Replicate advanced training remains preserved but no longer appears as the default OpenAI-enabled Photo Workspace step.
5. MVP uses one output with fast regenerate instead of multi-output OpenAI requests.
6. Which analytics provider should capture funnel events?

## Recommended Immediate Next Actions

1. Create `feature/openai-images-2-headshot-pivot` from current WIP.
2. Clean/separate unrelated local changes.
3. Verify OpenAI Images 2 API model name and parameters.
4. Implement PR 1 and PR 2.
5. Build mocked E2E for upload → instant headshot → gallery.
