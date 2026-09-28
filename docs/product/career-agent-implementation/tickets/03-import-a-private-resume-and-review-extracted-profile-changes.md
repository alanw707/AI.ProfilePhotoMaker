# 03: Import a private resume and review extracted profile changes

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

A user submits a PDF/DOCX, sees safe extraction progress, and accepts or rejects proposed changes to the confirmed profile without losing their own edits.

## Acceptance criteria

- [ ] Select parser and malware-scanning integration using licensed, bounded fixture evaluation; record configuration and measured extraction quality.
- [ ] Validate actual format, 10 MiB/25-page/100,000-character starting limits, archive expansion and encrypted/unsupported inputs; quarantine until scanning succeeds.
- [ ] Show consent/retention notice; use private opaque storage and owner-checked reads; configure raw-document expiry and explicit removal.
- [ ] Extract attributed facts as unconfirmed proposals, including conflicting dates and ambiguous claims; acceptance checks the current profile version.
- [ ] Scanned/unreadable documents offer manual/paste fallback; no OCR promise or silently empty profile.
- [ ] Test valid PDF/DOCX, malicious document text, zip bomb/oversize, scanner outage, extraction timeout, changed-profile conflict and cross-user download; redact content from logs.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/378 — Save a manual career profile and goal behind the career flag

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
