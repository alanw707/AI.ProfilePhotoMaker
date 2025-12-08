# Story 5.4: Data export & account deletion (user-controlled privacy)

Status: done

## Story

As a user,
I want to export and delete my data/account,
so that I control my presence.

## Acceptance Criteria

1. Export endpoint(s) provide user data (images metadata, profile) in a downloadable format; authorized per user.
2. Account delete flow cascades removal of images, models, credits records, tokens; revokes sessions.
3. Deletions clear retention queues and avoid resurrecting deleted data; returns completion confirmation.
4. Errors are safe/generic; audit logged without PII; ownership enforced.
5. UX: clear warnings/confirmations; accessible dialogs; progress/complete messaging per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: Export and account delete endpoints; require auth and confirmation semantics.
- [ ] Service: Cascade deletes across storage (uploads/enhanced/generated/training-zips), DB records (profiles, models, credits), tokens; revoke sessions/JWT validity if needed.
- [ ] Retention integration: Ensure retention job ignores deleted accounts; clear pending entries.
- [ ] Audit: Log export/delete events with user id; avoid PII.
- [ ] Tests: Integration tests for export success, delete cascade, auth enforcement; ensure no orphaned files; verify retention ignored post-delete.

## Dev Notes

- Coordinate with retention service (5.3) to avoid double work; ensure idempotent deletes.
- Provide download link or stream for export; avoid exposing storage paths.
- Consider grace-period flag if required by PRD; otherwise immediate delete.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/AccountPrivacyController.cs (or ProfileController extension)
- Services: AI.ProfilePhotoMaker.API/Services/AccountDeletionService.cs, ExportService.cs
- Storage: Delete user-scoped folders `/uploads/{userId}`, `/enhanced/{userId}`, `/generated/{userId}`, `/training-zips/{userId}.zip`.

### References

- bdocs/epics.md (E5 Story 5.4)
- docs/product/PRD.md (export/delete)
- docs/architecture/ARCHITECTURE_OVERVIEW.md
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Works with retention (5.3), credits/payments (5.2), and auth foundation (Epic 1).

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/5-4-data-export-account-deletion-user-controlled-privacy.md
- Epic source: bdocs/epics.md (E5)
