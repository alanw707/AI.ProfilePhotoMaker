# Career workspace — Stage 3 readiness (2026-10-09)

**Recommendation: NO-GO on any Stage 3 change for now. Hold for 7 days, then decide.**
No allowlist, flag or cap was changed while writing this report.

## Key finding: there is no allowlist left to widen

Stage 2 (#434, deployed 2026-10-08 ~01:40 UTC) already set `CAREER_WORKSPACE_AUDIENCE: 'Everyone'`
(`.github/workflows/simple-deploy.yml:33`). Live check: `GET https://api.aiprofilephotomaker.com/api/config/client`
→ `"careerWorkspace":true`. Every signed-in user can open the workspace today.
So "Stage 3" can only mean one of these:
1. **Raise the caps**: $2 global per UTC day, $1 / 20 drafts per user per month, $25 global per month (policy `2026-10-launch`).
2. **Promote it**: marketing, email or homepage traffic aimed at the workspace.

Both increase spend and support load, so both need the private numbers below.

## Data window

From 2026-10-08 01:40 UTC to 2026-10-09: **about 1 day**. The launch REVIEW thresholds are daily and two-day, so this window is too short for a decision even with full data.

## What I could measure (public, no credentials)

| Signal | Result |
|---|---|
| Feature live | `/api/config/client` 200, `careerWorkspace: true` |
| API latency (public config, 5 probes) | 0.19–0.25 s |
| Auth enforcement | `/api/career/journey`, `/api/admin/career/usage` and `/api/admin/career/controls` all return 401 when unauthenticated (5/5 each) |
| Deploys since Stage 2 | 12 Simple Deploy runs on 10-08/10-09, all `success` (last 40 runs: 38 success, 2 failure, both older) |
| Photo flows after Stage 2 | load signed in with no 5xx (launch REVIEW, `stage2-photo-*`) |

## What I could not measure (gaps)

The admin endpoint needs an Admin session. This machine has no Azure CLI or prod database access, and I did not create or request credentials.

| Metric asked for | Source | Status |
|---|---|---|
| Active users, per-user distribution | `GET /api/admin/career/usage` → `perActiveUser`, `topUsers` | **gap** |
| Runs per task | usage → `actions[].count` | **gap** |
| Failure rate | usage → `failures`, `failureRate`, `actions[].failures` | **gap** |
| Spend vs cap | usage → `totalCostUsd`, `actions[].costUsd` | **gap** |
| Model latency p50/p95 | usage → `actions[].latencyP50Ms/P95Ms` | **gap** |
| Daily cap hits (`CareerDailyCostCapReached`) | App Insights / container logs (`aipm-api-v1`, rg `aiprofilemaker-v1`) | **gap** |
| 5xx rate on `/api/career/*` | App Insights / container logs | **gap** |
| Kill-switch state | `GET /api/admin/career/controls` | **gap** |

To fill the usage rows in one step (read-only GETs), copy the `Cookie` header from a signed-in admin browser session:

```bash
CAREER_ADMIN_COOKIE='…' scripts/career/stage3-usage.sh            # since the Stage 2 deploy
CAREER_ADMIN_COOKIE='…' scripts/career/stage3-usage.sh 2026-10-08T01:40:00Z 2026-10-15T01:40:00Z
```

## Go criteria (check on or after 2026-10-15, with 7 days of data)

Any Stage 3 change is GO only if all of these hold:
- `failureRate` < 10% overall and for each action, with no day above 10% (the launch threshold).
- `CareerDailyCostCapReached` on no two days in a row, and month-to-date spend on pace for under $20 of the $25 cap.
- No 5xx above 1% of `/api/career/*` requests in any 15-minute window.
- p95 model latency per action is acceptable (target: under 30 s for drafts).
- No photo checkout or fulfillment errors linked to career.
- Independent target-user observation (#377) shows people can finish setup → first draft without coaching. This is a precondition for **promotion**. It isn't needed to raise caps for existing usage.

If the caps are being hit while failures and 5xx stay low, raise the daily cap first (for example $2 → $5). That is cheap and reversible.
Promote only after #377.

## Why NO-GO now

- About 1 day of data, against thresholds defined over days.
- All of the decisive metrics (spend, failures, cap hits) are private and unmeasured here.
- Rollback and the kill switch are ready (launch REVIEW § Rollback), so there is no risk in waiting.
