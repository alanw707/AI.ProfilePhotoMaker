# Replicate Training/Generation Workflow – Implementation Plan

This plan addresses issues in the Replicate training/generation workflow and adds a safe, no‑cost mock path for local development and tests.

## Scope

- Fix model ID consistency, user ID trust, and credit handling in controllers.
- Add secure ownership checks for status endpoints.
- Introduce a full no-cost mock implementation of `IReplicateApiClient` (no real network calls).
- Add minimal persistence for prediction ownership.
- Provide testing and rollout guidance.

## Phase 1: Model ID Consistency

- Files: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs`, `AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs`.
- Steps:
  - Store full model ID (owner/model-name) in DB:
    - In `CreateModelTrainingAsync`, set `modelCreationRequest.ReplicateModelId = destination` (remove `.Split('/').Last()`).
  - Ensure webhook reconciliation uses the same format:
    - In `ReplicateWebhookController`, when parsing `payload.Version`, keep the base model string as `owner/model-name` (left of the colon). Maintain `ReplicateModelId` as full `owner/model-name`.
  - Rationale: Enables webhook match → DB row update, and makes `CheckModelAvailabilityAsync`/`GetModelVersionAsync` work as they expect `owner/model-name`.

## Phase 2: Trust Authenticated User

- Files: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`, `AI.ProfilePhotoMaker.API/Models/DTOs/*`.
- Steps:
  - Use claims user ID for server actions:
    - Replace all `dto.UserId` usage with `userId` from `ClaimTypes.NameIdentifier` when calling `IReplicateApiClient` (Train, Generate, Batch, Basic, Enhance).
  - Optional stricter mode: if a DTO contains `UserId` and it mismatches claims, return `400` (`InvalidUserContext`).
  - Optional DTO cleanup: remove `UserId` from `TrainModelRequestDto` and `Generate*RequestDto` (or mark `[JsonIgnore]`). Update UI accordingly.
  - Rationale: Prevents spoofing/cross-user actions.

## Phase 3: Ownership for Status Endpoints

