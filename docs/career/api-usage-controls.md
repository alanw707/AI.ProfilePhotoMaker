# Career usage controls API (ticket #395, ADR 0022)

Policy values are **provisional until the owner approves the budget**. Config: `Career:Usage` (`CareerUsagePolicy`).

| Option | Default |
| --- | --- |
| `PolicyVersion` | `2026-10-provisional` |
| `MonthlyRunAllowance` | unset: uses `Career:Agent:MonthlyRunAllowance` (20) |
| `PerMinuteRunLimit` (per user) | 6 |
| `MaxConcurrentRunsPerUser` (queued + working) | 2 |
| `MaxQueuedRunsGlobal` | 200 |
| `MonthlyModelCostCapUsd` (global) / `PerUserMonthlyModelCostCapUsd` | 50 / 2 |
| `ControlsCacheSeconds` | 30 |
| `AbandonedReservationMinutes` / `ReaperPollSeconds` | 30 / 300 |

Window = UTC calendar month. Standard envelope `{ success, data | error }`.

## `POST /api/career/runs` additional failures
Checked after the idempotent replay (a replay of an existing key always answers) and after allowance exhaustion.

| Status | `error.code` | When | Retry-After |
| --- | --- | --- | --- |
| 503 | `CareerGenerationPaused` | kill switch `generationDisabled` | 300 |
| 429 | `CareerAllowanceExhausted` | monthly allowance used + reserved | none |
| 429 | `CareerRateLimited` | `PerMinuteRunLimit` runs created in the last 60 s | seconds until the oldest leaves the window |
| 429 | `CareerConcurrencyLimit` | too many queued/working runs for the user | 10 |
| 503 | `CareerBusy` | global queued runs at `MaxQueuedRunsGlobal` | 30 |
| 429 | `CareerUserCostCapReached` | user's model cost this month at the per-user cap | none |
| 503 | `CareerCostCapReached` | global model cost this month at the cap | seconds to the next UTC month |

Only run creation (and the external job source, below) is gated. Reading, editing, exporting, replays, cancel and the allowance
endpoint never depend on a switch or on the allowance. The checks run inside the allowance-row race (its `Version` is a concurrency
token), so parallel creates cannot exceed the allowance or the concurrency limit.

`GET /api/career/jobs/observations` answers 503 `CareerGenerationPaused` when `sourcesDisabled` is set and the source would be called.

## `GET /api/career/allowance` (career flag required)
`data`: `{ policyVersion, limit, used, reserved, remaining, resetsAt }`. `remaining = max(0, limit - used - reserved)`;
`resetsAt` is the first instant of the next UTC month (ISO 8601, `Z`).

Reservations settle on completion, release on failure/cancel/abandon: spent if a model call started, otherwise returned.
**Abandoned**: a `queued`/`working` run whose `updatedAt` is older than 30 minutes. `CareerReservationReaper.ReapAsync()` (hosted
every 5 min by `CareerReservationReaperService`, callable on demand) fails it with `CareerRunAbandoned`, bumps its fencing token and settles
once. `needs_input` runs are not abandoned (the runner expires them itself).

## Admin (role `Admin`; 403 otherwise)
- `GET /api/admin/career/usage?from&to` (ISO instants, default last 30 days; `from >= to` is 400). `data`:
  `{ from, to, totalEvents, failures, failureRate, totalCostUsd, actions: [{ action, count, failures, costUsd, latencyP50Ms, latencyP95Ms }],
  perActiveUser: { activeUsers, eventsP50, eventsP95, eventsMax, costUsdP50, costUsdP95, costUsdMax },
  topUsers: [{ userHash, events, costUsd }] }`. Percentiles are nearest-rank. Owners appear only as a 12-char SHA-256 hash.
- `GET /api/admin/career/controls` -> `{ generationDisabled, sourcesDisabled, updatedAt, updatedBy }`.
- `PUT /api/admin/career/controls` body `{ generationDisabled?, sourcesDisabled? }` (at least one; omitted = unchanged). Takes effect
  immediately on this instance; other instances see it within 30 s (cache TTL).

## Data
Migration `AddCareerUsageControls` (additive: two tables, indexes).
- `CareerUsageEvent` (private, in `CoveredEntityTypes`, purged and exported): `ownerId, runId?, action` (`model_step|export|job_source`),
  `model` (type name), `tokens` (a single total: the model reports no input/output split), `costCents`, `latencyMs`,
  `outcome` (`ok|failed|timeout`), `sourceAgeSeconds?`, `createdAt`. Never prompts, resume or profile text, or model output.
  Model events are saved with the run's next write, so a call whose result is discarded by a lost lease fence is not ledgered.
- `CareerOperatorState` (single row, Id 1): operator state, not user data; exempt from purge/export (explicit in the coverage tests).
