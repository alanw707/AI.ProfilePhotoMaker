# Story 4.3: Prediction complete webhook & gallery ingestion

Status: done

## Story

As a system,
I want to ingest generated images from webhooks,
so that users see outputs in their gallery.

## Acceptance Criteria

1. `POST /api/webhooks/replicate/prediction-complete` validates signature/time window; rejects invalid/stale payloads.
2. Downloads generated images to `/generated/{userId}`; creates DB records with retention dates (30 days) and normalized URLs.
3. Marks failures with retry-safe logs; idempotent on duplicate webhooks; no double-insert or double-download.
4. Secure: no public blob access; authorization enforced at retrieval time; logs contain no PII.
5. UX: gallery reflects new items with loading placeholders; delete/download accessible; retention window displayed where shown per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: Webhook endpoint for prediction complete with signature/timestamp validation (HMAC, 5-minute window).
- [ ] Service: Download assets, store in `/generated/{userId}`, create DB records with retention metadata, normalize URLs, handle retries idempotently.
- [ ] Security: Store webhook secret securely; avoid public blob access; guard against path traversal.
- [ ] Tests: Integration tests for valid/invalid signatures, duplicate webhook idempotency, storage/write failures; ensure DB + storage consistency.

## Dev Notes

- Reuse retention metadata model (Epic 5); set 30-day deletion markers.
- Idempotency key: predictionId + output URL/hash; skip existing.
- Logging: structured with predictionId; no payloads or tokens.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/Webhooks/ReplicatePredictionWebhookController.cs
- Services: AI.ProfilePhotoMaker.API/Services/PredictionWebhookService.cs
- Storage: `/generated/{userId}` paths.

### References

- bdocs/epics.md (E4 Story 4.3)
- docs/product/PRD.md (webhooks)
- docs/architecture/ARCHITECTURE_OVERVIEW.md
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Depends on generation jobs (4.1) and status tracking (4.2); uses retention policies (Epic 5) and auth foundation (Epic 1).

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/4-3-prediction-complete-webhook-gallery-ingestion.md
- Epic source: bdocs/epics.md (E4)
