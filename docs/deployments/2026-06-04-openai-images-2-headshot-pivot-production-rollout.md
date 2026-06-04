# Production rollout plan — OpenAI images 2 headshot pivot / outcome packages

Date: 2026-06-04
Branch: `feature/openai-images-2-headshot-pivot`
Primary migration: `20260518040142_AddOutcomePackages`
Generated migration SQL: `docs/deployments/2026-06-04-outcome-packages-migration.sql`

## Goal

Deploy the headshot/profile workflow pivot safely to production with database migration first, then application code, then production verification.

## Current change profile

- Large cross-cutting change: API controllers/services/DTOs, EF model/migration, Angular workflow/gallery/credit UI, tests, docs.
- GitHub deploy workflow supports `workflow_dispatch` input `run_db_migrations` and runs `dotnet ef database update` when enabled.
- Local deploy workflow comments expect image build/push before infrastructure/app deploy:
  - `IMAGE_TAG=<tag> ./scripts/build-local.sh`
  - `IMAGE_TAG=<tag> ./scripts/push-to-acr.sh`

## Migration-first rollout strategy

### Phase 0 — freeze and hygiene

1. Verify working tree.
   ```bash
   git status --short --branch
   git diff --stat
   ```
2. Exclude pi agent state from commit unless intentionally needed:
   - `.pi/goals/**`
   - `.pi/goals/goal_events.jsonl`
3. Confirm no unrelated product changes are bundled.
4. Create release tag/image tag variable:
   ```bash
   export IMAGE_TAG="$(date -u +%Y%m%d%H%M)-openai-images-2-pivot"
   ```

### Phase 1 — database migration review

Generated SQL:

```bash
export ConnectionStrings__DefaultConnection='Server=localhost;Database=DesignTimeOnly;User Id=sa;Password=DesignTimeOnly123!;TrustServerCertificate=true;MultipleActiveResultSets=true;'
dotnet ef migrations script \
  20260515001531_AddHeadshotGenerationMetadata \
  20260518040142_AddOutcomePackages \
  --project AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj \
  --startup-project AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj \
  --output docs/deployments/2026-06-04-outcome-packages-migration.sql \
  --idempotent
```

Review findings from generated SQL:

- Creates `OutcomePackageDefinitions`.
- Creates `UserPackageEntitlements`.
- Inserts three seed outcome package definitions.
- Creates indexes:
  - unique `OutcomePackageDefinitions.Code`
  - `OutcomePackageDefinitions.InternalCreditPackageId`
  - `OutcomePackageDefinitions.IsActive, DisplayOrder`
  - `UserPackageEntitlements.OutcomePackageDefinitionId`
  - filtered unique `UserPackageEntitlements.SourcePaymentTransactionId` where not null
  - `UserPackageEntitlements.UserId, Status, CreatedAt`
- Records migration in `__EFMigrationsHistory`.
- No existing table drop detected in the forward SQL.
- No existing column drop detected in the forward SQL.
- Main compatibility risk: new code expects these tables/seeds; deploy DB before code.
- Existing production code should ignore additive tables, so DB-first deploy is expected to be safe.

Required pre-prod DB actions:

1. Backup production database.
2. Confirm current production migration is at or after `20260515001531_AddHeadshotGenerationMetadata`.
3. Apply idempotent SQL or run workflow migration step.
4. Verify tables and seed rows:
   ```sql
   SELECT COUNT(*) FROM dbo.OutcomePackageDefinitions;
   SELECT Code, Price, IsActive, DisplayOrder FROM dbo.OutcomePackageDefinitions ORDER BY DisplayOrder;
   SELECT COUNT(*) FROM dbo.UserPackageEntitlements;
   SELECT MigrationId FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE '20260518040142%';
   ```

Rollback notes:

- Preferred rollback for app failure: roll back application image/revision, keep additive DB migration.
- DB rollback only if migration itself breaks production.
- Manual DB rollback would drop `UserPackageEntitlements`, then `OutcomePackageDefinitions`, and remove the migration history row. This destroys any new entitlement records created after rollout; backup restore is safer.

### Phase 2 — local verification gates

Run before deploying app code:

