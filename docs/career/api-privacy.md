# Career API: privacy export, deletion and retention (#392)

Design: ADR 0020. Auth + career flag as other career endpoints.

- `GET /api/career/privacy/retention` → `{ items: [{ key, label, retention, notes }], processors: [{ name, purpose }] }`.
- `GET /api/career/privacy/export` → JSON attachment `career-export-<date>.json`, `{ format: "career-export/v1", generatedAt, sections: { CareerProfile: [...], ... } }`; every covered type has a (possibly empty) section; blobs listed as metadata.
- `POST /api/career/privacy/deletions { scope: "raw_documents"|"career_profile" }` → 202 `{ id, scope, status, attempts, lastError, createdAt, completedAt }`. 401 `CareerReauthRequired` when the sign-in is older than 10 minutes; 400 bad scope.
- `GET /api/career/privacy/deletions/{id}` → status (404 other owner). `POST /api/career/privacy/deletions/{id}/retry` → 202 (409 when completed).
- Account deletion (`DELETE /api/profile/account`) purges career data first.
- Raw uploads are auto-deleted after the configured retention (default 30 days) or when you delete them.
- While a startup tombstone replay is incomplete for the caller, career endpoints (including `export`) return 503 `CareerPrivacyReplayPending` with `Retry-After`; `retention` and the `deletions` endpoints keep working. Resume upload racing a deletion → 410 `CareerResumeDeleted`.
