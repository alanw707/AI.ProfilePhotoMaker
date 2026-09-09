# Photo workspace production rollout and rollback plan

Date: 2026-09-06

Branch: `feature/photo-workspace-design-audit`

Production resource group: `aiprofilemaker-v1`

API Container App: `aipm-api-v1`

Frontend Container App: `aipm-web-v1`

## Current decision and authorization boundary

**NO-GO. This document is a plan, not deployment authorization.**

No production resource, secret, Storage key, Stripe endpoint, database, revision, traffic rule, or feature flag was changed while preparing this plan. No branch was pushed or merged and no image was published or deployed.

Execution requires named operators and explicit approval at each gate below. The current Container Apps use single-revision mode, and no isolated staging environment exists. Therefore either:

1. create/provide an isolated staging target and rehearse this plan there; or
2. explicitly approve the higher-risk direct-production maintenance-window path described below.

## Release compatibility snapshot

- Production database at the read-only review point: 33 applied migrations.
- Release database model: 39 migrations, ending at `20260906040831_ProtectCouponCapacityConcurrency`. The last three migrations are metadata-only (empty Up/Down): no database column or data changes.
- Production Stripe webhook endpoint: enabled, `2025-08-27.basil`.
- Release Stripe.NET: 49.1.0, expecting `2025-10-29.clover`.
- Production Storage key: considered compromised after read-only tooling displayed it; rotation is mandatory.
- Release adds database-backed generation and Stripe webhook idempotency.
- Latest local API suite: 415 passed. Full SQL/Azurite Compose smoke and restart persistence passed after final fixes. Independent completion audit is still required; paid AI image quality remains untested.
- Retry regression evidence includes coupon/purchase rollback, pre/post-commit failures, legacy-token replay, and generation lost-commit acknowledgement. See `docs/testing/evidence/photo-workspace-design-audit/final-local-release-gates.txt` and adjacent red/green artifacts.

## Owners and maintenance window

Assign before execution:

| Role | Responsibility | Required |
|---|---|---|
| Release operator | CI/image/revision execution | yes |
| Database operator | backup, migration, verification | yes |
| Azure/Storage operator | dual-key failover and key regeneration | yes |
| Stripe operator | checkout pause, endpoint version cutover, replay | yes |
| Product verifier | auth/workspace/payment/export smoke | yes |
| Rollback commander | sole go/rollback decision | yes |

Reserve a maintenance window long enough for migration, key failover, Stripe cutover, deployment, smoke, and event reconciliation. Keep every owner online until the observation gate closes.

## Approval gates

Do not combine these approvals implicitly.

- **A — release artifact:** reviewed commit/image digest, CI green, no private evidence or credentials included.
- **B — environment strategy:** staging rehearsal complete, or direct-production exception approved.
- **C — database:** backup/restoration point confirmed; three additive schema migrations and three metadata-only concurrency migrations approved.
- **D — Storage mutation:** alternate-key regeneration, secret switch, and compromised-key regeneration approved.
- **E — Stripe mutation:** checkout pause and Basil → Clover endpoint change approved.
- **F — deployment:** production revision/traffic change approved.
- **G — observation close:** accounting reconciliation clean; checkout may resume and release may be declared GO.

Any failed gate holds the release at NO-GO.

## Phase 0 — artifact freeze and preflight

1. Freeze the release commit. Exclude:
   - `.pi/**`
   - `/tmp/**`
   - `docs/testing/evidence/**` screenshots/traces not already approved
   - generated storage, test accounts, event payloads, and credentials
2. Record commit SHA, image tag, and immutable backend/frontend image digests.
3. Re-run CI from the frozen commit. Required results:
   - API tests: 415 passing or more, zero failures.
   - Karma: 465 passing, 19 known skips, zero failures.
   - Focused Playwright: 8 passing.
   - ESLint: zero errors; 35 known complexity warnings only.
   - production frontend and both Docker builds pass.
   - EF pending-model check, Bicep, NuGet, production npm audit, secret scan, and diff check pass.
4. Confirm Stripe live/test keys cannot be confused. Inspect only key prefixes and resource mode; never print complete values.
5. Confirm the production webhook URL remains `https://api.aiprofilephotomaker.com/api/webhooks/stripe` and record its endpoint ID securely.
6. Confirm no other deployment or database maintenance overlaps the window.

**Gate A:** release and rollback commander sign the immutable artifact record.

