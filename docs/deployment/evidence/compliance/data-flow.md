# Data Flow Inventory (Compliance Evidence)

Last updated: 2025-12-23  
Status: Evidence captured (documentation review).

## Evidence (production)
- Validated against PRD data flow and API retention evidence.
- PRD: docs/product/PRD.md
- Retention evidence: docs/deployment/evidence/retention-policy-production.json
- Expired images evidence: docs/deployment/evidence/retention-expired-images-production.json
- Captured: 2025-12-23T20:53:46Z

## 1. Data sources
- Account data (email, name, profile details).
- Uploaded photos and generated images.
- Usage and activity logs (credits, feature actions, timestamps).
- Payment metadata from Stripe (limited transaction details).

## 2. Processing and storage
- Uploads are stored in local or Azure storage and tracked in the database as ProcessedImage records.
- Model training and image generation use Replicate; enhancement uses Replicate or OpenAI (gpt-image-1).
- Outputs are stored in storage and referenced in the database for gallery and downloads.

## 3. Retention and deletion
- Input photos: deleted after 30 days.
- Generated images: deleted after 30 days.
- Retention policy endpoints expose and enforce the schedule; background services perform cleanup.
- Users can delete photos, models, all data, or their entire account from Settings.

## Evidence references
- PRD: `docs/product/PRD.md`
- Retention policy API: `AI.ProfilePhotoMaker.API/Controllers/RetentionPolicyController.cs`
- User deletion/export API: `AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs`
- Retention evidence: `docs/deployment/evidence/retention-policy-production.json`
- Expired images evidence: `docs/deployment/evidence/retention-expired-images-production.json`
- UI retention disclosure: `AI.ProfilePhotoMaker.UI/src/app/pages/retention-policy/retention-policy.component.ts`
