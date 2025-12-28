# Tech-Spec: Retention Reminder Dedupe

**Created:** 2025-01-13
**Status:** Ready for Development

## Overview

### Problem Statement
Retention reminder emails (14-day and 7-day) are being sent multiple times in the same day. The current de-duplication is in-memory, so app restarts or multiple instances cause duplicate sends. We need persistent, per-user/per-deletion-date de-duplication so each reminder is sent once when the day is hit.

### Solution
Add a persistent send log for retention reminders keyed by user, deletion date, and reminder window (days-before). Update the background service to:
- Narrow the notification window to one day (exact target day).
- Group notifications per user and deletion date.
- Check the send log before sending.
- Record send success in the log (with unique constraint to prevent double sends across instances).

### Scope (In/Out)
**In scope**
- Persisted dedupe for 14-day and 7-day retention reminder emails.
- One email per user per deletion day per reminder window.
- Update retention background job logic and add EF Core migration.

**Out of scope**
- Changes to retention period (30 days) or deletion logic.
- Changes to email templates beyond ensuring correct payload.
- UI changes.

## Context for Development

### Codebase Patterns
- Background work: `RetentionPolicyBackgroundService`.
- Data access: EF Core via `ApplicationDbContext`.
- Options pattern in `Configuration/`.
- Email sending via `IEmailNotificationService`.

### Files to Reference
- `AI.ProfilePhotoMaker.API/Services/RetentionPolicyBackgroundService.cs`
- `AI.ProfilePhotoMaker.API/Services/RetentionPolicyService.cs`
- `AI.ProfilePhotoMaker.API/Services/IRetentionPolicyService.cs`
- `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs`
- `AI.ProfilePhotoMaker.API/Models/ProcessedImage.cs`
- `AI.ProfilePhotoMaker.API/Configuration/RetentionNotificationOptions.cs`

### Technical Decisions
- Add a new EF Core entity/table to persist retention reminders.
- Enforce uniqueness with a composite index: `UserId + DaysBeforeDeletion + DeletionDate`.
- Use database as source of truth to prevent duplicate emails across restarts/instances.

## Implementation Plan

### Tasks
- [ ] Add `RetentionDeletionWarningLog` entity with fields:
  - `Id` (int, PK)
  - `UserId` (string, required)
  - `DaysBeforeDeletion` (int, required)
  - `DeletionDate` (date only stored as DateTime with `.Date`)
  - `SentAtUtc` (DateTime, required)
- [ ] Add DbSet and configuration in `ApplicationDbContext` with unique index on `(UserId, DaysBeforeDeletion, DeletionDate)`.
- [ ] Create EF migration for the new table/index.
- [ ] Update `RetentionPolicyBackgroundService.SendDeletionWarningNotificationsAsync`:
  - Set notification window to 1 day (exact day).
  - For each user, group images by `ScheduledDeletionDate.Date`.
  - For each group, check if a log entry exists for that user/day/window.
  - If not, send one email with `imageCount = group.Count` and `deletionDate = group.Key`.
  - On success, insert log entry; handle unique constraint violations by treating as duplicate.
- [ ] Add an `IRetentionPolicyService` or small repository helper for log lookup/insert if you want to keep DB access out of the background service.
- [ ] Add or update tests to cover:
  - Duplicate prevention across multiple runs.
  - Exact-day matching for 14 and 7 day reminders.
  - One email per user per deletion date even with multiple images.

### Acceptance Criteria
- [ ] A user receives at most one 14-day reminder and one 7-day reminder per deletion date.
- [ ] Re-running the background job on the same day does not resend reminders.
- [ ] Dedupe remains effective across app restarts or multiple instances.
- [ ] Deletion still occurs at 30 days as before.

## Additional Context

### Dependencies
- EF Core migration and database update.

### Testing Strategy
- Unit test around the dedupe check/insert path (use in-memory or SQLite provider).
- Integration test that simulates multiple background runs and validates single email send.

### Notes
- The current 2-day notification window (`notificationWindowSizeDays = 2`) can produce repeated matches; setting it to 1 day aligns with “send on the day hit.” If you want a wider window for safety, the grouping-by-date plus unique index still prevents duplicates per day.