**Gate B:** staging rehearsal or direct-production exception is signed.

## Phase 1 — database backup and additive migration

The forward migration is database-first and compatible with the old application. It:

- creates `HeadshotGenerationOperations`;
- creates `StripeWebhookOperations`;
- adds nullable `CouponRedemptions.PaymentTransactionId`;
- adds the payment-transaction foreign key and filtered unique index;
- adds unique operation/event indexes;
- adds operation fencing tokens and a nullable candidate-operation token;
- does not drop, truncate, or delete existing data.

### Pre-migration

1. Create and verify a restorable production SQL backup/restore point according to the existing Azure SQL policy.
2. Capture read-only baselines:

```sql
SELECT COUNT(*) AS AppliedMigrations FROM dbo.__EFMigrationsHistory;
SELECT TOP (5) MigrationId
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId DESC;
SELECT Status, COUNT(*) AS Transactions
FROM dbo.PaymentTransactions
GROUP BY Status;
SELECT COUNT(*) AS PendingPurchases
FROM dbo.CreditPurchases
WHERE Status = 0;
```

3. Generate the idempotent SQL from the frozen commit and compare its hash with the reviewed artifact:

```bash
export ConnectionStrings__DefaultConnection='<design-time-only connection string>'
dotnet ef migrations script \
  20260518040142_AddOutcomePackages \
  20260906040831_ProtectCouponCapacityConcurrency \
  --idempotent \
  --project AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj \
  --startup-project AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj \
  --context ApplicationDbContext \
  --output idempotency-migrations.sql
```

Do not place the generated connection string or migration artifact in logs.

### Apply and verify

Apply through one controlled path only: reviewed SQL **or** `.github/workflows/simple-deploy.yml` with `run_db_migrations=true`. Do not run both.

```sql
SELECT MigrationId
FROM dbo.__EFMigrationsHistory
WHERE MigrationId IN (
  '20260905231745_AddHeadshotGenerationOperationIdempotency',
  '20260905233853_AddStripeWebhookOperationIdempotency',
  '20260906012642_AddOperationFencing',
  '20260906034641_ProtectCreditBalanceConcurrency',
  '20260906035812_ProtectPackageAllowanceConcurrency',
  '20260906040831_ProtectCouponCapacityConcurrency'
);

SELECT OBJECT_ID('dbo.HeadshotGenerationOperations') AS GenerationOperations,
       OBJECT_ID('dbo.StripeWebhookOperations') AS WebhookOperations;

SELECT COL_LENGTH('dbo.HeadshotGenerationOperations', 'OperationToken') AS GenerationFence,
       COL_LENGTH('dbo.StripeWebhookOperations', 'OperationToken') AS WebhookFence,
       COL_LENGTH('dbo.ProcessedImages', 'GenerationOperationToken') AS CandidateFence;

SELECT name, is_unique
FROM sys.indexes
WHERE name IN (
  'IX_HeadshotGenerationOperations_CorrelationId',
  'IX_StripeWebhookOperations_OperationKey',
  'IX_StripeWebhookOperations_StripeEventId',
  'IX_CouponRedemptions_PaymentTransactionId'
);
```

Expected: six migration rows, two non-null object IDs, and four unique indexes. Also verify `OperationToken` exists on both operation tables and `GenerationOperationToken` exists on `ProcessedImages`.

**Gate C:** database operator confirms backup, migration rows, tables, indexes, and no unexpected data change.

### Database rollback policy

Prefer application rollback while retaining these additive tables/column. Do **not** run EF `Down` after new traffic: it would delete idempotency history and may delete production operation records. Restore the backup only if the migration itself damaged production and traffic has been stopped.

## Phase 2 — Azure Storage dual-key rotation

Perform independently before the Stripe/application cutover where possible.

1. Determine which key slot the API currently uses without printing either key. Compare secure hashes in operator memory/process only.
2. Regenerate the **inactive** key slot. If the active slot cannot be determined safely, stop.
3. Build a new connection string in a protected process using the regenerated inactive key. Never echo it, save it in shell history, or attach it to CI output.
4. Update the protected deployment secret `AZURE_STORAGE_CONNECTION_STRING` and the API Container App secret `storage-connection-string`.
5. Create/restart a revision so all three compatibility variables continue to reference that secret:
   - `AzureStorage__ConnectionString`
   - `ConnectionStrings__AzureStorage`
   - `AZURE_STORAGE_CONNECTION_STRING`