- Files: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`, `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs`, `AI/ProfilePhotoMaker.API/Models/*`.
- Steps:
  - Add prediction ownership entity:
    - New `Models/Prediction.cs`: `Id` (predictionId, string), `UserId`, `Style`, `CreatedAt`.
    - Add `DbSet<Prediction>` to `ApplicationDbContext`.
  - Persist ownership on prediction creation:
    - In `ReplicateApiClient.GenerateImagesAsync` and `GenerateBasicImageAsync`, after receiving response, insert a `Prediction` row with `Id=result.Id`, `UserId`, `Style`, `CreatedAt=UtcNow`.
  - Enforce ownership in status endpoints:
    - `GET /api/replicate/train/status/{trainingId}`: ensure a `ModelCreationRequest` exists with `UserId == userId` and `PendingTrainingRequestId == trainingId` before calling `GetTrainingStatusAsync`.
    - `GET /api/replicate/generate/status/{predictionId}`: ensure a `Prediction` row for `userId` exists before calling `GetPredictionStatusAsync`; return `404` or `403` otherwise.
  - EF migration:
    - `dotnet ef migrations add AddPredictionsTable`
    - `dotnet ef database update`
  - Rationale: Prevents leakage of status info across users.

## Phase 4: Unify Credit Consumption Order

- File: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`.
- Steps:
  - In `GenerateBasicImage`, move `ConsumeCreditsAsync` to after successful prediction creation (align with other endpoints).
  - Confirm all generation/training endpoints only consume after successful client call; if consumption fails post‑creation, log and continue (job already running).
  - Rationale: Avoid charging for failed submissions.

## Phase 5: Full Mock Client (No Cost)

- Files: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/MockReplicateApiClient.cs` (new), `AI.ProfilePhotoMaker.API/Program.cs`.
- Steps:
  - Implement `MockReplicateApiClient : IReplicateApiClient`:
    - `CreateModelAsync(userId, modelName, ...)` → return `mock/{modelName}`.
    - `CreateModelTrainingAsync(userId, imageZipUrl)` → insert `ModelCreationRequest` with `Status=Creating`, `ReplicateModelId=mock/{modelName}`, return `ReplicateTrainingResult { id=guid, status="starting" }`. Optionally start a background task to flip DB row to `Ready` and set a fake `TrainedModelVersion` after a short delay.
    - `GenerateImagesAsync(trainedModelVersion, userId, style, ...)` → return `ReplicatePredictionResult { id=guid, status="starting" }`, insert `Prediction` row. Optionally simulate completion by caching a “succeeded” result with sample URLs after delay.
    - `GetTrainingStatusAsync`/`GetPredictionStatusAsync` → read mock state (in-memory or DB) and return appropriate status and `Output`.
    - `EnhancePhotoAsync` → same pattern, 1 output.
    - `CheckModelExistsAsync`/`CheckModelAvailabilityAsync` → `true`.
    - `GetModelVersionAsync` → return fake stable version id.
    - `CreatePredictionAsync` → synthesize a prediction result.
    - `FindUserModelsByPatternAsync` → return sample entries matching `user-{userId}-*`.
  - Conditional DI registration in `Program.cs`:
    - If `ENABLE_REPLICATE_MOCK=true`: `services.AddSingleton<IReplicateApiClient, MockReplicateApiClient>();`
    - Else: `services.AddHttpClient<IReplicateApiClient, ReplicateApiClient>();`
  - Keep skipping `Replicate.ReplicateApi` when mock is enabled (already present).
  - Rationale: No external calls, zero cost local dev/tests, high fidelity flow.

## Phase 6: Webhook Strategy in Mock

- Option A (recommended for simplicity): skip webhooks in mock and directly update DB state to reflect completion (training → Ready, predictions → Succeeded).
- Option B (E2E path): have the mock client POST payloads to your own webhook endpoints using internal `HttpClient` and a dev secret (or empty `Replicate:WebhookSecret` to bypass signature in Dev).

## Phase 7: Testing

- Unit tests (xUnit):
  - Controllers with Moq for `IReplicateApiClient`, EF Core InMemory for `ApplicationDbContext`.
  - Cases:
    - Training blocked if existing `Ready` model exists.
    - Training uses claims `userId`; mismatching DTO `UserId` rejected (if enabled).
    - Status endpoints reject non-owned IDs.
    - Credit consumption only after job creation for all endpoints.
- Integration tests (WebApplicationFactory):
  - Configure with `ENABLE_REPLICATE_MOCK=true`, InMemory DB, and empty `Replicate:WebhookSecret`.
  - Flows:
    - Train → GET status: `ModelCreationRequest` flips to `Ready`, `TrainedModelVersion` set.
    - Generate/Batch → GET status: `Prediction` row created, optional `ProcessedImage` rows appear.
- Local run:
  - Use `ENABLE_REPLICATE_MOCK=true` (env). `./dev-start.sh` already toggles mock when token invalid; prefer explicit env for clarity.
  - `dotnet test AI.ProfilePhotoMaker.API.Tests`.

## Phase 8: Data and Migrations

- Add `Prediction` entity and `DbSet`.
- Run EF migration for new table.
- No backfill required.

## Phase 9: Config and Scripts

- ENV:
  - Respect `ENABLE_REPLICATE_MOCK=true` as the switch.
  - In Development, allow empty `Replicate:WebhookSecret` to ease local runs (signature validator already skips when missing).
- Scripts:
  - Keep `dev-start.sh` mock inference, but document explicit override via `ENABLE_REPLICATE_MOCK=true`.
  - Update README with “Local Mock Mode” instructions.

## Phase 10: Rollout & Verification

- Dev (mock on):
  - Verify training blocked when a `Ready` model exists.
  - Verify `ModelCreationRequest.ReplicateModelId` stored as full `owner/model-name` and webhook reconciliation (when not mocked) updates the same row.
  - Verify ownership checks on status endpoints.
  - Verify credits only consumed post‑creation.
- Staging/Prod (mock off):
  - Set valid `REPLICATE_API_TOKEN`, HTTPS `AppBaseUrl` for webhooks.
  - Validate end‑to‑end flows and run `./dev-test.sh`.

## Risks & Mitigations

- Legacy rows with bare model names:
  - Treat as unavailable; prompt retraining or add a one-time migration/normalizer if needed.
- Mock drift vs real API:
  - Shape mock results to `ReplicateTrainingResult`/`ReplicatePredictionResult` schemas. Add logging if critical fields differ.

## Deliverables

- Code updates in:
  - `ReplicateApiClient.cs` (store full model ID),
  - `ReplicateController.cs` (claims user ID, status guards, credit timing),
  - `ReplicateWebhookController.cs` (consistent IDs),
  - `Program.cs` (conditional DI for mock).
- New files:
  - `MockReplicateApiClient.cs`,
  - `Models/Prediction.cs` (+ DbSet + EF migration).
- Tests:
  - Unit tests for ownership and credit timing,
  - Integration tests using mock client.

---

If you want, I can implement Phases 1, 2, 4, and 5 immediately to unblock local end‑to‑end testing without hitting Replicate.

