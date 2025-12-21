# MVP Launch Readiness Checklist

Last updated: 2025-12-19  
Source of truth: `docs/product/PRD.md` (Version 1.2, 2025-12-19)

## Launch Definition
- Launch type: GA
- Target date: ASAP (TBD)
- Scope: MVP per PRD (see Non-Goals)

## Status Legend
- Not Started
- In Progress
- Blocked
- Done

## Go/No-Go Gates (MVP Acceptance Criteria)
Provisional desk-review evidence is summarized in `docs/deployment/DOCS_CODE_AUDIT.md`. Replace with runtime artifacts before GO.
| ID | Requirement | Owner | Status | Evidence |
| --- | --- | --- | --- | --- |
| AC-1 | Upload rejects >20 images or invalid types/sizes; success returns absolute URLs. | Alan | Not Started | Doc: `docs/deployment/DOCS_CODE_AUDIT.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ImageController.cs` |
| AC-2 | Training ZIP created when >=10 images exist; returns public URL. | Alan | Not Started | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ImageController.cs` |
| AC-3 | Training blocks when READY model exists; requires 15 purchased credits; consumes after starting. | Alan | In Progress | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`, `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs` |
| AC-4 | Generation requires purchased credits (5 per output); fails gracefully when model unavailable. | Alan | In Progress | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`, `AI.ProfilePhotoMaker.API/Models/CreditCostConfig.cs`; Evidence: `docs/deployment/evidence/model-status.json` (no trained model, purchased credits = 0) |
| AC-5 | Enhancement consumes 1 weekly credit (Replicate) or 2 credits (OpenAI styles); returns output and remaining credits. | Alan | Not Started | Doc: `docs/OPENAI-ENHANCEMENT.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`, `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`, `AI.ProfilePhotoMaker.UI/src/app/services/replicate.service.ts` |
| AC-6 | Retention background job sets and deletes data per policy; manual endpoints work. | Alan | Not Started | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Services/RetentionPolicyBackgroundService.cs`, `AI.ProfilePhotoMaker.API/Controllers/RetentionPolicyController.cs`; Evidence: `docs/deployment/evidence/retention-policy.json` |
| AC-7 | Credit status/package endpoints return typed data; purchase adds credits to account. | Alan | In Progress | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/CreditController.cs` |
| AC-8 | Webhook ingestion persists generated images and sets retention; downloads images locally. | Alan | Not Started | Doc: `docs/product/PRD.md`; Code: `AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs` |

## Production Configuration Checks (PRD Rollout Dependencies)
| ID | Requirement | Owner | Status | Evidence |
| --- | --- | --- | --- | --- |
| PC-1 | DB migrations apply at startup and schema is current. | Alan | In Progress | Doc: `docs/product/PRD.md` |
| PC-2 | Replicate API token configured; training/generation/enhancement succeed. | Alan | In Progress | Doc: `docs/setup/ENVIRONMENT_SETUP.md`, `docs/ENVIRONMENT_VARIABLES.md` |
| PC-3 | Stripe keys and webhook secret configured; payment simulation disabled in prod. | Alan | In Progress | Doc: `docs/setup/ENVIRONMENT_SETUP.md`, `docs/ENVIRONMENT_VARIABLES.md`; Evidence: `docs/deployment/evidence/payment-config.json` |
| PC-4 | CORS origins set for production UI domain(s). | Alan | In Progress | Doc: `docs/product/PRD.md` |
| PC-5 | OAuth configuration validated (see `docs/deployment/DEPLOYMENT_CHECKLIST.md`). | Alan | In Progress | Doc: `docs/deployment/DEPLOYMENT_CHECKLIST.md`; Evidence: `docs/deployment/evidence/oauth-redirect-headers.txt` |
| PC-6 | Storage configured (local or Azure Blob) and image URLs resolve. | Alan | In Progress | Doc: `docs/setup/ENVIRONMENT_SETUP.md`, `docs/ENVIRONMENT_VARIABLES.md` |

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
