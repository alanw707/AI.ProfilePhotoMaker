# Career API: targeted resume (#389)

Same envelope/auth/flag as `api-profile-goal.md`. Design: ADR 0018.

- `POST /api/career/runs` `{ "task": "targeted_resume", "materialId"?: guid }` + `Idempotency-Key`. Steps: `read_profile` "Read your confirmed profile", `read_goal` "Read your career goal", `select_facts` "Chose the facts that fit your target", `draft_resume` "Drafted your resume", `save_resume` "Saved your draft". Run DTO gains `materialId` and `proposalId`.
- `GET /api/career/materials?kind=resume` → `{ materials: [{ id, title, stale, currentVersion, updatedAt }] }`.
- `GET /api/career/materials/{id}` → `{ id, title, etag: "\"material-vN\"", currentVersion, pinned: { profileVersion, goalVersion, occupationCode }, stale, staleReasons, contact: { name: true, email: true, phone: false, location: false, links: false }, sections: [{ key: "headline|summary|experience_highlights|skills", lines: [{ id, text, factIds, origin }] }], questions: [{ id, factId, text }], facts: [{ id, text }] }` (`facts` = the pinned facts that lines cite, for display).
- `PUT /api/career/materials/{id}` `{ sections, contact }` + `If-Match` → 200 new version (`author: user`). 400 limits, 412/428, 409 `CareerResumeUnsupportedClaim` only for `origin: generated` lines with unresolved fact ids.
- `GET /api/career/materials/{id}/versions?page=1` → `{ versions: [{ number, author, createdAt }], total }`; `GET …/versions/{n}`; `POST …/versions/{n}/restore` + `If-Match` → new version.
- `GET /api/career/materials/{id}/proposals/{pid}` → `{ id, baseVersion, changes: [{ id, kind: added|removed|changed, section, before, after, factIds }] }`; `POST …/apply { acceptedChangeIds }` + `If-Match` (412 when the material moved since `baseVersion`); `POST …/reject`.
