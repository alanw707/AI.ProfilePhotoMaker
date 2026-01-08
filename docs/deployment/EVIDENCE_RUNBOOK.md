# Production Evidence Runbook (MVP Launch)

Use this runbook to capture runtime artifacts in `docs/deployment/evidence/` for each pending acceptance criterion (AC) and production configuration (PC). Redact secrets and any PII before saving.

## Shared Conventions
- Environment: production.
- Use one evidence log per artifact based on `docs/deployment/evidence/evidence-log-template.md`.
- Attach raw JSON/logs/screenshots under `docs/deployment/evidence/`.
- Reference the evidence file in `docs/deployment/LAUNCH_READINESS_CHECKLIST.md` after capture.

## Local Preflight (Optional)
Use this for quick validation before production capture.
- Local UI base: `http://localhost:4200`
- Local API base: `http://localhost:5032`
- UI dev proxy maps `/api` to `http://localhost:5032` (`AI.ProfilePhotoMaker.UI/proxy.conf.json`).
- Do not store tokens or PII in evidence artifacts.

## Inputs Needed Before Run
- API base URL:
- UI base URL:
- Test account credentials (non-PII):
- Stripe test payment method (if applicable in prod):
- Replicate/OpenAI/Stripe tokens (do not store in evidence files):

## Evidence Map (Pending Items)
### AC-1 Upload validation (file limits/types + absolute URLs)
- Evidence file: `docs/deployment/evidence/upload-validation.json`
- Log file: `docs/deployment/evidence/upload-validation.log`
- Evidence log: `docs/deployment/evidence/upload-validation.md`
- Steps:
  1. Call upload endpoint with >20 files and expect rejection.
  2. Call upload endpoint with invalid type/size and expect rejection.
  3. Call upload endpoint with valid files and confirm absolute URLs returned.

### AC-2 Training ZIP creation (>=10 images)
- Evidence file: `docs/deployment/evidence/training-zip.json`
- Log file: `docs/deployment/evidence/training-zip.log`
- Evidence log: `docs/deployment/evidence/training-zip.md`
- Steps:
  1. Ensure account has >=10 images.
  2. Call training ZIP creation endpoint.
  3. Confirm public URL returned and accessible.

### AC-3 Training READY gate + credit deduction
- Evidence file: `docs/deployment/evidence/training-ready-gate.json`
- Log file: `docs/deployment/evidence/training-ready-gate.log`
- Evidence log: `docs/deployment/evidence/training-ready-gate.md`
- Steps:
  1. With READY model present, attempt training and confirm block.
  2. With no READY model, start training and confirm credits deducted.

### AC-5 Enhancement credits (Replicate + OpenAI)
- Evidence file: `docs/deployment/evidence/enhancement-credits.json`
- Log file: `docs/deployment/evidence/enhancement-credits.log`
- Evidence log: `docs/deployment/evidence/enhancement-credits.md`
- Steps:
  1. Run Replicate enhancement and confirm credit decrement (1).
  2. Run OpenAI enhancement and confirm credit decrement (1).
  3. Capture response payload with remaining credits.

### AC-7 Credit status + purchase flow
- Evidence file: `docs/deployment/evidence/credit-purchase.json`
- Log file: `docs/deployment/evidence/credit-purchase.log`
- Evidence log: `docs/deployment/evidence/credit-purchase.md`
- Steps:
  1. Fetch credit status/packages.
  2. Complete a purchase flow and confirm credits added.
  3. Capture Stripe webhook confirmation (redact identifiers).

### AC-8 Webhook ingestion (prediction complete)
- Evidence file: `docs/deployment/evidence/webhook-ingestion.json`
- Log file: `docs/deployment/evidence/webhook-ingestion.log`
- Evidence log: `docs/deployment/evidence/webhook-ingestion.md`
- Steps:
  1. Trigger a prediction and await webhook.
  2. Confirm images downloaded and retention set.

### PC-1 DB migrations at startup
- Evidence file: `docs/deployment/evidence/migrations-startup.log`
- Evidence log: `docs/deployment/evidence/migrations-startup.md`
- Steps:
  1. Capture startup logs showing migrations applied.
  2. Confirm schema version aligns with latest migration.

### PC-2 Replicate token configured
- Evidence file: `docs/deployment/evidence/replicate-smoke.json`
- Evidence log: `docs/deployment/evidence/replicate-smoke.md`
- Steps:
  1. Call a low-impact Replicate operation and confirm success.

### PC-4 CORS configured
- Evidence file: `docs/deployment/evidence/cors-config.json`
- Evidence log: `docs/deployment/evidence/cors-config.md`
- Steps:
  1. Validate response headers for production UI origin(s).

### PC-6 Storage URL resolution
- Evidence file: `docs/deployment/evidence/storage-url-check.json`
- Evidence log: `docs/deployment/evidence/storage-url-check.md`
- Steps:
  1. Fetch a stored image URL and confirm it resolves.

## Completion Checklist
- Update `docs/deployment/LAUNCH_READINESS_CHECKLIST.md` with evidence links and status.
- Update `docs/deployment/GO_NO_GO_SUMMARY.md` decision and blockers.
