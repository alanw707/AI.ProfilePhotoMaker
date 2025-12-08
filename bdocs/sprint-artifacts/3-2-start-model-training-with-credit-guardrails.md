# Story 3.2: Start model training with credit guardrails

Status: done

## Story

As a user,
I want to start model training using purchased credits,
so that I can generate styled photos later.

## Acceptance Criteria

1. `POST /api/replicate/train` checks purchased credits (15) and blocks when insufficient; consumes credits after starting job.
2. Blocks retrain when a READY model exists; returns status endpoint `GET /api/replicate/train/status/{trainingId}`.
3. Associates training with latest training ZIP; enforces min image count (≥10).
4. Returns clear errors for insufficient credits or model state; logs events without PII.
5. UX: shows remaining credits and errors; loading/progress messaging per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: `POST /api/replicate/train`, `GET /api/replicate/train/status/{trainingId}`.
- [ ] Service: Validate credits (purchased), READY model guard, min images; submit Replicate training; consume credits; persist training job.
- [ ] Data: Model/training records with status, model version; link to user and training ZIP.
- [ ] Tests: Integration tests for sufficient/insufficient credits, READY-block, min images, status fetch; unit tests for credit guard logic.

## Dev Notes

- Use PRD rule: 15 purchased credits per training; block retrain if READY exists until user resets/archives.
- Ensure idempotent submission; guard against duplicate calls.
- Logging: structured, user id; no tokens; capture trainingId and model version.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs (or TrainingController)
- Services: AI.ProfilePhotoMaker.API/Services/TrainingService.cs, CreditConsumptionService.cs
- Data: Training/job entity; credit ledger.

### References

- bdocs/epics.md (E3 Story 3.2)
- docs/product/PRD.md (training credits)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (Replicate integration)
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Depends on styles (3.1), training ZIP (2.3), and credits (Epic 5). Auth foundation from Epic 1.

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/3-2-start-model-training-with-credit-guardrails.md
- Epic source: bdocs/epics.md (E3)
