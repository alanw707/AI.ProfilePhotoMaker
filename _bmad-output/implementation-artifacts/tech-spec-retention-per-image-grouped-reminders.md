# Tech-Spec: Per-Image Retention With Grouped Reminder Emails

**Created:** 2025-12-26
**Status:** Ready for Development

## Overview

### Problem Statement
Retention reminders currently risk behaving as if there is a single retention date per user/batch, which conflicts with the policy that each image should retain its own lifecycle. Users also should not receive multiple reminder emails for multiple images expiring on the same day; they want a single email that includes all expiring images.

### Solution
- Enforce per-image retention dates based on the image creation/ready timestamp (the same moment the image is available for download).
- Keep reminders at 14-day and 7-day windows, but send **one email per user per day** per reminder window that includes all images expiring in that window (not one email per image).
- Update email content to include the expiring images (links/thumbnails) instead of only a count.

### Scope (In/Out)
**In scope**
- Reminder calculation based on `ProcessedImage.ScheduledDeletionDate` (per image).
- Reminder grouping per user/day, with dedupe at user/day/window level.
- Email template update to include all expiring images in the message.
- Test updates for reminder grouping and email payload changes.

**Out of scope**
- Changes to retention duration (still 30 days).
- UI changes (gallery display, retention badges, etc.).
- New notification channels (push/SMS).

## Context for Development

### Codebase Patterns
- Retention scheduling: `ProcessedImage.SetScheduledDeletionDate()` uses `CreatedAt` (`AI.ProfilePhotoMaker.API/Models/ProcessedImage.cs`).
- Reminder orchestration: `RetentionPolicyBackgroundService.SendDeletionWarningNotificationsAsync` groups images by user + deletion date and dedupes via `RetentionDeletionWarningLogs` (`AI.ProfilePhotoMaker.API/Services/RetentionPolicyBackgroundService.cs`).
- Reminder data query: `RetentionPolicyService.GetImagesApproachingDeletionAsync` returns images for a date window, grouped by user (`AI.ProfilePhotoMaker.API/Services/RetentionPolicyService.cs`).
- Email template: `EmailNotificationService.SendRetentionDeletionWarningAsync` currently takes a count + deletion date only (`AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs`).

### Files to Reference
- `AI.ProfilePhotoMaker.API/Models/ProcessedImage.cs`
- `AI.ProfilePhotoMaker.API/Services/RetentionPolicyService.cs`
- `AI.ProfilePhotoMaker.API/Services/RetentionPolicyBackgroundService.cs`
- `AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs`
- `AI.ProfilePhotoMaker.API/Models/RetentionDeletionWarningLog.cs`
- `AI.ProfilePhotoMaker.API/Services/IRetentionPolicyService.cs`
- Tests:
  - `AI.ProfilePhotoMaker.API.Tests/Integration/RetentionPolicyBackgroundServiceTests.cs`
  - `AI.ProfilePhotoMaker.API.Tests/Services/EmailNotificationServiceTests.cs`

### Technical Decisions
- Use `ProcessedImage.CreatedAt` as the retention start date ("ready for download" timestamp) and rely on per-image `ScheduledDeletionDate`.
- Deduplicate reminders per `userId + daysBeforeDeletion + sentDate` (sent day in UTC) to prevent multiple emails per user per day.
- Email payload should include a list of expiring images (URLs and/or IDs) so a single email can represent all expiring images.

## Implementation Plan

### Tasks
- [ ] Verify all image creation flows set `ProcessedImage.CreatedAt` and call `SetScheduledDeletionDate()` (Image upload, Replicate webhook, atomic image path). Avoid any batch-level override of `ScheduledDeletionDate`.
- [ ] Update `RetentionPolicyService.GetImagesApproachingDeletionAsync` and/or `RetentionPolicyBackgroundService.SendDeletionWarningNotificationsAsync` to aggregate **all images in the window per user** and send a single email per user per day (per reminder window).
- [ ] Change reminder dedupe logic to use `SentAtUtc.Date` (or equivalent day) rather than `DeletionDate` so each user gets at most one reminder email per day per window.
- [ ] Update `IEmailNotificationService.SendRetentionDeletionWarningAsync` and implementation to accept the list of expiring images (or URLs) and render them in the email (thumbnails + links). Include fallback text if URLs are missing.
- [ ] Update tests to reflect new method signature and grouping behavior:
  - `RetentionPolicyBackgroundServiceTests`: ensure single email per user per day with multiple images in payload.
  - `EmailNotificationServiceTests`: verify 14-day/7-day templates render with image list.

### Acceptance Criteria
- [ ] Given multiple images with different `ScheduledDeletionDate` values, when reminders run, then each image retains its own retention schedule (no shared date override).
- [ ] Given a user with multiple images expiring in the same reminder window on the same day, when reminders run, then exactly one email is sent for that user/window/day and it includes all expiring images.
- [ ] Given repeated runs on the same day, when reminders run, then duplicates are not sent for the same user/window/day.
- [ ] Given the 14-day and 7-day windows, when reminders run, then each window generates its own email if eligible, and each email includes the images expiring for that window.

## Additional Context

### Dependencies
- `RetentionDeletionWarningLogs` are used for dedupe; adjust logic but avoid schema changes unless necessary.
- Email service depends on configured delivery (Postmark/SMTP); behavior should remain best-effort.

### Testing Strategy
- Update integration tests in `AI.ProfilePhotoMaker.API.Tests/Integration/RetentionPolicyBackgroundServiceTests.cs` for new grouping + dedupe behavior.
- Update unit tests in `AI.ProfilePhotoMaker.API.Tests/Services/EmailNotificationServiceTests.cs` for new payload (image list).
- If needed, add a small unit test for the grouping logic (per user per day) in `RetentionPolicyServiceTests` or dedicated helper.

### Notes
- If existing `RetentionDeletionWarningLog` uniqueness constraints depend on `DeletionDate`, keep the field but adjust query logic to dedupe by `SentAtUtc.Date` to avoid DB migration.
- Keep reminder windows configurable via `RetentionNotificationOptions.NotificationDays`.
