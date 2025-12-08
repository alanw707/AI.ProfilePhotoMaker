# Story 5.1: Photo enhancement with weekly credits

Status: done

## Story

As a basic-tier user,
I want to enhance a photo,
so that I can improve it using my weekly credits.

## Acceptance Criteria

1. `POST /api/replicate/enhance` consumes 1 weekly credit; blocks when none remain; returns predictionId and remaining weekly credits.
2. Validates input image ownership; only user images are eligible.
3. Returns normalized URLs for enhanced image when ready or status link if async.
4. Errors are clear for insufficient credits or invalid selection; no PII in responses/logs.
5. UX: shows credit balance before action; loading/progress; inline errors; responsive layout per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: `POST /api/replicate/enhance`.
- [ ] Service: Validate ownership; check weekly credit balance; submit Replicate enhancement; decrement weekly credits; persist job.
- [ ] Data: Track weekly credit usage and reset markers; store enhancement job/results.
- [ ] Tests: Integration tests for credit check, ownership validation, success path; unit tests for credit decrement/reset logic.

## Dev Notes

- Weekly credits separate from purchased credits; align with PRD refresh cadence (weekly reset service from Epic 5.2).
- Ensure idempotent submission and no double-decrement on retries.
- Logging: structured with user id; no payloads.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs (enhance)
- Services: AI.ProfilePhotoMaker.API/Services/EnhancementService.cs, WeeklyCreditService.
- Data: Credit tracking and enhancement job entities.

### References

- bdocs/epics.md (E5 Story 5.1)
- docs/product/PRD.md (weekly credits)
- docs/architecture/ARCHITECTURE_OVERVIEW.md
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Depends on auth (Epic 1) and uploads (Epic 2); uses credit system (Epic 5.2) for status.

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/5-1-photo-enhancement-with-weekly-credits.md
- Epic source: bdocs/epics.md (E5)
