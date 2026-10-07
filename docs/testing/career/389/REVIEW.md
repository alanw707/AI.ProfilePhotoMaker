# #389 targeted resume: review

Branch `career/389-targeted-resume` off `feature/career-workspace`. Design: ADR 0018; contract: `docs/career/api-targeted-resume.md`. Independent review by openai-codex/gpt-5.5 (spec + standards).

## Gates (after fixes)
- API `dotnet test` (excl. Performance): 1170 passed, 1 skipped, 0 failed. Release `-warnaserror` is clean. EF: no pending model changes (`AddCareerMaterials` is additive).
- UI: lint 0 errors; Karma 692; `build:mvp-v1` succeeds. Playwright resume 16 passed (before the fixes: resume, roadmap and tracking 36 passed), with 0 axe WCAG 2.2 AA violations at 1280/390/320 and no overflow at 320.

## Acceptance criteria → evidence
| Criterion | Evidence |
|---|---|
| Private material/versions, pinned versions, fact provenance | `CareerMaterial`/`Version`; every generated line has resolvable `factIds`; owner-scoped, cross-user 404, deletion coverage |
| Only supported claims; missing metrics become questions | Highlights copied verbatim; a highlight with no digit adds a question; unresolved ids → 409 on save, restore and apply |
| Accessible editor, autosave, history, diffs, concurrency | aria-live save status; If-Match 428/412; a 412 keeps the draft; versions and restore; proposal partial apply; a human save during generation → apply 412 with the human text intact |
| Stale without deleting | `profile_changed`/`goal_changed`, content unchanged |
| No photo; contact defaults to name + email | API + Playwright defaults |
| Injection, long history, reload/recovery | Injection text stored verbatim with no PhD line; `<img onerror>` rendered as text; 60-line cap; sessionStorage draft restore |

## Review findings

| ID | Sev | Area | Finding | Status |
|----|-----|------|---------|--------|
| F1 | P2 | API | Restore skipped grounding check on the old version's generated lines | fixed: 409 CareerResumeUnsupportedClaim, no version appended |
| F2 | P2 | API | Apply proposal did not validate final generated lines | fixed: 409 before status change or append |
| F3 | P3 | UI | Line textarea maxlength 500 vs API 600 | fixed: bound to MAX_LINE_LENGTH (600) |
| F4 | P3 | UI | Autosave kept stale questions/facts/stale/contact | fixed: metadata refreshed from the response; line text untouched when newer edits exist |
| F5 | P3 | UI | Version GET typed as the material DTO | fixed: ResumeMaterialVersionDto, adapted in viewVersion |

Open P0/P1: **none**
