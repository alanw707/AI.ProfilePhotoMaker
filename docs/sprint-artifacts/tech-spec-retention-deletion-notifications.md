# Tech-Spec: Retention Policy Deletion Notification Emails

**Created:** 2025-12-23
**Status:** Ready for Development

## Overview

### Problem Statement
Users' images are automatically deleted after 30 days per the retention policy, but no notification emails are sent before deletion. This silent deletion can surprise users who may have forgotten about the 30-day policy, leading to poor user experience. Users deserve advance warning to allow them to download or extend retention of their images before they're permanently deleted.

### Solution
Implement a proactive email notification system that sends warning emails to users before their images are scheduled for deletion. Based on industry best practices and user feedback, send notifications at 14 days and 7 days before deletion to provide ample warning while maintaining reasonable email frequency. The notification system will:
- Identify images approaching deletion (within both notification windows)
- Send personalized emails to affected users at each notification stage
- Track sent notifications to prevent duplicate emails and ensure users receive both notifications at appropriate times
- Integrate seamlessly with existing retention background service

### Scope (In/Out)

**In Scope**
- Add method to `IRetentionPolicyService` to find images scheduled for deletion within X days
- Add `SendRetentionDeletionWarningAsync` method to `IEmailNotificationService` interface and implementation with support for different notification stages (14-day and 7-day warnings)
- Update `RetentionPolicyBackgroundService` to check for images approaching deletion at both notification windows (14 days and 7 days) and send appropriate notifications
- Track notification state to prevent duplicate emails (using scheduled deletion date window checks to determine which notification stage is appropriate)
- Email content includes: deletion date, image count, link to gallery, polite and clear messaging
- Respect email service configuration (enabled/disabled, sandbox mode)
- Handle edge cases: missing user email, email send failures, users with multiple images

**Out of Scope**
- User preferences for notification frequency (future enhancement)
- Extending retention period via email link (users can access gallery via link to manage their images)
- Notification emails for original uploads vs generated images separately (treat all images the same)
- Database schema changes for notification tracking (use scheduled deletion date window checks to determine notification stage)

## Context for Development

### Codebase Patterns
- Retention policy: `RetentionPolicyService` handles deletion logic; `RetentionPolicyBackgroundService` runs every 6 hours
- Email notifications: `EmailNotificationService` implements `IEmailNotificationService` with Postmark API and SMTP fallback
- User data: `UserProfile` links to `ApplicationUser` (via `UserId`); email stored in `ApplicationUser.Email`
- Image retention: `ProcessedImage.ScheduledDeletionDate` set to `CreatedAt.AddDays(30)` for all image types
- Background services: Use `IServiceProvider.CreateScope()` for scoped services; follow existing error handling patterns

### Files to Reference
- `AI.ProfilePhotoMaker.API/Services/RetentionPolicyService.cs`
- `AI.ProfilePhotoMaker.API/Services/IRetentionPolicyService.cs`
- `AI.ProfilePhotoMaker.API/Services/RetentionPolicyBackgroundService.cs`
- `AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs`
- `AI.ProfilePhotoMaker.API/Services/Notifications/IEmailNotificationService.cs`
- `AI.ProfilePhotoMaker.API/Models/ProcessedImage.cs`
- `AI.ProfilePhotoMaker.API/Models/UserProfile.cs`
- `AI.ProfilePhotoMaker.API/Models/ApplicationUser.cs`
- `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs`
- `AI.ProfilePhotoMaker.API.Tests/`

### Technical Decisions
- **Notification timing**: Two notification stages - 14 days before deletion (16 days after creation) and 7 days before deletion (23 days after creation). This provides users with early warning and a reminder closer to deletion date
- **Notification tracking**: Check `ScheduledDeletionDate` within specific day windows (e.g., 13-15 days before for 14-day notification, 6-8 days before for 7-day notification) to find images needing notification; use scheduled deletion date windows to determine which notification stage is appropriate; rely on scheduled deletion date being stable (not recalculated) to prevent repeated notifications at the same stage
- **Email frequency**: One notification per user per notification stage per image batch (group images by user, send one email per user even if multiple images are scheduled). Users will receive up to two emails total (14-day and 7-day warnings) for the same set of images
- **Email content**: Polite, professional tone. Include count of images scheduled for deletion, deletion date (formatted), link to gallery (where users can view/download their images), reminder about 30-day policy. Use friendly language that respects the user's attention
- **Integration point**: Add notification check in `RetentionPolicyBackgroundService.PerformRetentionPolicyCheck()` after setting retention dates but before deleting expired images. Check both 14-day and 7-day windows in each run
- **Error handling**: Log failures but don't block retention deletion process; email failures are non-critical
- **User email retrieval**: Query `UserProfile` with `.Include(up => up.User)` to access `ApplicationUser.Email`; skip users with null/empty email addresses

### Research Notes
- Industry best practices suggest notifications at 15 days and 25 days before deletion
- User preference: Notifications at 14 days and 7 days before deletion provide good balance of early warning and reminder
- Two notifications (14-day and 7-day) are reasonable and not excessive - users receive sufficient warning without email fatigue
- Notification should be clear and actionable with polite, professional tone
- Link to gallery allows users to view and download their images before deletion
- GDPR compliance: While not explicitly required, proactive notifications demonstrate good faith data handling and improve user trust

## Implementation Plan

### Tasks

