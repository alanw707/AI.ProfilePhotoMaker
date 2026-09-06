# Production deployment execution record

## Authorization

User explicitly approved direct-production deployment without staging, push/merge,
production migrations, Azure Storage key rotation, Stripe Basil-to-Clover cutover,
maintenance downtime, and rollback if checks fail. No real-money purchase or paid
AI call is included in verification.

## Preflight discovery

The previously audited release tip was `0b559a6`. Remote production main advanced
to `f49a6fc` with 62 commits absent from that branch. Do not deploy the old tip over
those changes. Integration occurs in isolated worktree `/tmp/aipm-production-integration`,
branch `release/photo-workspace-production-integration`.

Read-only production baseline:
- API revision: `aipm-api-v1--0000847`.
- Web revision: `aipm-web-v1--0000372`.
- Both image tags: `583-33517757517` in existing ACR.
- API uses single-revision mode.
- SQL online; seven-day PITR retention and Geo backup redundancy reported.
- Earliest restore date reported: 2026-08-30T07:17:04.336305Z.

Restore retention is not a completed restore rehearsal. Verify recovery point and
maintenance controls before mutations. Credentials must never enter this record.

## Merge decisions

- Retain current Studio layout, premium direction options, private preview promotion,
  Gallery refinement and partial candidate fulfillment semantics.
- Retain audited receipt/token fencing, fail-stop unknown outcomes, transaction retry
  handling, credit/allowance/coupon concurrency checks and strict Stripe compatibility.
- Existing purchase-promoted preview slots are not consumed again by generation.
- Current Gallery uses a dedicated refinement flow. Paid photos remain invalid free
  preview resume anchors; integration coverage checks this boundary and unchanged allowances.
- Temporary preview delivery failures/inactive packages remain retryable; definitive
  fulfillment validation errors retain audited manual-review handling.
- Retain both branches' regression coverage, adapting fixtures/selectors to the merged
  signature and current layout. Restore portrait-preview fallback and selection semantics.

## Integrated local gates

- API: 474 passed, zero failed.
- Karma: 499 passed, two existing skips.
- Production UI build passed; 26 SEO pages.
- Focused Playwright: nine passed, including current preview/premium flows.
- EF model consistency: no pending model changes.
- Full integrated Docker smoke, final security gates and rollout preflight: pending.

Detailed local runner logs remain under `/tmp/aipm-production-*`; no credentials,
screenshots or private application data are committed.

## Production status

Not deployed. No production mutation during integration/preflight so far.
Do not confuse previous local audit approval with validation of this newly merged build.
