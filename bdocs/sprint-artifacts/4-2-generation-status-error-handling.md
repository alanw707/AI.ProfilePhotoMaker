# Story 4.2: Generation status & error handling

Status: done

## Story

As a user,
I want to track generation status,
so that I know when images are ready or if something failed.

## Acceptance Criteria

1. `GET /api/replicate/generate/status/{predictionId}` returns current status, outputs if ready, and errors if failed.
2. Handles timeouts/retries gracefully; consistent status values; user-friendly errors without leaking internals.
3. Pending jobs can be safely polled; responses include normalized URLs when available.
4. Logging excludes PII and secrets; correlates by predictionId/user id.
5. UX: polling/refresh indicators; empty state when pending; accessible status messaging per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: Status endpoint for generation jobs.
- [ ] Service: Fetch prediction state from Replicate/local record; map to consistent status; include outputs when ready.
- [ ] Data: Job records updated with statuses/errors/outputs; normalized URLs.
- [ ] Tests: Integration tests for pending/ready/failed states; invalid predictionId; ensure error messages are safe.

## Dev Notes

- Map Replicate statuses to product statuses; cache/store last known outputs to avoid redundant fetches.
- Guard against missing/foreign predictionIds (404 vs 403) based on ownership.
- Logging: structured with predictionId; no payloads.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs (status)
- Services: AI.ProfilePhotoMaker.API/Services/GenerationStatusService.cs
- Data: Generation job entity with status and outputs.

### References

- bdocs/epics.md (E4 Story 4.2)
- docs/product/PRD.md
- docs/architecture/ARCHITECTURE_OVERVIEW.md
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Depends on generation submission (4.1); uses uploads/styles/credits context; auth foundation.

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/4-2-generation-status-error-handling.md
- Epic source: bdocs/epics.md (E4)
