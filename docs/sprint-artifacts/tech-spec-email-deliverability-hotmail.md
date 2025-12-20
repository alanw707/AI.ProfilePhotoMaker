# Tech-Spec: Email Deliverability Improvements (Hotmail/Outlook)

**Created:** 2025-12-20
**Status:** Completed

## Overview

### Problem Statement

Transactional emails from AI Profile Photo Maker (verification, welcome, receipts, status updates, support feedback) are landing in Hotmail/Outlook Junk and Gmail shows a suspicious message banner. Brevo domain verification is completed and SPF/DKIM/DMARC pass, but delivery reputation and trust signals appear weak. We need consistent Inbox placement for Outlook/Hotmail without breaking other providers.

### Solution

Improve deliverability by hardening authentication alignment, reducing spam signals, and aligning technical send paths with Brevo best practices:

- Add plain-text alternatives to all transactional emails.
- Align sender domain, return-path (MAIL FROM), and dedicated IP usage through Brevo.
- Add configurable Brevo API headers (including dedicated IP selector) and optional tracking controls.
- Move to a dedicated transactional subdomain and a custom tracking domain to reduce shared-domain reputation issues.
- Update SPF/DMARC/DKIM to align with the transactional subdomain.
- Add logging and verification steps for deliverability.

### Scope (In/Out)

**In scope**
- All transactional emails sent by the API (verification, welcome, training, generation, purchase receipts, support feedback).
- API and SMTP send paths via `EmailNotificationService`.
- DNS and Brevo configuration changes required for the new transactional subdomain, custom tracking domain, and dedicated IP.

**Out of scope**
- Marketing email campaigns.
- Major template redesign (copy changes only if needed for deliverability).
- UI changes unrelated to email.

## Context for Development

### Codebase Patterns

- Email sending handled in `AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs`.
- Config in `AI.ProfilePhotoMaker.API/Configuration/EmailOptions.cs` and `AI.ProfilePhotoMaker.API/appsettings.json` (overridable via env vars `Email__*`).
- Brevo API path: `https://api.brevo.com/v3/smtp/email` (current payload only sets `htmlContent`).
- SMTP path uses `MailMessage` with `IsBodyHtml = true` and no plain-text alternate view.

### Files to Reference

- `AI.ProfilePhotoMaker.API/Services/Notifications/EmailNotificationService.cs`
- `AI.ProfilePhotoMaker.API/Configuration/EmailOptions.cs`
- `AI.ProfilePhotoMaker.API/Program.cs`
- `AI.ProfilePhotoMaker.API/appsettings.json`
- `docker-compose.yml` (Email env vars)

### Technical Decisions

- Prefer Brevo API for production sends so we can set API headers (including dedicated IP selector) and explicitly include `textContent`.
- Add a small HTML-to-text builder for transactional messages; avoid external dependencies.
- Introduce config for optional Brevo API headers (e.g., `sender.ip`) and tracking flags once validated with Brevo.
- Move From address to a transactional subdomain (e.g., `no-reply@mail.aiprofilephotomaker.com`) and add a custom tracking domain (e.g., `links.aiprofilephotomaker.com`).

## Implementation Plan

### Tasks

- [x] Task 1: Audit runtime email mode and ensure production is using Brevo API (not SMTP) when `Email__UseApi=true` and `Email__ApiKey` is set.
- [x] Task 2: Add plain-text content generation for all transactional emails.
  - Add a simple `EmailContentBuilder` to strip HTML tags and preserve critical CTA links.
  - For Brevo API: send both `htmlContent` and `textContent`.
  - For SMTP: set `Body` to text and add HTML as an alternate view, or use `AlternateViews` with both.
- [x] Task 3: Add configurable Brevo API headers in `EmailOptions` (dictionary) and send them on API requests.
  - Include support for `sender.ip` when a dedicated IP is configured.
  - Keep header names configurable to avoid hardcoding unverified tracking toggles.
- [x] Task 4: Add config for dedicated IP and toggle in production configuration; log when used (without leaking secrets).
- [x] Task 5: Implement transactional subdomain support.
  - Update `FromEmail` (and optionally `ReplyTo`) to `no-reply@mail.aiprofilephotomaker.com`.
  - Ensure Brevo sender/domain configuration supports the subdomain.
- [x] Task 6: Add custom tracking domain configuration in Brevo and switch link tracking to `links.aiprofilephotomaker.com`.
- [x] Task 7: Update DNS records:
  - SPF for `mail.aiprofilephotomaker.com` to include Brevo (e.g., `v=spf1 include:spf.brevo.com -all`).
  - DKIM for the transactional subdomain (Brevo-provided CNAMEs).
  - DMARC for subdomain (start at `p=none` and move to `p=quarantine` after validation).
  - Ensure root domain SPF still includes any needed senders (Cloudflare + Brevo if root domain is used).
- [x] Task 8: Log Brevo response messageId on API success for troubleshooting and support.
- [x] Task 9: Update operational docs with new DNS records, Brevo settings, and verification checklist.

### Acceptance Criteria

- [ ] Emails to Hotmail/Outlook test accounts land in Inbox (not Junk) for verification and welcome emails.
- [ ] Gmail no longer shows a "suspicious message" warning for the same transactional emails.
- [ ] All transactional emails include both HTML and plain-text parts.
- [ ] Authentication results show SPF/DKIM/DMARC pass and aligned with the From domain.
- [ ] Brevo API sends include configured headers (including dedicated IP if set) and log a messageId on success.

## Additional Context

### Dependencies

- Brevo account configuration: transactional domain, dedicated IP, and custom tracking domain.
- DNS changes for SPF/DKIM/DMARC on the transactional subdomain.
- Verification via Outlook/Hotmail and Gmail test accounts.

### Testing Strategy

- Unit: add tests for the HTML-to-text builder (ensure CTA links and key copy remain).
- Manual: send verification + welcome emails to Hotmail/Outlook and Gmail; confirm Inbox placement, images display without suspicious warning, and headers show aligned SPF/DKIM/DMARC.
- Observe Outlook SCL header and Authentication-Results for alignment.

### Notes

- Current DNS for root domain shows SPF only includes Cloudflare; this does not cover Brevo for the root domain. If root domain remains the From domain, SPF should include Brevo.
- The sample headers show Return-Path on Brevo shared domain; a custom MAIL FROM domain should improve alignment and reputation.
- DNS and Brevo console changes still require manual execution and validation to satisfy the acceptance criteria.
