# Story 2.3: Training ZIP creation & management

Status: done

## Story

As a user,
I want a training ZIP built from my uploads,
so that I can train a model.

## Acceptance Criteria

1. Auto-create ZIP when `ForTraining=true` on upload or via `POST /api/image/create-training-zip`; enforces ≥10 images.
2. ZIP stored at `/training-zips/{userId}.zip`; `GET/DELETE` endpoints list and remove training ZIP; idempotent rebuilds allowed.
3. Clear 400 when insufficient images; handles concurrent requests safely without corrupting ZIP.
4. Uses user-scoped storage and respects architecture storage paths; no public access.
5. UX: shows progress/status for ZIP creation; clear validation errors; responsive messaging per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: `POST /api/image/create-training-zip`, `GET /api/image/training-zip`, `DELETE /api/image/training-zip`.
- [ ] Service: Build ZIP from user uploads; enforce min count; handle concurrency (lock per user or queue); support idempotent rebuild.
- [ ] Storage: Save to `/training-zips/{userId}.zip`; ensure cleanup on delete; normalize URLs if returned.
- [ ] Validation: Ensure only images flagged/eligible included; return 400 if <10 images.
- [ ] Tests: Integration tests for create with sufficient/insufficient images, idempotent rebuild, delete/list, concurrency guard.

## Dev Notes

- Reuse upload storage from 2.1; ensure file reads are safe and paths validated.
- Consider background job for ZIP building if long-running; otherwise synchronous with timeouts.
- Logging: structured, user id only; no file paths; capture counts and durations.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/ImageController.cs (or TrainingZipController)
- Services: AI.ProfilePhotoMaker.API/Services/TrainingZipService.cs
- Storage: `/training-zips/{userId}.zip` per architecture doc.

### References

- bdocs/epics.md (E2 Story 2.3)
- docs/product/PRD.md (Training ZIP requirements)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (storage paths)
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Builds on uploads (2.1) and gallery data (2.2); ensure consistent URL/metadata handling.

## Dev Agent Record

### Context Reference

- Story file: bdocs/sprint-artifacts/2-3-training-zip-creation-management.md
- Epic source: bdocs/epics.md (E2)

### Completion Notes List

- Story context created via create-story workflow; status set to ready-for-dev.

### File List

- bdocs/sprint-artifacts/2-3-training-zip-creation-management.md
