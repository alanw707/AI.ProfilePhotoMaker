# ADR 0022: Career usage policy, cost ledger and operator kill switch

Status: accepted (2026-10-05, ticket #395). Builds on ADR 0009 (runtime allowance).

## Decision
- **Versioned policy, server-side.** `CareerUsagePolicy` options (`Career:Usage`): `PolicyVersion`, `MonthlyRunAllowance` (default stays the current value), `PerMinuteRunLimit` per user (default 6), `MaxConcurrentRunsPerUser` (2), `MaxQueuedRunsGlobal` (backpressure, 200), `MonthlyModelCostCapUsd` global (default 50) and per-user cap. Window = UTC calendar month; reset time = first instant of next month UTC, returned exactly. **Values are provisional until the owner approves the budget** (recorded in REVIEW).
- **Kill switches** (`CareerOperatorState` row, admin-editable, cached ≤ 30 s): `generationDisabled` (new model/agent runs → 503 `CareerGenerationPaused`), `sourcesDisabled` (external job source off). Reading, editing and exporting saved work never depend on them or on allowance.
- **Cost ledger.** `CareerUsageEvent` per run step/export/source call: owner (hashed id in reports), action, model, input/output tokens, estimated cost (from configured per-token prices), latency ms, outcome, source age; never prompts, resume bodies or outputs. Model cost is checked against caps before reservation; exceeding → 503 `CareerCostCapReached` (global) or 429 allowance-style (user).
- **Allowance UX.** `GET /api/career/allowance` → `{ policyVersion, limit, used, reserved, remaining, resetsAt }`; reservations settle on completion, release on failure/cancel/abandon (abandoned = reserved > 30 min, reaped).
- **Admin.** `GET /api/admin/career/usage?from&to` (Admin role): per-action counts, cost and latency p50/p95, per-active-user distribution (p50/p95/max), failure rate; `GET/PUT /api/admin/career/controls`. No photo-to-career credit conversion.
