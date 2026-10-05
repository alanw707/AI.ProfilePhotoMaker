# Career API: professional summaries and exports (#390)

Design: ADR 0019. Same envelope/auth/flag as `api-targeted-resume.md`.

- `POST /api/career/runs { "task": "professional_summary", "materialId"? }` — steps `read_profile`, `read_goal`, `select_facts`, `draft_summary` "Drafted your summary", `save_summary`. Creates a `kind: summary` material (sections `short`, `long`) or a proposal for an existing one.
- `GET /api/career/materials?kind=summary|resume` (omit kind → both, each with `kind`). Other material endpoints are shared with the resume.
- `POST /api/career/materials/{id}/exports { format, version?, includePhoto? }` → 201 `{ id, format, version, includesPhoto, fileName, expiresAt, downloadUrl: "/api/career/exports/{id}" }`. 400 bad format/version/photo, 404 not owner.
- Retention: an owner keeps their newest 50 unexpired exports for 24 h each. Creating another deletes the oldest so at most 50 remain; there is no per-owner export 429. A deleted export downloads as 404.
- Links: only `http`, `https` and `mailto` URIs in text become hyperlinks in PDF and DOCX; other schemes (`javascript:`, `data:`, `file:`) stay plain text.
- Lines with `origin: human` may cite no facts, but any `factIds` supplied must resolve against the pinned profile, else 409 `CareerResumeUnsupportedClaim` (save, restore and proposal apply).
- `GET /api/career/exports/{id}` → file bytes (`application/pdf` or `application/vnd.openxmlformats-officedocument.wordprocessingml.document`), `Content-Disposition: attachment`, `Cache-Control: no-store, private`. 404 not owner/unknown, 410 `CareerExportExpired`.
- `GET /api/career/materials/{id}/exports` → recent unexpired exports for that material.
