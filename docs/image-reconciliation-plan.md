# Image Reconciliation and Safe Repair Plan

## Context
- Some environments experienced unexpected drops in “uploaded photos” due to aggressive UI‑triggered database reconciliation.
- We’ve already gated repair behind feature flags and prevented destructive reconcile in production.
- Long‑term, we want automatic and safe cleanup with zero data loss risk.

## Goals
- Keep user photos in sync with blob storage.
- Eliminate orphaned DB records and orphaned blobs.
- Avoid false deletions caused by transient failures or path drift.
- Provide auditable, observable actions.

## Immediate Approach (Now)
- Use feature flags to prevent any UI‑triggered repair.
- Rely on existing 30‑day retention background job (`RetentionPolicyBackgroundService`) to delete items whose `ScheduledDeletionDate` has passed (30 days for both uploads and generated images).
- Production guard: API blocks destructive reconcile in production (returns 403 if `dryRun=false`).

## Future Safe Reconciliation (Design)
1) Backend‑driven, timed reconcile
- Introduce `ReconciliationBackgroundService` to run every 12h (configurable), not on user login.
- First phase: dry‑run only (reporting).

2) Multi‑check existence validation
- Exact path `ExistsAsync` check.
- Legacy path fallback (environment prefix aliases, path normalization, container mismatches).
- Classify as Present / Missing / Inconclusive (inconclusive never deletes).

3) Soft‑delete with grace windows
- Record suspects with `FirstSeenMissingAt`, `MissCount`.
- Soft‑delete after 2+ consecutive misses across ≥48h; exclude from UI.
- Hard‑delete after ≥7d in soft‑deleted state and re‑validation still missing.

4) Auditing and reporting
- Emit metrics: scanned, suspects, soft‑deleted, hard‑deleted.
- `/admin/reconcile/report` shows current suspects and last run summary (no secrets).
- Durable audit log entry per deletion (reason + evidence).

## Configuration
- Feature flags (UI): `enableAutoRepair`, `autoRepairDryRunOnly` (prod=true), thresholds/cooldown.
- API: disable destructive reconcile in production regardless of client flags.
- CORS (Azure Blob): ensure GET/HEAD are allowed for validation scenarios (future).

## Acceptance Criteria
- No user‑initiated reconcile calls.
- No destructive reconcile in production.
- Retention deletes after 30 days continue to function.
- Reconcile (when implemented) must meet: multi‑check present→missing classification, grace windows, soft‑delete before hard‑delete, full audit trail.

## Rollout Phases
- Phase A: Document + flags + retention only (current state).
- Phase B: Background reconcile (dry‑run + report).
- Phase C: Soft‑delete path with grace.
- Phase D: Hard‑delete after proven stability, guarded by config.

## Risks & Mitigations
- Transient storage/network failures → classify as Inconclusive; never delete.
- Path drift (env prefix/container) → legacy fallback checks.
- Operator error → destructive operations gated, logged, and off by default in prod.

