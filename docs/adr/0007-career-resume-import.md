# ADR 0007: Resume import produces reviewable proposals, never facts

Status: accepted (2026-10-04, ticket #379, spec #376)

## Context

#379 lets a user upload a PDF/DOCX and turn it into profile changes. The spec requires quarantine until scanning succeeds, private storage, bounded inputs, attributed unconfirmed proposals, version-checked acceptance, a manual/paste fallback and no OCR promise. No parser or malware-scanning vendor has been licensed; choosing one is a human gate on #379.

## Decision

- **Seams, not vendors.** `IMalwareScanner` and `IResumeParser` are interfaces. This slice ships a deterministic, dependency-free parser (DOCX text from `word/document.xml`; PDF text from literal strings in uncompressed or Flate-compressed content streams) plus a rule-based fact extractor, and a `NoThreatsScanner` placeholder that is registered only for LocalDev/Testing. **Production has no scanner registered, so uploads fail closed (503 `CareerScannerUnavailable`)** until an owner selects and configures a real one.
- **Limits checked before any parsing:** 10 MiB, 25 pages, 100,000 extracted characters; format by magic bytes (PDF `%PDF-`, DOCX = ZIP containing `word/document.xml`); encrypted PDFs, OLE/legacy or encrypted Office files, more than 200 ZIP entries, >50 MiB declared expansion or >100× expansion ratio are rejected with stable codes.
- **Pipeline (synchronous for this slice):** validate → store raw bytes under an opaque key `career-private/resumes/{guid}` → `quarantined` → scan → `extracting` → `ready` (proposal created) or `unreadable` (no text; paste fallback) or `failed` (timeout/parser error). A scanner outage or detected threat deletes the raw file. The async worker arrives with #380.
- **Privacy.** Raw files are never exposed by URL; the storage proxy refuses any `career-private` path. Reads go through the owner-checked `GET /api/career/resumes/{id}/file`. Raw documents expire after `Career:ResumeRetentionDays` (default 30) via a purge job; `DELETE` removes the raw file, metadata and pending proposals immediately. Logs carry IDs and codes only, never document text.
- **Proposals.** Extraction (or pasted text via `POST /api/career/profile/proposals`) creates a `CareerProfileProposal` pinned to the profile version at creation time, with items carrying field, value, page, section, excerpt and flags (`conflict`, `ambiguous`). Document text is data: nothing in it can trigger an action. Accepting chosen items requires the current profile `If-Match`; if the profile moved past the pinned version the answer is 412. Accepted items merge into a new profile version with `Source = "resume"` or `"pasted"` and `SourceProposalId`.
- New entities join `CareerPrivateDataService` deletion coverage.

## Consequences

- Extraction quality is fixture-measured only (see `docs/career/resume-import-quality.md`); a licensed parser is expected to replace the built-in one.
- No OCR: scanned PDFs are reported unreadable with the paste fallback.
