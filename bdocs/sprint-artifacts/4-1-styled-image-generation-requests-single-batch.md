# Story 4.1: Styled image generation requests (single & batch)

Status: done

## Story

As a user,
I want to generate styled images in batches,
so that I can get multiple outputs efficiently.

## Acceptance Criteria

1. `POST /api/replicate/generate` and `/api/replicate/generate/batch` accept 1–4 outputs per style per request; validate model availability and purchased credits (5 per output).
2. Rejects when model unavailable or insufficient credits with actionable errors; returns predictionId(s) and status endpoints.
3. Consumes credits upon successful submission; records jobs per user/style.
4. Logging without PII; errors are user-friendly.
5. UX: shows credit balance impacts before submit; progress/status feedback; responsive layout for batch selection per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: `POST /api/replicate/generate`, `/api/replicate/generate/batch`.
- [ ] Service: Validate model READY, credit balance (5/output), fan-out per style; submit Replicate prediction(s); persist job records.
- [ ] Data: Prediction/job entity with status, outputs expected, style ids.
- [ ] Tests: Integration tests for sufficient/insufficient credits, unavailable model, max outputs, job persistence; unit tests for credit calc.

## Dev Notes

- Respect PRD limits: 1–4 outputs, 5 credits each; model must be READY from 3.x.
- Ensure idempotent handling for retries; avoid double-charging credits.
- Logging: structured; include predictionId; no payloads or PII.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs (generation endpoints)
- Services: AI.ProfilePhotoMaker.API/Services/GenerationService.cs, CreditConsumptionService.
- Data: Generation job entity.

### References

- bdocs/epics.md (E4 Story 4.1)
- docs/product/PRD.md (generation limits/credits)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (Replicate flow)
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Depends on styles (3.1), training READY model (3.2/3.3), credits (Epic 5), uploads (Epic 2), auth (Epic 1).

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/4-1-styled-image-generation-requests-single-batch.md
- Epic source: bdocs/epics.md (E4)
