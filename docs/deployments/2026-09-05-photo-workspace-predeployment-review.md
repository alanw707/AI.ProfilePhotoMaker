# Photo workspace pre-deployment review — 2026-09-05

## Decision

**NO-GO for production deployment.** Source, local full-stack verification, idempotency, and automated release gates are healthy. Production still requires separate authorization, coordinated Stripe API-version cutover, Azure Storage key rotation, and either an isolated staging target or an explicitly approved direct-canary exception.

## Reviewed scope

- Baseline: `main` at `bfbafbec94079627a9d9f7678a92e902f34fa60d`
- Release branch: `feature/photo-workspace-design-audit`
- Release commit before review fixes: `1ac254e`
- Scope includes the approved pre-audit workspace/header changes.
- Excluded: `.pi` state, private `/tmp/photo-workspace-audit/` data, generated storage, trace ZIPs, and large screenshots.
- No push, merge, deployment, or production mutation was performed.

No linked issue or acceptance test was available. Spec review used `CONTEXT.md`, `docs/testing/photo-workspace-design-audit.md`, and the approved audit scope.

## Adversarial review

### Standards findings fixed

1. **Legacy dashboard redirect dropped payment-return query parameters.** Replaced the static redirect with an Angular `UrlTree` redirect that preserves all query parameters; added Playwright coverage.
2. **Partially generated batches remained successful after a later provider/storage failure.** Incomplete batches are now marked failed and charged credits are refunded. Completed generation/accounting is not rolled back by later response/logging failures; added service coverage.
3. **Transient Stripe verification failure was acknowledged with HTTP 200.** It now propagates so Stripe retries delivery. Permanent fulfillment failures become `PendingReview` instead of appearing completed.
4. **`PendingReview` transactions could be reopened by webhook replay.** Replays now preserve the manual-review block. Out-of-order failed/canceled events no longer downgrade completed payments.
5. **Webhook transaction metadata could select a local transaction belonging to another PaymentIntent.** Metadata lookup now also requires the Stripe PaymentIntent ID to match.
6. **Production storage credentials were rendered as plain Container App environment values.** Bicep now stores the connection string as a Container App secret and references it from all three compatibility environment variables.
7. **EF deployment tooling targeted EF 8 while the application targets EF 10.** Local tool manifest updated to `dotnet-ef` 10.0.11.
8. **Retry and premium Playwright checks depended on live/local services.** Their package, profile-completion, and Turnstile behavior is now explicitly mocked.
9. **Generation idempotency was process-local and race-prone.** `HeadshotGenerationOperations` now provides a unique database claim, failure/recovery lease, and atomic accounting/completion transition. Separate-context concurrency coverage proves one provider call, one charge, and one successful operation.
10. **Stripe webhook replay/concurrency was not database-coordinated.** `StripeWebhookOperations` now claims `event-type:PaymentIntent` operations across replicas, retries failed operations, and rejects concurrent processing. Discount coupon redemption is tied idempotently to the payment transaction; purchase, credits, and entitlement are committed in one database transaction.
11. **Fresh/recovering SQL startup could be misclassified.** Migration startup now lets EF create a genuinely missing database and distinguishes an existing-but-recovering SQL database through `master`, waiting before migration rather than issuing a conflicting `CREATE DATABASE`.
12. **Production npm dependencies contained high-severity advisories.** Angular was upgraded from 19.2 to 20.3, Angular ESLint and TypeScript were aligned, and the legacy face-api transitive `node-fetch` was pinned to patched 2.7.0.

### Spec findings fixed

1. Package HTTP errors recover through a deterministic retry fixture.
2. Payment ownership, package, amount, currency, replay, and temporary-provider failure paths remain fail-closed.
3. Incomplete candidate batches no longer appear as valid paid results.

### Open findings

1. **P1 — production Stripe endpoint incompatible with this release.** The enabled live endpoint uses `2025-08-27.basil`; Stripe.NET 49.1.0 expects `2025-10-29.clover`. Do not change the endpoint independently while the current Basil application is serving traffic. Coordinate endpoint and application rollout with a rollback plan.
2. **P1 — exposed Azure Storage account key requires rotation.** A read-only CLI inspection displayed the current key. Future Bicep deployments hide it behind a secret reference, but the current key remains compromised until production is switched to the alternate key and the exposed key is regenerated.
3. **P1 — no staging environment exists.** Azure contains only the production API/web apps, production SQL server/database, and production storage in `aiprofilemaker-v1`. Production-like migration/configuration and end-to-end smoke tests therefore cannot be completed safely without creating an isolated staging target.
4. **P2 — standalone premium add-on purchases are absent.** `CONTEXT.md` says premium augmentations may be purchased additionally; this release deliberately exposes only package-included allowances and says no standalone add-on is available.
5. **P2 — workspace component remains above repository complexity limits.** Lint reports 35 warnings: template complexity reaches 82, `startEnhancement` complexity reaches 78/212 lines, and the component reaches 2,309 lines. No lint errors occur; splitting the workflow is deferred to avoid an unrelated release refactor.
6. **P2 — development-only npm audit debt remains.** Production dependency audit is clean. Full-tree audit reports 4 high and 8 moderate advisories confined to Puppeteer/Angular build and development-server tooling; fixing the Puppeteer chain requires a major v25 upgrade, while the current Angular build-tool chain has no published in-range fix. These packages are not shipped in the static frontend runtime.
7. **Residual validation gap — paid AI visual quality.** Provider orchestration/accounting was tested with deterministic fixtures; identity preservation and image quality were not tested against paid providers.

