# MVP Launch Readiness Checklist

Last updated: 2025-12-23  
Source of truth: `docs/product/PRD.md` (Version 1.2, 2025-12-19)

## Launch Definition
- Launch type: GA
- Target date: TBD
- Scope: MVP per PRD (see Non-Goals)

## Status Legend
- Not Started
- In Progress
- Blocked
- Done

## Evidence Policy
- Desk review evidence (`docs/deployment/DOCS_CODE_AUDIT.md`) is provisional.
- Go/No-Go requires runtime artifacts captured in `docs/deployment/evidence/`.

## Go/No-Go Gates (MVP Acceptance Criteria)
Provisional desk-review evidence is summarized in `docs/deployment/DOCS_CODE_AUDIT.md`. Replace with runtime artifacts before GO.
| ID | Requirement | Owner | Status | Evidence |
| --- | --- | --- | --- | --- |
| AC-1 | Upload rejects >20 images or invalid types/sizes; success returns absolute URLs. | Alan | Done | Doc: `docs/deployment/DOCS_CODE_AUDIT.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ImageController.cs`; Evidence: `docs/deployment/evidence/upload-validation-production.json`. |
| AC-2 | Training ZIP created when >=10 images exist; returns public URL. | Alan | Done | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ImageController.cs`; Evidence: `docs/deployment/evidence/training-zip-production.json`. |
| AC-3 | Training blocks when READY model exists; requires 15 credits; consumes after starting. | Alan | Done | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`, `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs`; Evidence: `docs/deployment/evidence/training-start-production.json` (credit deduction), `docs/deployment/evidence/training-ready-gate-production.json`, `docs/deployment/evidence/training-ready-model-current-production.json`. |
| AC-4 | Generation requires 5 credits per output; fails gracefully when model unavailable. | Alan | Done | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`, `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs`; Evidence: owner attestation (production generation completed during training); prior artifacts `docs/deployment/evidence/generation-insufficient-credits.json`, `docs/deployment/evidence/model-status.json`. |
| AC-5 | Enhancement consumes 1 credit (Replicate or OpenAI styles); returns output and remaining credits. | Alan | Done | Doc: `docs/OPENAI-ENHANCEMENT.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`, `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`, `AI.ProfilePhotoMaker.UI/src/app/services/replicate.service.ts`; Evidence: `docs/deployment/evidence/enhancement-production.json`, `docs/deployment/evidence/enhancement-production.log`, `docs/deployment/evidence/enhancement-production.md`, `docs/deployment/evidence/enhancement-turnstile-failed-production.png` (manual run completed; credits 28 -> 27). |
| AC-6 | Retention background job sets and deletes data per policy; manual endpoints work. | Alan | Done | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Services/RetentionPolicyBackgroundService.cs`, `AI.ProfilePhotoMaker.API/Controllers/RetentionPolicyController.cs`; Evidence (production background job): `docs/deployment/evidence/retention-background-production.json`, `docs/deployment/evidence/retention-background-production.log`, `docs/deployment/evidence/retention-background-production.md`. Evidence (production manual endpoints): `docs/deployment/evidence/retention-policy-production.json`, `docs/deployment/evidence/retention-policy-production.log`, `docs/deployment/evidence/retention-policy-production.md`, `docs/deployment/evidence/retention-expired-images-production.json`, `docs/deployment/evidence/retention-expired-images-production.log`, `docs/deployment/evidence/retention-expired-images-production.md`, `docs/deployment/evidence/retention-delete-expired-production.json`, `docs/deployment/evidence/retention-delete-expired-production.log`, `docs/deployment/evidence/retention-delete-expired-production.md`. |
| AC-7 | Credit status/package endpoints return typed data; purchase adds credits to account. | Alan | Done | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/CreditController.cs`; Evidence: `docs/deployment/evidence/credit-status-packages-production.json`, `docs/deployment/evidence/credit-history-production.json`. |
| AC-8 | Webhook ingestion persists generated images and sets retention; downloads images locally. | Alan | Done | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs`; Evidence: `docs/deployment/evidence/webhook-ingestion-production.json`, `docs/deployment/evidence/webhook-ingestion-production.log`, `docs/deployment/evidence/webhook-ingestion-production.md`. |

## Production Configuration Checks (PRD Rollout Dependencies)
| ID | Requirement | Owner | Status | Evidence |
| --- | --- | --- | --- | --- |
| PC-1 | DB migrations apply at startup and schema is current. | Alan | Done | Doc: `docs/product/PRD.md`; Evidence: `docs/deployment/evidence/api-health-production.json`. |
| PC-2 | Replicate API token configured; training/generation/enhancement succeed. | Alan | Done | Doc: `docs/setup/ENVIRONMENT_SETUP.md`, `docs/ENVIRONMENT_VARIABLES.md`; Evidence: `docs/deployment/evidence/replicate-health-production.json`, `docs/deployment/evidence/replicate-generate-production.json`, `docs/deployment/evidence/replicate-generate-status-production.json`, `docs/deployment/evidence/replicate-generate-production.md`. |
| PC-3 | Stripe keys and webhook secret configured; payment simulation disabled in prod. | Alan | Done | Doc: `docs/setup/ENVIRONMENT_SETUP.md`, `docs/ENVIRONMENT_VARIABLES.md`; Evidence: `docs/deployment/evidence/payment-config.json`, owner attestation (production Stripe purchase completed). |
| PC-4 | CORS origins set for production UI domain(s). | Alan | Done | Doc: `docs/product/PRD.md`; Evidence: `docs/deployment/evidence/cors-config-production.txt`. |
| PC-5 | OAuth configuration validated (see `docs/deployment/DEPLOYMENT_CHECKLIST.md`). | Alan | Done | Doc: `docs/deployment/DEPLOYMENT_CHECKLIST.md`; Evidence: `docs/deployment/evidence/oauth-redirect-headers.txt`, owner attestation (Google OAuth login completed). |
| PC-6 | Storage configured (local or Azure Blob) and image URLs resolve. | Alan | Done | Doc: `docs/setup/ENVIRONMENT_SETUP.md`, `docs/ENVIRONMENT_VARIABLES.md`; Evidence: `docs/deployment/evidence/storage-url-check-production.txt`. |

## Non-Goals (MVP Exclusions)
- No subscription billing lifecycle.
- No admin analytics dashboard.
- No enterprise SSO or multi-tenant roles.

## Open Questions (PRD Section 16)
- Downloads require credits for premium tier? (Currently no.)
- Max total uploads per account/week? (Only per-request max enforced.)
- Admin moderation of styles or generated content?

## Related Docs
- `docs/product/PRD.md`
- `docs/development/DEVELOPMENT_BACKLOG.md`
- `docs/development/SPRINT_ROADMAP.md`
- `docs/deployment/DEPLOYMENT_CHECKLIST.md`
- `docs/deployment/COMPLIANCE_READINESS_CHECKLIST.md`
