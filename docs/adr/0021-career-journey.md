# ADR 0021: One career journey — server-derived context, allowlisted actions, no hidden writes

Status: accepted (2026-10-05, ticket #393, spec #376). Connects ADRs 0006–0020.

## Decision
- **Journey summary endpoint.** `GET /api/career/journey` returns the single explicit context, derived server-side from saved state only: current profile version, current goal version (occupation, location), `nextAction` (one of a fixed list: `create_profile`, `confirm_profile`, `set_goal`, `confirm_occupation`, `build_brief`, `analyze_pay`, `build_roadmap`, `accept_roadmap`, `draft_resume`, `export_material`, `none`) with the page route, `latestResult` (newest completed artifact: kind, id, version, createdAt), `activeRuns` (queued/running/failed-recoverable runs with task and id) and `stale` artifacts. Reading it never writes. Visible context (query params, page state) never changes the saved goal; only the goal page's existing If-Match save does.
- **Allowlisted actions.** `ContextActions` is a fixed map from action key to an existing run task / route; the UI renders contextual buttons only from this list and passes the artifact ids and versions the journey returned. Unknown keys are ignored. Free text (including malicious "context" strings in URLs or profile fields) is never interpreted as an action.
- **Home.** Career home shows the goal, next useful action, latest result and any recoverable active work (resume polling via the shared run helper); links to all career pages (profile, occupation, market/markets, pay, jobs, roadmap, resume, summary/materials, photo, privacy). No placeholder flows remain.
- **No chat dependency.** Every structured edit/result is reachable without a chat surface; writes keep their acceptance workflows (profile proposals, roadmap accept, material proposals).
- **Recovery.** The existing per-page sessionStorage drafts and run start keys stay; home surfaces failed/interrupted runs with a retry that reuses the run's idempotency semantics.
