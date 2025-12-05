# Story 5.3: Retention & privacy enforcement

Status: done

## Story

As a user,
I want my data retained or deleted per policy,
so that privacy is respected.

## Acceptance Criteria

1. Retention job schedules deletions: originals after 7 days, generated after 30 days; manual endpoints: `GET /api/retentionpolicy/expired-images`, `POST /api/retentionpolicy/delete-expired`, `POST /api/retentionpolicy/initialize-retention-dates`.
2. Export and delete flows for photos/model/account are available; deletions remove files + DB records; audit logged.
3. Operations authorize per user; avoid PII in logs; retention metadata kept with images.
4. Background service runs safely and idempotently; repair endpoints available for admins/non-prod only.
5. UX: retention windows surfaced; warnings before destructive actions; accessible confirmations per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Background service: retention scheduler enforcing 7/30 day rules; configurable thresholds.
- [ ] Controller: Retention policy endpoints (list expired, delete expired, initialize retention dates); admin/non-prod guard for repair.
- [ ] Data/Storage: Store retention metadata per image; delete files + DB records atomically.
- [ ] Export/Delete flows: Ensure APIs tie into retention (account delete, model cleanup) and audit logging.
- [ ] Tests: Integration tests for retention job, manual delete endpoints, auth/ownership; ensure idempotent deletes and no orphaned files.

## Dev Notes

- Align with PRD retention; ensure background service has observability (logs/metrics) and safe retries.
- No public blob access; confirm storage provider honors deletion.
- Logging: structured; include counts; no filenames/PII.

### Project Structure Notes

- Background: AI.ProfilePhotoMaker.API/BackgroundServices/RetentionPolicyBackgroundService.cs
- Controller: AI.ProfilePhotoMaker.API/Controllers/RetentionPolicyController.cs
- Services: AI.ProfilePhotoMaker.API/Services/RetentionPolicyService.cs

### References

- bdocs/epics.md (E5 Story 5.3)
- docs/product/PRD.md (retention/privacy)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (storage/retention)
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Depends on images from Epic 2 and generated outputs from Epic 4; complements account deletion (5.4).

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/5-3-retention-privacy-enforcement.md
- Epic source: bdocs/epics.md (E5)
