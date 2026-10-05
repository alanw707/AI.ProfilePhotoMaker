# #390 summaries and exports: review

Open P0/P1: **none**

## Review findings

| ID | Sev | Area | Finding | Status |
|---|---|---|---|---|
| F1 | P2 | API | Link detection covered only http(s); `mailto:` was not linked | Fixed: http, https and mailto only, in PDF and DOCX; `javascript:`, `data:`, `file:` stay plain text (tests) |
| F2 | P2 | API | Human lines could cite unresolved fact ids | Fixed: unresolved ids on human lines are 409 `CareerResumeUnsupportedClaim` on save, restore and proposal apply; empty is allowed (tests) |
| F3 | P3 | API | 50 active exports returned 429 | Fixed: the oldest exports are deleted so the newest 50 remain; documented in `docs/career/api-summaries-exports.md` (test: 51st succeeds, oldest 404) |
| F4 | P3 | UI | Download object URL revoked after a fixed 1 s | Fixed: tracked URLs revoked after 60 s and all remaining in `ngOnDestroy` (Karma spec) |
