# Story 3.3: Training complete webhook handling

Status: done

## Story

As a system,
I want to process training-complete webhooks,
so that model status is updated and optional auto-generation can start.

## Acceptance Criteria

1. `POST /api/webhooks/replicate/training-complete` validates signature + 5-minute window; rejects invalid payloads with 400.
2. Updates model record to READY with model version; idempotent on retries/duplicates.
3. Optionally triggers generation for selected styles per config (fan-out) when training completes.
4. Logs audit entry without PII; errors are retry-safe; webhook handler resilient to failures.
5. UX: training status transitions surfaced to UI; user-facing messages avoid technical leakage.

## Tasks / Subtasks

- [ ] Controller: Webhook endpoint for training complete; apply signature/time validation (HMAC).
- [ ] Service: Update training/model records; mark READY; optional auto-generation trigger; ensure idempotency and retry safety.
- [ ] Security: Verify signatures, timestamps; reject stale or tampered requests; support replay guard.
- [ ] Tests: Integration tests for valid/invalid signature, stale timestamp, idempotent duplicate; verify model updated and optional fan-out.

## Dev Notes

- Store webhook secret in config/secrets; never log payloads or secrets.
- Idempotency key could be trainingId + event id; ensure safe persistence.
- Auto-generation should respect credit availability and user selections (Epic 4 expectations).

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/Webhooks/ReplicateTrainingWebhookController.cs
- Services: AI.ProfilePhotoMaker.API/Services/TrainingWebhookService.cs
- Data: Training/model entities with status and version.

### References

- bdocs/epics.md (E3 Story 3.3)
- docs/product/PRD.md (webhooks)
- docs/architecture/ARCHITECTURE_OVERVIEW.md

## Previous Story Intelligence

- Depends on training submission (3.2), styles (3.1), training ZIP (2.3), auth foundation (Epic 1), and credits (Epic 5 for generation fan-out).

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/3-3-training-complete-webhook-handling.md
- Epic source: bdocs/epics.md (E3)
