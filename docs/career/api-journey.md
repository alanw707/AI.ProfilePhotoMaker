# Career API: journey summary (#393)

Design: ADR 0021. Auth + career flag as other career endpoints.

- `GET /api/career/journey` → `{ profile: { version, confirmed } | null, goal: { version, occupationCode, occupationTitle, location } | null, nextAction: { key, route }, latestResult: { kind, id, version, createdAt } | null, activeRuns: [{ id, task, status, startedAt }], stale: [{ kind, id, reasons }] }`. Read-only, owner-scoped, no side effects. 503 `CareerPrivacyReplayPending` like other career reads.
- `nextAction.key` ∈ `create_profile|confirm_profile|set_goal|confirm_occupation|build_brief|analyze_pay|build_roadmap|accept_roadmap|draft_resume|export_material|none`; `route` is an app path under `/app/career/...`.
