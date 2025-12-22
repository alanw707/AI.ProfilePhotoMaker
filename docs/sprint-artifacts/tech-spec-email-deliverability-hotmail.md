# Tech-Spec: Email Deliverability Improvements (Hotmail/Outlook)

**Created:** 2025-12-20
**Status:** In Progress (Postmark migration)

## Overview

### Problem Statement

Transactional emails from AI Profile Photo Maker (verification, welcome, receipts, status updates, support feedback) are landing in Hotmail/Outlook Junk and Gmail shows a suspicious message banner. Domain verification is completed and SPF/DKIM/DMARC pass, but delivery reputation and trust signals appear weak. We need consistent Inbox placement for Outlook/Hotmail without breaking other providers.

### Solution

Improve deliverability by hardening authentication alignment, reducing spam signals, and routing transactional traffic through Postmark:

- Add plain-text alternatives to all transactional emails.
- Align sender domain and Postmark authentication (SPF/DKIM/DMARC).
- Migrate transactional sends to the Postmark API for improved inbox placement.
- Move to a dedicated transactional subdomain to reduce shared-domain reputation issues.
- Update SPF/DMARC/DKIM to align with the transactional subdomain.
- Add logging and verification steps for deliverability.

### Scope (In/Out)

**In scope**
- All transactional emails sent by the API (verification, welcome, training, generation, purchase receipts, support feedback).
- API and SMTP send paths via `EmailNotificationService`.
- DNS and Postmark configuration changes required for the transactional subdomain.

**Out of scope**
- Marketing email campaigns.
- Major template redesign (copy changes only if needed for deliverability).
- UI changes unrelated to email.

## Context for Development

### Codebase Patterns

- Email sending handled in `AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs`.
- Config in `AI.ProfilePhotoMaker.API/Configuration/EmailOptions.cs` and `AI.ProfilePhotoMaker.API/appsettings.json` (overridable via env vars `Email__*`).
- Email API provider: Postmark (`https://api.postmarkapp.com/email`).
- SMTP path uses `MailMessage` with alternate views when text content is available.

### Files to Reference

- `AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs`
- `AI.ProfilePhotoMaker.API/Configuration/EmailOptions.cs`
- `AI.ProfilePhotoMaker.API/Program.cs`
- `AI.ProfilePhotoMaker.API/appsettings.json`
- `docker-compose.yml` (Email env vars)

### Technical Decisions

- Prefer Postmark API for production sends and explicitly include `TextBody`.
- Add a small HTML-to-text builder for transactional messages; avoid external dependencies.
- Move From address to the transactional sender domain (e.g., `no-reply@aiprofilephotomaker.com`).

## Implementation Plan

### Tasks

- [x] Task 1: Audit runtime email mode and ensure production is using Postmark API (not SMTP) when `Email__UseApi=true` and `Email__PostmarkServerToken` is set.
- [x] Task 2: Add plain-text content generation for all transactional emails.
  - Add a simple `EmailContentBuilder` to strip HTML tags and preserve critical CTA links.
  - For Postmark API: send both `HtmlBody` and `TextBody`.
  - For SMTP: set `Body` to text and add HTML as an alternate view, or use `AlternateViews` with both.
- [x] Task 3: Add Postmark API configuration and ensure TextBody + MessageStream are included; add tests.
- [x] Task 4: Update default configuration to use Postmark in production and local templates.
- [x] Task 5: Implement transactional sender domain support.
  - Update `FromEmail` (and optionally `ReplyTo`) to `no-reply@aiprofilephotomaker.com`.
  - Ensure Postmark sender/domain configuration supports the sender domain.
- [ ] Task 6: Update DNS records:
  - SPF for `aiprofilephotomaker.com` to include Postmark (e.g., `v=spf1 include:spf.mtasv.net -all`).
  - DKIM for the transactional sender domain (Postmark-provided CNAMEs).
  - DMARC for sender domain (start at `p=none` and move to `p=quarantine` after validation).
  - If a dedicated subdomain is adopted later, add SPF/DKIM/DMARC records for that subdomain.
- [x] Task 7: Log Postmark MessageID on API success for troubleshooting and support.
- [x] Task 8: Update operational docs with Postmark setup and verification checklist.

### Acceptance Criteria

- [ ] Emails to Hotmail/Outlook test accounts land in Inbox (not Junk) for verification and welcome emails.
- [ ] Gmail no longer shows a "suspicious message" warning for the same transactional emails.
- [x] All transactional emails include both HTML and plain-text parts.
- [ ] Authentication results show SPF/DKIM/DMARC pass and aligned with the From domain.
- [ ] Postmark API sends include `TextBody` and log a MessageID on success.

## Additional Context

### Dependencies

- Postmark account configuration: transactional domain and message stream.
- DNS changes for SPF/DKIM/DMARC on the transactional subdomain.
- Verification via Outlook/Hotmail and Gmail test accounts.

### Testing Strategy

- Unit: add tests for the HTML-to-text builder (ensure CTA links and key copy remain).
- Unit: verify Postmark API payload includes `TextBody` and MessageStream.
- Manual: send verification + welcome emails to Hotmail/Outlook and Gmail; confirm Inbox placement, images display without suspicious warning, and headers show aligned SPF/DKIM/DMARC.
- Observe Outlook SCL header and Authentication-Results for alignment.

### Notes

- Current DNS for root domain shows SPF only includes Cloudflare; this does not cover Postmark for the root domain. If root domain remains the From domain, SPF should include Postmark.
- DNS and Postmark console changes still require manual execution and validation to satisfy the acceptance criteria.

## Dev Agent Record

### File List
- `.github/workflows/simple-deploy.yml`
- `AI.ProfilePhotoMaker.API/appsettings.json`
- `AI.ProfilePhotoMaker.API/appsettings.Development.json.template`
- `AI.ProfilePhotoMaker.API/Configuration/EmailOptions.cs`
- `AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs`
- `docker-compose.yml`
- `docs/ENVIRONMENT_VARIABLES.md`
- `docs/INDEX.md`
- `docs/operations/EMAIL_DELIVERABILITY.md`
- `docs/sprint-artifacts/tech-spec-email-deliverability-hotmail.md`
- `AI.ProfilePhotoMaker.API.Tests/Services/EmailNotificationServiceTests.cs`
- `infrastructure/simple-deploy.bicep`

### Change Log
- Switched defaults to Postmark for transactional delivery and documented Postmark as primary.
- Added Postmark API payload test coverage for `TextBody` and MessageStream.
- Updated deliverability ops guide to Postmark-first guidance.
- Removed Brevo provider fallback references to keep a single email API path.
- Treat placeholder Postmark tokens as missing so the API falls back to SMTP.
- Note: working tree includes unrelated UI/auth changes outside this deliverability spec.

## Current Status (2025-12-20)

- Postmark migration in progress; config defaults updated to Postmark API.
- Auth checks pass (SPF/DKIM/DMARC) with `mail.aiprofilephotomaker.com`, but Gmail still shows "suspicious message".
- Transactional subdomain DNS records added in Cloudflare (SPF/DKIM/DMARC).

## Open Items / Next Steps

- Create a Postmark account and Server; capture the Server API token.
- Verify `mail.aiprofilephotomaker.com` in Postmark and add Postmark DKIM/SPF records.
- Set `Email__UseApi=true` and `Email__PostmarkServerToken` in production.
- Run Hotmail/Outlook + Gmail inbox placement verification.