```bash
dotnet restore AI.ProfilePhotoMaker.sln
dotnet build AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj --configuration Release
dotnet test AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj --configuration Release --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Performance"

cd AI.ProfilePhotoMaker.UI
npm ci
npm run lint:errors-only
npm run test -- --watch=false --browsers=ChromeHeadless
npm run build:mvp-v1
```

Targeted optional smoke/e2e:

```bash
cd AI.ProfilePhotoMaker.UI
npx playwright test ../AI.ProfilePhotoMaker.API/tests/playwright/tests/instant-headshot-mocked-flow.spec.ts
npx playwright test ../AI.ProfilePhotoMaker.API/tests/playwright/tests/profile-workflow-flags-and-download.spec.ts
```

Gate rule: do not deploy app code if backend build/tests or frontend lint/build fail. If flaky UI unit tests fail, capture exact failure and decide explicitly before deploy.

### Phase 3 — app deployment

1. Commit reviewed changes on feature branch.
2. Push branch and open PR.
3. Confirm CI green.
4. Merge to `main` only after DB migration applied or when using workflow with migrations enabled.
5. Build and push images with the chosen tag if using local image build path:
   ```bash
   IMAGE_TAG="$IMAGE_TAG" ./scripts/build-local.sh
   IMAGE_TAG="$IMAGE_TAG" ./scripts/push-to-acr.sh
   ```
6. Trigger `.github/workflows/simple-deploy.yml` on `main`:
   - `skip_tests=false`
   - `run_db_migrations=true` if migration not already applied
   - `run_db_migrations=false` if migration was manually applied and verified

Feature flags to verify in production app configuration:

- `Features__OpenAIHeadshotMvp=true`
- `Features__ProfilePhotoWorkflowOverhaul=true`
- `Features__OutcomePackagesVisible=true`
- `Features__ProfilePhotoScoreVisible=true`
- `Features__CreativeStylePackVisible=true`
- `Features__PremiumAugmentationsVisible=true`

If risk needs staged exposure, deploy code with visible flags disabled first, verify health, then enable flags one at a time.

### Phase 4 — production smoke verification

Run after deployment:

```bash
./scripts/prod-smoke.sh
./scripts/validate-deployment.sh
./scripts/verify-container-revision.sh
```

Manual/product smoke checklist:

1. Backend health endpoint responds healthy.
2. Frontend loads from production URL.
3. Login/auth session works.
4. Verify-email route still works.
5. Credit/outcome packages render expected free/starter/pro choices.
6. Free preview/profile score path works.
7. Instant headshot generation request starts and returns expected status/result.
8. Gallery loads generated images.
9. Download/export path works.
10. Stripe package purchase path is not regressed, or remains hidden if not enabled.
11. App logs show no migration, DI, EF, OpenAI, Stripe, storage, or auth exceptions.

Production SQL verification after app traffic:

```sql
SELECT COUNT(*) AS OutcomePackageCount FROM dbo.OutcomePackageDefinitions;
SELECT TOP 20 Code, Name, Price, IsActive FROM dbo.OutcomePackageDefinitions ORDER BY DisplayOrder;
SELECT TOP 20 Status, COUNT(*) AS CountByStatus FROM dbo.UserPackageEntitlements GROUP BY Status ORDER BY Status;
```

### Phase 5 — rollback plan

App rollback:

1. Roll back Azure Container App to previous healthy revision.
2. Disable new feature flags if config is separate from revision.
3. Re-run health and smoke.
4. Keep additive DB migration unless it is the root cause.

DB rollback emergency only:

1. Stop app traffic or disable feature flags.
2. Restore backup, or manually drop new tables if no production entitlement data must be preserved.
3. Redeploy previous app revision.
4. Verify migration history matches restored schema.

## Go / no-go checklist

Go only when all are true:

- Migration SQL reviewed and stored.
- Production DB backup complete.
- Forward migration applied or deploy workflow configured to apply it.
- Local backend build/tests pass.
- Local frontend lint/test/build pass or explicit approved exception documented.
- Feature flags known and reversible.
- Rollback owner/steps clear.
- Production smoke checklist ready.

No-go if any are true:

- Migration SQL includes destructive change not reviewed.
- Production DB state/migration baseline unknown.
- Build or critical test fails.
- Required secrets/config missing.
- No one available to verify production immediately after deploy.
