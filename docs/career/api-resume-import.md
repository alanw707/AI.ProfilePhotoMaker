# Career API: resume import and profile proposals (#379)

Same envelope, auth, flag (403 `CareerWorkspaceDisabled`) and error shape as `api-profile-goal.md`. Another owner's IDs return 404.

## Upload

`POST /api/career/resumes` — `multipart/form-data` with `file` (PDF or DOCX), `consent=true` and `consentVersion` (must equal the server's current notice version, `resume-notice-2026-10-04` unless `Career:ResumeConsentVersion` says otherwise).

| Status | Code | When |
|---|---|---|
| 201 | — | stored and processed; body is `ResumeDocumentDto` (state may be `ready`, `unreadable` or `failed`; an unexpected processing error is `failed`/`ParserError`, never a 500) |
| 400 | `ValidationError` (`fieldErrors.consent` / `fieldErrors.file`) | consent missing, `consentVersion` missing or not the current one (`consent`: "The notice changed; read it again."), no file |
| 413 | `CareerResumeTooLarge` | over 10 MiB, 25 pages or 100,000 characters |
| 415 | `CareerResumeUnsupported` | not a real PDF/DOCX, legacy `.doc`, encrypted file, or unsafe archive (`detail` says which) |
| 422 | `CareerResumeRejected` | the scanner found a threat (file deleted) |
| 503 | `CareerScannerUnavailable` | no scanner or scanner outage (file deleted); `retryAfterSeconds` given |

`ResumeDocumentDto`:

```json
{
  "id": "guid", "fileName": "resume.pdf", "format": "pdf", "sizeBytes": 48211, "pageCount": 2,
  "state": "ready",                // ready | unreadable | failed
  "failureCode": null,             // ExtractionTimeout | ParserError when failed
  "proposalId": "guid",            // null unless ready
  "consentVersion": "resume-notice-2026-10-04",
  "uploadedAt": "...", "expiresAt": "..."   // raw file auto-deleted at expiresAt
}
```

`GET /api/career/resumes` → list (newest first, max 20). `GET /api/career/resumes/{id}` → one. `GET /api/career/resumes/{id}/file` → the original bytes (owner only, `Content-Disposition: attachment`). The raw file is deleted for `failed` and `unreadable` resumes (and after expiry); those answer 404 `CareerResumeFileGone`, and the UI should offer the paste fallback. `DELETE /api/career/resumes/{id}` → 204; removes the raw file, metadata and any pending proposal from it.

## Proposals

`POST /api/career/profile/proposals` with `{ "text": "pasted resume text" }` (1–100,000 chars) → 201 `CareerProfileProposalDto` (source `pasted`). The paste fallback for unreadable files.

`GET /api/career/profile/proposals/{id}` → `CareerProfileProposalDto`:

```json
{
  "id": "guid", "source": "resume",          // resume | pasted
  "resumeId": "guid", "baseProfileVersion": 3, // null when no profile existed
  "status": "pending",                        // pending | accepted | dismissed
  "isStale": false,                           // true when the profile moved past baseProfileVersion
  "items": [
    { "id": "guid", "field": "currentTitle",  // currentTitle | industry | yearsExperience | location | summary | skills | highlights
      "value": "Senior Operations Lead",      // skills/highlights: one entry per item
      "currentValue": "Operations lead",      // null when not set
      "page": 1, "section": "Experience",
      "excerpt": "Senior Operations Lead, Regional Health Services, 2021–present",
      "flags": ["conflict"] }                 // conflict | ambiguous
  ],
  "createdAt": "..."
}
```

`POST /api/career/profile/proposals/{id}/accept` with `{ "itemIds": ["guid", ...] }`. Requires `If-Match` with the current profile ETag when a profile exists (428 missing, 412 stale or when the profile changed since the proposal). Returns the new `CareerProfileDto`; its `provenance.source` is `resume`/`pasted` and `provenance.sourceProposalId` is set. Skills and highlights are appended (de-duplicated); other fields replace. At least one item (400). Accepting marks the goal stale like any profile change.

`POST /api/career/profile/proposals/{id}/dismiss` → 200 proposal with `status: dismissed`.
