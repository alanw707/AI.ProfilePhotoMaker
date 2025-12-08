# Story 2.1: Validated image upload

Status: done

## Story

As a user,
I want to upload photos with validation,
so that only acceptable images are stored.

## Acceptance Criteria

1. `POST /api/image/upload` accepts up to 20 files/request, each ≤10MB, types jpg/jpeg/png/webp with magic-byte validation; rejects invalid files with per-file errors; over-limit returns 400.
2. Files stored under user-scoped paths (`/uploads/{userId}` or `/enhanced/{userId}` when flagged) without path traversal; normalized absolute URLs returned for accepted files.
3. Structured error response lists accepted/rejected files with reasons; rejects entire request if limit exceeded.
4. Logging excludes PII/paths; validation errors are user-friendly; retries safe.
5. UX: drag/drop + picker; per-file errors inline; progress/complete indicators; responsive layout and accessible controls per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: `POST /api/image/upload` (AI.ProfilePhotoMaker.API/Controllers/ImageController.cs) with multipart handling.
- [ ] Service: Validate magic-bytes, size, count; persist to user-scoped storage; generate normalized URLs.
- [ ] Config: Max file size (10MB), max files (20) from config; storage roots for uploads/enhanced; ensure no public access.
- [ ] Error model: Return per-file validation results (accepted/rejected) with reasons.
- [ ] Tests: Integration tests for valid upload, type/size/count violations, traversal attempts; unit tests for validator.

## Dev Notes

- Use streaming/multipart reader to avoid buffering large payloads; enforce limits early.
- Normalize URLs using configured base (env-specific); store only userId-derived paths.
- Logging: structured, no file paths that reveal PII; include correlation id.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/ImageController.cs
- Services: AI.ProfilePhotoMaker.API/Services/ImageUploadService.cs, validators.
- Storage paths: per architecture `/uploads/{userId}` and `/enhanced/{userId}`.

### References

- bdocs/epics.md (E2 Story 2.1)
- docs/product/PRD.md (Upload requirements)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (storage paths, validation)
- docs/architecture/cloud-architecture.md
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Auth/session controls from Epic 1 provide user context and rate limiting; reuse auth middleware.

## Dev Agent Record

### Context Reference

- Story file: bdocs/sprint-artifacts/2-1-validated-image-upload.md
- Epic source: bdocs/epics.md (E2)

### Completion Notes List

- Story context created via create-story workflow; status set to ready-for-dev.

### File List

- bdocs/sprint-artifacts/2-1-validated-image-upload.md