6. Verify before invalidating the old key:
   - `/api/health/storage` reports Azure Blob Storage and connectivity;
   - an authorized test upload succeeds;
   - the uploaded blob is retrievable through the API proxy;
   - an existing production image still reads;
   - export ZIP creation/download succeeds;
   - logs contain no authorization/signature/storage fallback errors.
7. Regenerate the formerly active, compromised key.
8. Repeat all storage checks. Confirm private-container policy remains enforced.
9. Delete local temporary variables and secure artifacts containing either key.

Rollback before step 7: restore the previous secret and restart/redeploy the prior configuration.

Forward-fix after step 7: repair the new-key secret; the invalidated compromised key must never be restored.

**Gate D:** Storage operator and product verifier confirm both pre- and post-invalidation checks.

## Phase 3 — image publication and deployment preparation

1. Build and publish immutable images from the frozen commit only after Gate A.
2. Record digests; reject mutable-tag drift.
3. Verify production secret references, without revealing values:
   - SQL connection
   - JWT
   - Storage connection
   - Stripe secret/publishable/webhook secrets
   - Google OAuth
   - Turnstile
   - OpenAI/Replicate settings as applicable
4. Confirm automatic production migration is disabled in the app. If Phase 1 applied SQL manually, dispatch deployment with `run_db_migrations=false`.
5. Confirm prior healthy API/frontend revision names and image digests for rollback.
6. Because Bicep currently sets `activeRevisionsMode: 'Single'`, do not claim a zero-traffic canary. Changing to multiple revisions requires a separately reviewed infrastructure change.

## Phase 4 — coordinated Stripe Basil → Clover cutover

The application and live endpoint are incompatible until both sides use Clover. Do not change either side casually. Obtain Gates E and F before beginning this phase; approval must precede both endpoint mutation and revision activation.

### Cutover

1. Temporarily stop new checkout/PaymentIntent creation using an approved maintenance control. Verify a customer cannot start a new payment while normal non-payment browsing remains available.
2. Allow already-started PaymentIntents to settle where practical. Record, without PII:
   - pending local transactions;
   - recent succeeded PaymentIntents lacking a completed purchase;
   - Stripe endpoint delivery failures/pending retries.
3. Confirm the release application expects `2025-10-29.clover` and the configured signing secret still matches the existing live endpoint.
4. Change the existing live endpoint from `2025-08-27.basil` to `2025-10-29.clover`. Do not create a second live endpoint with an unconfigured signing secret.
5. Immediately deploy the frozen API/frontend images. The mismatch window is expected to return failures that Stripe can retry; checkout must remain paused.
6. Verify the new API revision is healthy and is the active single revision.
7. Send one Stripe Dashboard endpoint test event, if supported and separately approved, without creating a charge. Confirm signature/version acceptance.
8. Inspect failed deliveries created during the cutover window and replay them after the Clover revision is healthy.
9. Reconcile each succeeded PaymentIntent in the window to exactly one local transaction and at most one completed purchase/entitlement.

### Stripe/accounting smoke

Run these purchase scenarios against local/staging Stripe test mode before cutover. Do not point test-mode events at the live-secret production handler. In production, use only a separately approved no-charge endpoint test and read-only reconciliation.

1. Successful Starter or Pro checkout.
2. Signed `payment_intent.succeeded` webhook.
3. Exact credit award and one entitlement.
4. Same-event replay: no second award.
5. Concurrent replay: one completed webhook operation and one fulfillment.
6. Discounted purchase: one coupon redemption tied to the payment transaction.
7. Decline and 3DS paths.
8. Temporary Stripe verification or coupon-persistence failure: webhook returns retryable failure, then completes once after replay.

Latest local evidence used a real Stripe test-mode discounted PaymentIntent/event payload with 16 concurrent, valid locally HMAC-signed deliveries. It produced one operation (attempt count one), one coupon redemption, one purchase, one entitlement, and one 150-credit award; sequential replay returned 200 without duplication. The initial Stripe CLI listener check remains separately labeled as Stripe-forwarded signing evidence.

**Gate E:** Stripe operator approves endpoint mutation and confirms delivery/replay state.

**Gate F:** rollback commander authorizes production image activation.

## Phase 5 — application and product smoke

Run immediately after deployment:

```bash
./scripts/prod-smoke.sh
./scripts/validate-deployment.sh
./scripts/verify-container-revision.sh
```

