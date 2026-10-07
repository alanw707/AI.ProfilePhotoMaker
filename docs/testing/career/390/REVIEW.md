# #390 Summaries and exports — review

Branch `career/390-summaries-exports` off `feature/career-workspace`. Design: ADR 0019; contract: `docs/career/api-summaries-exports.md`. Independent review by openai-codex/gpt-5.5 (spec + standards).

## Gates (after fixes)
- API `dotnet test` (excl. Performance): 1212 passed, 1 skipped, 0 failed. Release `-warnaserror` clean. EF: no pending model changes (`AddCareerExports` additive).
- UI: lint 0 errors; Karma 700; `build:mvp-v1` succeeds. Playwright materials-exports + resume + photo-handoff: 46 passed, 0 axe WCAG 2.2 AA violations at 1280/390/320, no overflow at 320.

## Acceptance criteria → evidence
| Criterion | Evidence |
|---|---|
| Factual summary, same review/version workflow, no publishing | `SummaryAssembler` cites facts verbatim (≤300/≤1200); shared material versions/proposals; copy-to-clipboard only, no publish button (Playwright) |
| PDF/DOCX of a selected version with headings and links | PDFsharp + DejaVu Sans (outline, link annotations); OpenXml Heading styles + hyperlinks; text order tested with PdfPig/OpenXml |
| Free at quota exhaustion, no ATS promise | Export is not a run; quota-exhausted API + UI tests; copy says we do not promise ATS behaviour |
| Private, expiring downloads | Owner 404, 410 after 24 h with row removed, `no-store, private` + attachment; newest 50 kept |
| No headshot by default | No image parts by default; photo only as a separate PDF with a selection |
| Short/long/multilingual/long-link, page breaks | Multi-page PDF with no line split; accented name extracts as text (CJK has no glyphs in DejaVu — noted); long URL wrapped and linked |
| Placeholder replaced | "later release" text gone; materials page lists resumes and summaries |


Open P0/P1: **none**

## Review findings

| ID | Sev | Area | Finding | Status |
|---|---|---|---|---|
| F1 | P2 | API | Link detection covered only http(s); `mailto:` was not linked | Fixed: http, https and mailto only, in PDF and DOCX; `javascript:`, `data:`, `file:` stay plain text (tests) |
| F2 | P2 | API | Human lines could cite unresolved fact ids | Fixed: unresolved ids on human lines are 409 `CareerResumeUnsupportedClaim` on save, restore and proposal apply; empty is allowed (tests) |
| F3 | P3 | API | 50 active exports returned 429 | Fixed: the oldest exports are deleted so the newest 50 remain; documented in `docs/career/api-summaries-exports.md` (test: 51st succeeds, oldest 404) |
| F4 | P3 | UI | Download object URL revoked after a fixed 1 s | Fixed: tracked URLs revoked after 60 s and all remaining in `ngOnDestroy` (Karma spec) |