## Environment checks

- Azure authentication: valid.
- Production Container App revision: healthy/running.
- Production SQL at the read-only review point: 33 migrations applied and 0 pending against the pre-idempotency model. This release now contains 35 migrations; the two new additive migrations have not been applied to production.
- EF model: no changes since migration `20260905233853_AddStripeWebhookOperationIdempotency`.
- Database auto-migration: disabled in production.
- Stripe live endpoint: enabled; Basil API version; required PaymentIntent events configured.
- Staging: not found.

## Dependency remediation

Resolved all reported NuGet advisories:

- `SSH.NET` pinned to 2026.0.0.
- `SQLitePCLRaw.lib.e_sqlite3` pinned to 2.1.12 in the test project.
- `Azure.Extensions.AspNetCore.DataProtection.Blobs` updated to 1.5.4, resolving the vulnerable `System.Security.Cryptography.Xml` graph.

`dotnet list package --vulnerable --include-transitive` reports no vulnerable packages for API or test projects.

Frontend production dependencies were also remediated:

- Angular runtime/compiler packages: 20.3.30.
- Angular CLI/build tooling: 20.3.36; Angular ESLint: 20.7.0; TypeScript: 5.9.3.
- `node-fetch`: forced to 2.7.0 under `face-api.js`/TensorFlow through npm `overrides`.
- `npm audit --omit=dev --audit-level=high`: **0 vulnerabilities**.
- Full development-tree audit: 4 high and 8 moderate build/test-tool findings remain, as documented under Open findings.

## Verification

- API: **390 passed, 0 failed**.
- Angular/Karma on Angular 20: **465 passed, 19 skipped**.
- Focused Playwright on Angular 20: **8 passed** across pricing/dashboard, workspace recovery/accessibility, and premium generation. Two earlier attempts were environment/setup failures (no server, then an intentionally production-configured Docker frontend without the test-only auth bypass); the final intended development-host run passed after installing the Playwright 1.63 browser.
- Production UI build: passed; 26 SEO pages generated.
- ESLint: 0 errors, 35 documented complexity warnings in host and Docker frontend builds.
- Local Docker builds: API and Angular 20 frontend images passed (`Dockerfile.backend` and `Dockerfile.frontend`); API compiled with 0 warnings/errors.
- Fresh full Docker Compose runtime: SQL Server, Azurite, deterministic provider fixture, API, and frontend started; SQL/API/frontend health passed; API liveness, frontend root, and `/pricing/` passed.
- Fresh SQL migration: **35 migrations**, both idempotency tables, and all required unique indexes verified. Forward idempotent SQL contains no `DROP`, `TRUNCATE`, or `DELETE`.
- Full workflow stack restart: account, entitlement, and image rows persisted, and the stored Azurite image remained retrievable.
- Final fresh-schema Compose restart: API/frontend returned healthy and SQL retained all 35 migrations.
- Bicep compilation: passed.
- `dotnet ef migrations has-pending-model-changes`: none.
- NuGet vulnerability scan: 0 vulnerable packages in API and tests.
- npm production vulnerability scan: 0 vulnerabilities.
- Changed-file credential-pattern scan: clean.
- `git diff --check`: passed.

## Local integration evidence labels

- **Real Stripe test mode:** a Stripe CLI listener supplied a temporary signing secret outside the repository; a real test-mode PaymentIntent produced a signed webhook, 150 credits, and a Pro entitlement. Replay preserved single fulfillment. No real purchase occurred.
- **Deterministic AI fixture:** free preview, nine paid candidates, and premium relighting used a local OpenAI-compatible fixture. No paid AI call occurred; visual quality and identity preservation were not evaluated.
- **Real local persistence:** SQL Server and Azurite used isolated named volumes. Upload, generated-image retrieval, export ZIP, accounting, and restart persistence were exercised against those services rather than mocks.
- **Private evidence:** detailed scripts, credentials, event payloads, and account data remain under `/tmp/aipm-compose-verify/` and are intentionally excluded from commits.

## Required approvals/actions

1. Approve creation/cost for isolated staging, provide an existing target, or explicitly accept the higher-risk direct-production maintenance-window exception described in the rollout plan.
2. Approve coordinated Azure Storage alternate-key failover and compromised-key regeneration.
3. Approve the coordinated Stripe Basil → Clover endpoint/revision cutover and reconciliation window.
4. Approve the reviewed branch for commit/push/PR/merge and authorize a named operator and rollback owner.
5. Apply and verify the two additive idempotency migrations before routing traffic to the new revision.
6. Repeat checkout/webhook, storage, export, accounting, and workspace smoke on staging or the approved zero-traffic/canary production revision before changing the production decision.

See `docs/deployments/2026-09-06-photo-workspace-production-rollout-plan.md`. No production mutation, push, merge, or deployment was performed during this review.
