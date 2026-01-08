# Docs and Code Audit (Launch Readiness)

Date: 2025-12-19  
Scope: PRD v1.2, launch readiness docs, documentation index, and API controllers (desk review only).

## Sources Reviewed
- `docs/product/PRD.md`
- `docs/deployment/LAUNCH_READINESS_CHECKLIST.md`
- `docs/deployment/GO_NO_GO_SUMMARY.md`
- `docs/INDEX.md`
- API controllers:
  - `AI.ProfilePhotoMaker.API/Controllers/ImageController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/CreditController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/RetentionPolicyController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`
  - `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs`

## Key Findings

### Status and Scope Mismatch
- `docs/INDEX.md` previously stated "PRODUCTION READY" while `docs/development/DEVELOPMENT_BACKLOG.md` (2025-12-03) reports 93% complete with 5-6 weeks remaining.
- Resolved: index updated to "GA readiness in progress" with explicit links to readiness docs.

### OAuth Providers Mismatch
- `docs/INDEX.md` claimed Google, Facebook, and Apple sign-in support.
- Code only supports Google OAuth in `AuthController.cs`.
- Resolved: index updated to Google-only support.

### Photo Enhancement Divergence
- Code supports both Replicate and OpenAI enhancement paths; both consume 1 credit from the unified balance.
- Resolved: PRD and provider docs updated to reflect dual-provider behavior and unified credit costs. Decision: keep both providers.

### Archived / Historical Docs Present in Canonical Index
Several documents are explicitly marked as archived or historical but still appear in the primary index:
- `docs/deployment/DEPLOYMENT_OPTIONS.md`
- `docs/deployment/DEPLOYMENT_STRATEGY.md`
- `docs/deployment/WORKFLOW_VALIDATION.md`
- `docs/PRODUCTION_MIGRATION_GUIDE.md`
- `docs/unified-secrets-management.md`
- `docs/TROUBLESHOOTING-IMAGE-UPLOAD.md`
- `docs/refactor/playwright-suite-overview.md`
- `docs/refactor/cleanup-checklist.md`

Resolved: moved into a "Historical/Archived" section of the index (no file moves yet). Optional references (`docs/SignalR-Integration-Example.md`, `docs/replicate-workflow-implementation-plan.md`) now live under "Optional / Design References."

## PRD Acceptance Criteria to Code Mapping (Provisional)

| AC | Requirement | Primary Code References | Notes |
| --- | --- | --- | --- |
| AC-1 | Upload rejects >20 images or invalid types/sizes; success returns absolute URLs. | `ImageController.cs` (`UploadImages`, `IsValidImageFile`) | 20 file cap, 10MB limit, extension + magic bytes; returns storage URL via `GetImageUrl`. |
| AC-2 | Training ZIP created when >=10 images exist; returns public URL. | `ImageController.cs` (`CreateTrainingZipAsync`, `CreateTrainingZip`) | 10-image minimum enforced for ZIP creation. |
| AC-3 | Training blocks when READY model exists; requires 15 credits; consumes after starting. | `ReplicateController.cs` (`TrainModel`) | Checks READY model; uses `CreditCostConfig.ModelTraining` (15). |
| AC-4 | Generation requires 5 credits per output; fails gracefully when model unavailable. | `ReplicateController.cs` (`Generate`, `GenerateBatch`) | Uses `CreditCostConfig.StyledGeneration` (5) and availability checks. |
| AC-5 | Enhancement consumes 1 credit (Replicate or OpenAI styles); returns output and remaining credits. | `ReplicateController.cs` (`EnhancePhoto`), `EnhancementController.cs` | Dual-provider behavior documented in PRD. |
| AC-6 | Retention background job sets and deletes data per policy; manual endpoints work. | `RetentionPolicyBackgroundService.cs`, `RetentionPolicyController.cs` | 30/30 day policy exposed in controller. |
| AC-7 | Credit status/package endpoints return typed data; purchase adds credits to account. | `CreditController.cs`, `CreditCostConfig.cs` | Status + packages + purchase endpoints in controller. |
| AC-8 | Webhook ingestion persists generated images and sets retention; downloads images locally. | `ReplicateWebhookController.cs` | Downloads images, stores URLs, sets retention date. |

## Updates Applied (2025-12-19)
- Updated PRD to v1.2 with dual-provider enhancement details and credit costs.
- Aligned PRD training completion to polling (removed training webhook references).
- Updated `docs/OPENAI-ENHANCEMENT.md`, `docs/operations/PHOTO_PROCESSING.md`, and `docs/operations/API_REFERENCE.md` enhancement sections.
- Updated `docs/INDEX.md` status and added readiness + historical sections.

## Open Actions
- Keep dual-provider enhancement docs in sync if behavior changes.