Manual checks:

1. API liveness/readiness and frontend root/pricing routes.
2. Existing account login and session persistence.
3. New test account registration/email-confirmation procedure if approved.
4. Source upload and score.
5. Deterministic/non-paid smoke where production configuration permits; do not trigger paid AI without separate approval.
6. Free preview, package visibility, paid candidate restoration, refinement, premium augmentation, and export entitlements.
7. Existing and newly written blobs through the API proxy.
8. Payment-return redirect/query preservation.
9. Gallery and resumable workspace after refresh.
10. No new browser accessibility/contrast regression on desktop/mobile.

## Phase 6 — monitoring and reconciliation

Observe for at least the agreed window before resuming checkout. Compare rates with the captured baseline.

### Logs and platform signals

Monitor:

- API/frontend revision health, restarts, CPU/memory, latency, and HTTP 5xx/409 rates;
- migration/startup failures;
- `GenerationInProgress` volume and provider-call/credit-refund anomalies;
- failed or reconciliation-due `HeadshotGenerationOperations`;
- failed/reconciliation-due `StripeWebhookOperations` and attempt counts;
- Stripe signature/API-version errors and delivery retries;
- `PendingReview` transactions;
- Storage authorization, blob-not-found, proxy, and export errors;
- auth/Turnstile/OAuth callback failures.

### Stale-operation safety rule

`LeaseExpiresAt` is an observability/reconciliation deadline, **not** permission for another replica to reclaim a processing row. The application deliberately rejects duplicates while status remains `Processing`, even after that timestamp. This fail-stop behavior prevents a crashed request from repeating provider work, charging twice, or overlapping payment fulfillment.

Before any status/token mutation:

1. Pause the affected workflow (and checkout for webhook receipts).
2. Drain or terminate every revision/replica that could own the recorded token; verify no worker remains in flight.
3. Preserve the operation row, token, related logs, Stripe event/PaymentIntent, payment transaction, coupon redemption, purchase, entitlement, credit ledger, and token-matched candidate rows.
4. For generation, reconcile charge/refund and token-matched candidates. Mark `Succeeded` only when the complete candidate/accounting result is durable. Otherwise refund exactly the token-specific charge if needed, mark only token-matched candidates failed, then mark the operation `Failed` before allowing a deliberate retry. If provider outcome is unknown, do not retry until an operator accepts the potential external duplicate.
5. For Stripe, mark `Succeeded` when coupon/purchase/credit/entitlement fulfillment is already complete. If incomplete, repair any inconsistent local records transactionally, then mark the receipt `Failed` and replay the preserved Stripe event. Existing coupon and purchase keys make a partial prior commit idempotent.
6. Record approver, before/after row snapshots, reason, and reconciliation result. Never overwrite an operation token or set `Failed` while its prior worker may still run.

OpenAI timeouts, network loss, malformed responses, and other failures without proof of rejection are ambiguous outcomes, not retry permission. The application retains the processing claim and charge for reconciliation, including when provider output could not be persisted. Explicit authentication rejection remains a definitive failure. Investigate retained charges promptly; never refund or resubmit automatically merely because the customer received an error.

A debit may also persist before its consumption receipt or usage-log entry reaches generation. Any exception during debit, or missing receipt, retains `Processing`; absence of a charge usage log is not proof that no debit occurred. After draining workers, reconcile database/audit history and all intervening account transactions before deciding whether a debit occurred. Restore a verified debit exactly once with an operator-recorded transaction; do not rely on the automatic refund helper when its charge log is missing. If the amount/outcome cannot be established, keep the operation blocked and escalate rather than guessing or replaying.

Account balances use EF optimistic concurrency: stale tracked credit writes must fail rather than replace a newer balance. Distinct purchases roll back on balance conflicts and can fulfill on fresh-context webhook replay; do not manually award them first. Generation debit conflicts currently retain the processing claim conservatively and follow the debit reconciliation procedure above. Monitor concurrency errors and reconciliation age during rollout. An old application without this mapping cannot safely share write traffic with the new version; drain old workers before enabling the new workflow. App rollback restores the old version's accounting race, so keep checkout/generation paused until the rollback commander approves mitigation.