1. **Add method to find images approaching deletion**
   - [ ] Add `GetImagesApproachingDeletionAsync(int daysBeforeDeletion, int windowSizeDays = 1)` method to `IRetentionPolicyService` interface
   - [ ] Implement in `RetentionPolicyService` to query images where `ScheduledDeletionDate` is between `now + daysBeforeDeletion - windowSizeDays/2` and `now + daysBeforeDeletion + windowSizeDays/2` (configurable window, default 1 day)
   - [ ] Include `UserProfile` and `ApplicationUser` in query to get email addresses
   - [ ] Return grouped results by user (userId, email, list of images)

2. **Add email notification method**
   - [ ] Add `SendRetentionDeletionWarningAsync(string userId, string? email, int imageCount, DateTime deletionDate, int daysUntilDeletion)` to `IEmailNotificationService` interface
   - [ ] Implement in `EmailNotificationService` with email template
   - [ ] Email template includes: polite subject (e.g., "Reminder: Your photos will be deleted soon" for 7-day, "Your photos will be deleted in 14 days" for 14-day), image count, deletion date (formatted), link to gallery, polite and clear messaging
   - [ ] Use existing `SendEmailAsync` infrastructure with template tag "retention-deletion-warning-{daysUntilDeletion}d" (e.g., "retention-deletion-warning-14d", "retention-deletion-warning-7d")
   - [ ] Handle null/empty email gracefully (log and return without sending)
   - [ ] Email tone should be polite, professional, and respectful

3. **Integrate notification into background service**
   - [ ] Update `RetentionPolicyBackgroundService.PerformRetentionPolicyCheck()` to call notification logic for both notification windows
   - [ ] After `SetRetentionDatesForExistingImagesAsync()`, check for images approaching deletion at both windows:
     - 14 days before deletion (check 13-15 day window)
     - 7 days before deletion (check 6-8 day window)
   - [ ] For each notification window, group images by user (userId)
   - [ ] Track sent notifications in-memory per window (Dictionary<int, HashSet<string>>) to prevent duplicates within same run
   - [ ] Send one email per user per notification window with aggregated image count
   - [ ] Log notification results (sent count per window, skipped count, errors)
   - [ ] Continue with existing deletion logic after notifications

4. **Add configuration for notification timing (optional)**
   - [ ] Consider adding `RetentionNotificationDays` array to appsettings (default: [14, 7]) for flexibility
   - [ ] Can be added later if needed; hardcode to [14, 7] for initial implementation

5. **Tests**
   - [ ] Unit test: `GetImagesApproachingDeletionAsync` returns correct images in date window for both 14-day and 7-day windows
   - [ ] Unit test: `SendRetentionDeletionWarningAsync` sends email with correct content and polite tone for each notification stage
   - [ ] Integration test: Background service sends 14-day notifications for images 14 days before deletion
   - [ ] Integration test: Background service sends 7-day notifications for images 7 days before deletion
   - [ ] Integration test: Users receive both 14-day and 7-day notifications at appropriate times (different background service runs)
   - [ ] Integration test: No duplicate emails sent for same user in single background service run for same notification window
   - [ ] Integration test: Users without email addresses are skipped
   - [ ] Integration test: Email failures don't block retention deletion process

### Acceptance Criteria

- [ ] Background service identifies images scheduled for deletion at both 14-day and 7-day windows
- [ ] Users receive 14-day warning email when they have images scheduled for deletion in approximately 14 days
- [ ] Users receive 7-day warning email when they have images scheduled for deletion in approximately 7 days
- [ ] Users receive one email per notification window per background service execution (no duplicates within same window in same run)
- [ ] Email includes: image count, deletion date (formatted), link to gallery, polite and professional messaging
- [ ] Email tone is polite, clear, and respectful
- [ ] No duplicate emails sent to same user within single background service execution for same notification window
- [ ] Users without email addresses are skipped (logged, not errored)
- [ ] Email send failures are logged but don't prevent retention deletion from proceeding
- [ ] Existing retention deletion functionality continues to work unchanged
- [ ] Notifications sent at appropriate times (14 days and 7 days before scheduled deletion)
- [ ] Email content is clear, professional, polite, and actionable

## Additional Context

### Dependencies
- Existing `EmailNotificationService` infrastructure (Postmark/SMTP)
- Existing `RetentionPolicyService` and background service patterns
- EF Core for querying `ProcessedImage` with user relationships

### Testing Strategy
- Unit tests for service methods (notification finding for both windows, email sending with different daysUntilDeletion values)
- Integration tests for background service notification flow at both 14-day and 7-day windows
- Test edge cases: missing emails, multiple images per user, email service disabled
- Verify notification timing accuracy (14 days and 7 days before deletion)
- Test that users receive both notifications at appropriate times (not both in same run)

### Notes
- Current retention policy: 30 days for all image types (originals and generated)
- Background service runs every 6 hours, so notifications will be sent within 6-hour window of the 14-day and 7-day marks
- Notification windows (14-day and 7-day) are non-overlapping, so users will receive both notifications at appropriate times
- Email tone: Polite, professional, and respectful - acknowledge that users' data is valuable to them
- Gallery link: Points users to their gallery where they can view and download images before deletion
- Consider future enhancements: User preferences for notification frequency, extension links
- Notification tracking uses scheduled deletion date windows to determine appropriate notification stage (in-memory tracking prevents duplicates within same service execution)

