# #389 targeted resume: review

## Review findings

| ID | Sev | Area | Finding | Status |
|----|-----|------|---------|--------|
| F1 | P2 | API | Restore skipped grounding check on the old version's generated lines | fixed: 409 CareerResumeUnsupportedClaim, no version appended |
| F2 | P2 | API | Apply proposal did not validate final generated lines | fixed: 409 before status change or append |
| F3 | P3 | UI | Line textarea maxlength 500 vs API 600 | fixed: bound to MAX_LINE_LENGTH (600) |
| F4 | P3 | UI | Autosave kept stale questions/facts/stale/contact | fixed: metadata refreshed from the response; line text untouched when newer edits exist |
| F5 | P3 | UI | Version GET typed as the material DTO | fixed: ResumeMaterialVersionDto, adapted in viewVersion |

Open P0/P1: **none**
