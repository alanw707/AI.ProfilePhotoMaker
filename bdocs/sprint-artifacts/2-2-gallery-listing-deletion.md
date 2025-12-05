# Story 2.2: Gallery listing & deletion

Status: done

## Story

As a user,
I want to view and delete my images,
so that I control my uploads.

## Acceptance Criteria

1. `GET /api/image/images` returns list with absolute URLs, type (original/enhanced/generated), createdAt, retention dates; filters by current user.
2. `DELETE /api/image/images/{imageId}` removes file + DB record; blocks path traversal; returns 404 for missing/not-owned items.
3. Optional repair/debug endpoints are gated to non-production or admin scopes; never exposed publicly in prod.
4. Responses avoid leaking other users’ data; authorization enforced via user context.
5. UX: thumbnails lazy-load; delete requires confirmation; empty state guidance; keyboard/focusable actions; no horizontal scroll on mobile; per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: `GET /api/image/images`, `DELETE /api/image/images/{id}`; optional repair endpoints with env/admin guard.
- [ ] Service: Fetch images scoped to user, join retention metadata; delete file + DB atomically; handle generated/enhanced/original types.
- [ ] Storage: Normalize URLs; ensure retention dates included; protect filesystem from traversal.
- [ ] Tests: Integration tests for list/filter, delete success, delete unauthorized/missing, repair endpoint gating.

## Dev Notes

- Reuse storage paths and normalization from 2.1; ensure retention metadata included for FR11 consistency.
- Logging: structured, no PII; include user id and image id; avoid logging file paths directly.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/ImageController.cs
- Services: AI.ProfilePhotoMaker.API/Services/ImageGalleryService.cs
- Data: images table/entity with type, timestamps, retention fields.

### References

- bdocs/epics.md (E2 Story 2.2)
- docs/product/PRD.md (Gallery requirements)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (storage, URLs)
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Depends on uploads from 2.1; ensure consistent URL and retention handling.

## Dev Agent Record

### Context Reference

- Story file: bdocs/sprint-artifacts/2-2-gallery-listing-deletion.md
- Epic source: bdocs/epics.md (E2)

### Completion Notes List

- Story context created via create-story workflow; status set to ready-for-dev.

### File List

- bdocs/sprint-artifacts/2-2-gallery-listing-deletion.md
