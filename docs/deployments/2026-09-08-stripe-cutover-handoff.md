# Stripe cutover handoff

Date: 2026-09-08
Branch: `feature/photo-workspace-design-audit`
Repository: `alanw707/AI.ProfilePhotoMaker`

## Objective

Resume release work in a new context. Align the production Stripe webhook API version with the release, then deploy the frozen feature commit only after release gates pass.

## Frozen artifact

- Commit: `060d00aeed7861df007bfc6d2470ecc7f3e6d50a`
- Message: `fix outcome package allowances and remove credit balance UI`
- Branch is 11 commits ahead of local `main`.
- Feature branch is not pushed; it has no GitHub Actions deployment runs.
- Local commit excludes `.pi/**` and `docs/testing/evidence/**` artifacts.
- Uncommitted local state is limited to `.pi` state, `.aipm-verify-compose.yml`, and generated evidence. Do not include these in the release.

## Verification recorded

- Targeted backend tests: 49 passed.
- Backend excluding performance tests: 409 passed.
- Frontend tests: 465 passed.
- Frontend build and ESLint: passed; ESLint has warnings only.
- Full backend suite: 1 performance-gate failure; measured 85.0% against a strict `>85%` threshold. Rerun from the frozen commit before release.

## Current production state

- Resource group: `aiprofilemaker-v1`
- API app: `aipm-api-v1`
- Web app: `aipm-web-v1`
- API revision after storage rotation: `aipm-api-v1--storagerelay1121359`, 100% traffic, Running/Healthy.
- Web revision remains `aipm-web-v1--0000379`.
- API health: `/api/health` = 200; `/api/health/storage` = 200.
- Storage account remains HTTPS-only and private blob access is disabled.
- API storage variables reference `storage-connection-string`; values are not inline.
- No feature image or feature revision has been deployed.

## Storage rotation already completed

Using Azure CLI, without printing credentials:

1. Renewed standby `key2`.
2. Switched the API secret to renewed `key2`; created a healthy revision.
3. Renewed formerly active `key1`.
4. Switched the API secret to renewed `key1`; created a healthy revision.
5. Renewed `key2` again, invalidating its temporary value.
6. Updated GitHub Actions secrets `AZURE_STORAGE_CONNECTION_STRING` and `AZURE_STORAGE_CONTAINER_NAME`.

The active Container App secret matches regenerated `key1`; both storage key slots are distinct. Storage health passed after each switch. Authorized upload/retrieval/existing-image/export smoke has not been repeated, so Storage Gate D is not fully signed off.

## Why Stripe cutover is required

- Live Stripe webhook endpoint currently uses API version `2025-08-27.basil`.
- Release uses Stripe.NET `49.1.0`, expecting `2025-10-29.clover`.
- Stripe API versions can change PaymentIntent and webhook payload fields. Deploying the release while the endpoint remains on the older version risks webhook parsing, fulfillment, entitlement, and reconciliation failures.

Cutover is an operational sequence, not a secret rotation:

1. Pause checkout and record the endpoint ID/current version securely.
2. Confirm live/test key modes and prefixes without printing values.
3. Coordinate the endpoint version change to Clover with the compatible application revision.
4. Deploy the frozen revision and verify signed test-mode checkout/webhook behavior.
5. Replay queued/failed events and reconcile PaymentIntent, local transaction, purchase, entitlement, coupon, and webhook-operation records.
6. Resume checkout only after reconciliation and rollback observation pass.

Do not change the live endpoint independently while the Basil application is serving traffic.

## Remaining release gates

The rollout plan says **NO-GO** until every gate passes:

- Gate A: immutable artifact approval and CI green.
- Gate B: staging rehearsal or explicitly approved direct-production exception.
- Gate C: database backup, migration, and verification approval.
- Gate D: complete post-rotation storage smoke checks.
- Gate E: Stripe checkout pause and Basil → Clover cutover approval.
- Gate F: production deployment approval.
- Gate G: product/database/Stripe/rollback-owner reconciliation sign-off.

Required named roles: release operator, database operator, Azure/Storage operator, Stripe operator, product verifier, and rollback commander.

## Important tooling note

`scripts/rotate-storage-keys.sh` is unsafe for the current secret-reference setup: its active-key detection reads the Container App environment `value` field (empty when `secretRef` is used), and its update function writes the connection string as inline environment values. It was not used. Future rotation automation must read `az containerapp secret show` in a protected process, update `storage-connection-string`, and preserve all three `secretref` bindings.

## Next action

Have the Stripe operator approve and execute the coordinated Basil → Clover maintenance sequence, then rerun the release gates from commit `060d00a`. Do not push or invoke `scripts/deploy-branch-via-actions.sh` before Gate A–F approval; that workflow deploys directly to production and can apply production EF migrations.
