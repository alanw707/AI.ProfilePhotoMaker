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
- Integrated Docker rebuild and complete fresh-account workflow passed: upload, score,
  preview, real Stripe sandbox delivery concurrency, purchase promotion, full nine-photo
  set, premium relighting, ZIP export and exact allowance accounting.
- Current Studio generates remaining candidates through individual idempotent requests.
  The purchased preview counts once: eight new slots plus one promoted preview.
  Package-covered generation and premium finishing consume zero legacy credits.
- Full Docker restart preserved SQL rows, allowances, migration state and Azurite images.
- Lint passed; production npm audit and NuGet dependency scan completed cleanly.
- Sanitized actual gate summaries: `docs/testing/evidence/photo-workspace-design-audit/production-integration-local-gates.txt`.
- Production rollout preflight: blocked as below.

Detailed local runner logs remain under `/tmp/aipm-production-*`; no credentials,
screenshots or private application data are committed.

## Production status

Not deployed. Integrated source frozen at `c03bf4d`; no production mutation during
integration/preflight so far. Production main remains `f49a6fc` with its last deployment
workflow successful. No push or production workflow dispatch has occurred.

Read-only refreshed configuration confirms the live Stripe endpoint remains enabled
on `2025-08-27.basil`; live API and webhook credentials are available. Storage still
uses key1. Secrets were resolved in memory, never printed or committed.

**Blocker:** SQL firewall rejects the operator host. Consequently production schema,
operation drain and data recovery readiness cannot yet be verified from this host.
Do not treat a configured SQL secret or successful Azure resource lookup as a successful
SQL verification. A temporary operator-IP-only access rule, removed after verification,
or an existing approved in-network execution path is needed before proceeding.
No firewall rule was added and no production data was changed.

Maintenance/rollback readiness and Stripe/Storage cutover remain unexecuted. Do not
push main or dispatch the production workflow before these prerequisites pass.
Do not confuse previous local audit approval with validation of this newly merged build.
