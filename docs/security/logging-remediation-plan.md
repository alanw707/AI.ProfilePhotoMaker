# Logging Sanitization Remediation Plan

CodeQL flagged numerous `Log entries created from user input` findings across the API codebase. This document tracks the remediation plan to ensure we sanitize all user-supplied values before they reach structured logs.

## Goals

1. Centralize logging sanitization to consistently scrub control characters and sensitive user input.
2. Eliminate existing CodeQL high-severity alerts related to untrusted data in logs.
3. Prevent regressions by documenting guidance and providing helper utilities/tests.

## Tasks

### 1. Shared Logging Sanitizer Utility
- [ ] Create `LoggingSanitizer` static helper (e.g., under `AI.ProfilePhotoMaker.API/Infrastructure/Logging/`)
  - Methods: `Sanitize(string? value)`, optional `SanitizeForId(string?)`, `SanitizeForPrompt(string?, int maxLength)`
  - Replace newline, carriage return, tab, and other control characters with safe placeholders
  - Collapse null/whitespace to `[redacted]`
- [ ] Unit tests covering common input patterns and boundary conditions

### 2. Replace Inline Sanitization
- [ ] Refactor `CreditPackageService` to consume the shared helper (remove local `SanitizeForLog`)
- [ ] Audit existing logging code across the API projects (controllers, services, clients) and replace manual scrubbing/formatting with helper usage

### 3. Clear CodeQL Alerts
- [ ] Review CodeQL report filtered by `Log entries created from user input`
- [ ] For each location (e.g., `ReplicateApiClient`, `OpenAImageGenerationService`, etc.), ensure sanitized logging
- [ ] Re-run CodeQL scan locally or via GitHub to verify alerts are resolved

### 4. Documentation & Guidance
- [ ] Add section to `AI.ProfilePhotoMaker.API/SECURITY_NOTES.md` covering logging hygiene and sanitizer usage
- [ ] Communicate guidelines to contributors (PR template or engineering channel)

### 5. Follow-Up Automation (Optional)
- [ ] Investigate Roslyn analyzer or custom linting rule to flag unsanitized logging of user input

## Tracking

Create issues or PR checklist items referencing this plan to ensure each step ships with review evidence.