Package allowance updates also compare original candidate/use/refinement/premium/export balances, status, and expiry. Competing consumption returns false rather than silently succeeding; the losing tracked entity is detached. Do not override this rejection or blindly grant allowances after an error. Reconcile generation candidates/refunds and remaining entitlement state together. Drain old writers because they lack both balance and allowance concurrency mappings.

Shared coupon capacity is also concurrency-checked against original usage, limit, activation, and expiry. A conflicting redemption rolls back and clears tracked state before webhook retry. Replay of the winning payment remains idempotent; another payment must respect the exhausted coupon and may require payment review. Do not bypass coupon limits or blindly redeem again. Drain old writers that lack this mapping too.

No automated job may reclaim these rows.

### Read-only reconciliation queries

```sql
-- Stale or failed generation operations
SELECT Status, COUNT(*) AS OperationCount
FROM dbo.HeadshotGenerationOperations
WHERE Status = 2 OR (Status = 0 AND LeaseExpiresAt < SYSUTCDATETIME())
GROUP BY Status;

-- Stale, failed, or repeatedly attempted Stripe operations
SELECT Status, AttemptCount, COUNT(*) AS OperationCount
FROM dbo.StripeWebhookOperations
WHERE Status = 2
   OR AttemptCount > 1
   OR (Status = 0 AND LeaseExpiresAt < SYSUTCDATETIME())
GROUP BY Status, AttemptCount;

-- Paid transactions requiring reconciliation
SELECT t.Id, t.Status, t.UpdatedAt
FROM dbo.PaymentTransactions t
LEFT JOIN dbo.CreditPurchases p
  ON p.PaymentTransactionId = CONVERT(nvarchar(50), t.Id)
WHERE t.Status = 5
   OR (t.Status = 1 AND p.Id IS NULL);

-- Duplicate fulfillment must return no rows
SELECT PaymentTransactionId, COUNT(*) AS PurchaseCount
FROM dbo.CreditPurchases
WHERE PaymentTransactionId IS NOT NULL
GROUP BY PaymentTransactionId
HAVING COUNT(*) > 1;

SELECT SourcePaymentTransactionId, COUNT(*) AS EntitlementCount
FROM dbo.UserPackageEntitlements
WHERE SourcePaymentTransactionId IS NOT NULL
GROUP BY SourcePaymentTransactionId
HAVING COUNT(*) > 1;
```

Treat unexpected duplicates, unbounded failed-operation growth, unexplained credit deltas, or Storage authorization failures as rollback/checkout-pause triggers.

**Gate G:** product, database, Stripe, and rollback owners sign reconciliation; only then resume checkout and change NO-GO to GO.

## Rollback and forward-fix matrix

| Failure | Immediate action | Recovery |
|---|---|---|
| Migration failure before app deploy | Keep old revision serving; checkout may remain unchanged | investigate/restore backup only if schema/data damaged |
| Storage fails before old key invalidation | restore old secret and revision | correct regenerated key/connection string |
| Storage fails after compromised key invalidation | keep checkout/workspace writes paused | forward-fix new key secret; never restore compromised key |
| New revision unhealthy before Stripe cutover | keep Basil endpoint and old revision | fix/rebuild; no Stripe reconciliation needed |
| New revision unhealthy after Clover cutover | pause checkout; assess fast forward-fix | if rolling back app, restore endpoint to Basil before/with old revision and replay/reconcile window events |
| Webhook version/signature failures | keep checkout paused | correct endpoint version/secret, then replay failed deliveries |
| Duplicate/lost fulfillment signal | stop checkout and automated replay | preserve DB/event evidence; reconcile before any manual award |
| Non-payment UI regression | rollback frontend revision if API remains compatible | retain additive DB migration and rotated Storage keys |

After any successful new-version payment, prefer forward-fix over database rollback. Never manually award credits until PaymentIntent, local transaction, purchase, entitlement, coupon redemption, and webhook operation have been reconciled together.

## Final execution checklist

Production execution may begin only when every item is checked:

- [ ] Gate A artifact approval
- [ ] Gate B staging/direct-canary decision
- [ ] Gate C backup and migration approval
- [ ] Gate D Storage rotation approval
- [ ] Gate E Stripe endpoint approval
- [ ] Gate F deployment approval
- [ ] Checkout maintenance control tested
- [ ] Prior revisions/digests recorded
- [ ] Named rollback commander online
- [ ] Smoke and reconciliation operators online
- [ ] Gate G observation close before checkout resumes

Until then: **NO-GO; stop for explicit production authorization.**
