# 20: Verify release readiness, baseline parity and reversible rollout

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

Produce an evidence-backed release candidate and an executable same-domain rollout/rollback runbook. Actual production changes require a separate explicit release decision.

## Acceptance criteria

- [ ] Resolve dirty-worktree ownership and record approved implementation baseline; compare intended main SHA, built artifact digest and deployed production SHA/digest using CI/deployment evidence; unknown parity remains explicitly unresolved.
- [ ] Run authenticated API, deterministic market fixtures, real SQL concurrency, browser and existing photo fulfillment suites; record commands/build versions and results.
- [ ] Verify all six pages, no-purchase exports, covered personalized ranges across three occupation families/multiple geographies, sparse fallback, keyboard/mobile/zoom and user-comprehension review.
- [ ] Verify privacy/retention/provider rights, licensed data availability, source dates, model evaluation, actual approved budgets/quotas and no unauthorized side effects in the fixed evaluation set.
- [ ] Rehearse staff then invited-beta flag rollout and rollback in nonproduction: stop new career work, preserve data and photo delivery, restore old /app routing/public messaging, recover checkpoints safely.
- [ ] Record GO/NO-GO owner decision, monitoring thresholds, rollback owner and exact flag procedure; do not deploy, buy services, send applications or mutate production as part of planning.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/392 — Provide career privacy export, deletion and retention controls
- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/394 — Prepare the existing homepage for the free career core
- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/395 — Calibrate sustainable free usage and operator controls

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
