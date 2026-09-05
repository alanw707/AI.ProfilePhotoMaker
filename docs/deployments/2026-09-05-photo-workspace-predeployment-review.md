# Photo workspace pre-deployment review — 2026-09-05

## Decision

**NO-GO for production deployment.** Source and automated checks are healthy, but Stripe API-version coordination, credential rotation, and a real staging target remain unresolved.

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

### Spec findings fixed

1. Package HTTP errors recover through a deterministic retry fixture.
2. Payment ownership, package, amount, currency, replay, and temporary-provider failure paths remain fail-closed.
3. Incomplete candidate batches no longer appear as valid paid results.

### Open findings

1. **P1 — production Stripe endpoint incompatible with this release.** The enabled live endpoint uses `2025-08-27.basil`; Stripe.NET 49.1.0 expects `2025-10-29.clover`. Do not change the endpoint independently while the current Basil application is serving traffic. Coordinate endpoint and application rollout with a rollback plan.
2. **P1 — exposed Azure Storage account key requires rotation.** A read-only CLI inspection displayed the current key. Future Bicep deployments hide it behind a secret reference, but the current key remains compromised until production is switched to the alternate key and the exposed key is regenerated.
3. **P1 — no staging environment exists.** Azure contains only the production API/web apps, production SQL server/database, and production storage in `aiprofilemaker-v1`. Production-like migration/configuration and end-to-end smoke tests therefore cannot be completed safely without creating an isolated staging target.
4. **P1 — generation idempotency is not atomic across replicas.** Two simultaneous requests with the same client request ID can both pass the initial lookup. In a multi-replica race, duplicate provider work and cross-marking candidates remain possible. Add a database-backed request/idempotency record or lock before relying on automatic POST retries.
5. **P1 — discounted webhook fulfillment is not atomic across concurrent deliveries.** Sequential replay is safe after fulfillment, but concurrent delivery can race coupon redemption and package creation. Add database-backed Stripe event/idempotency handling before enabling high-volume discounted purchases.
6. **P2 — standalone premium add-on purchases are absent.** `CONTEXT.md` says premium augmentations may be purchased additionally; this release deliberately exposes only package-included allowances and says no standalone add-on is available.
7. **P2 — workspace component remains above repository complexity limits.** Lint reports 35 warnings: template complexity reaches 82, `startEnhancement` complexity reaches 78/212 lines, and the component reaches 2,309 lines. No lint errors occur; splitting the workflow is deferred to avoid an unrelated release refactor.
8. **Residual validation gap — paid AI visual quality.** Provider orchestration/accounting was tested with deterministic fixtures; identity preservation and image quality were not tested against paid providers.

## Environment checks

- Azure authentication: valid.
- Production Container App revision: healthy/running.
- Production SQL: read-only connection succeeded; 33 migrations applied; 0 pending.
- EF model: no changes since the latest migration.
- Database auto-migration: disabled in production.
- Stripe live endpoint: enabled; Basil API version; required PaymentIntent events configured.
- Staging: not found.

## Dependency remediation

Resolved all reported NuGet advisories:

- `SSH.NET` pinned to 2026.0.0.
- `SQLitePCLRaw.lib.e_sqlite3` pinned to 2.1.12 in the test project.
- `Azure.Extensions.AspNetCore.DataProtection.Blobs` updated to 1.5.4, resolving the vulnerable `System.Security.Cryptography.Xml` graph.

`dotnet list package --vulnerable --include-transitive` reports no vulnerable packages for API or test projects.

## Verification

- API: **386 passed, 0 failed**.
- Angular/Karma: **465 passed, 19 skipped**.
- Focused Playwright after review fixes: **8 passed** across pricing/dashboard, workspace recovery/accessibility, and premium generation.
- Production UI build: passed; 26 SEO pages generated.
- ESLint: 0 errors, 35 documented complexity warnings.
- Bicep compilation: passed.
- `dotnet ef migrations has-pending-model-changes`: none.
- Production migration query: 0 pending.
- NuGet vulnerability scan: 0 vulnerable packages.

## Required approvals/actions

1. Approve creation and cost envelope for an isolated staging API, web app, SQL database, and storage account—or provide an existing staging target.
2. Approve coordinated Azure Storage key failover/rotation.
3. Choose a coordinated Stripe rollout strategy for Basil → Clover.
4. Decide whether atomic generation/webhook idempotency and standalone add-on purchasing block this release.
5. Repeat staging sandbox checkout, webhook fulfillment, storage, export, and workspace smoke tests before changing the production decision.
