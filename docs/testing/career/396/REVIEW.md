# #396 Release readiness — review

Branch `career/396-release-readiness` off `feature/career-workspace` (b4e306ef). No merge to main, no deploy, no production change.

## Regression, flag off and on
| Suite | Flag off | Flag on |
|---|---|---|
| API `dotnet test --filter "Category!=Performance"` | 1302 passed, 1 skipped, 0 failed | 1302 passed, 1 skipped, 0 failed |
| Karma | 723 SUCCESS (separate run, LocalDev API flag off) | 723 SUCCESS (separate run, flag on) |
| Playwright full (chromium), real LocalDev API | 307 passed, 0 failed, 1 skipped | 307 passed, 0 failed, 1 skipped |

Fixes made here:
- Five career e2e specs still checked the old career home; they now check the journey home and allowance (44/44).
- `CareerWorkspaceFlagOffTests` now uses an explicit `CareerWorkspaceDisabledFactory`. Before, `Features__CareerWorkspace=true` made 78 of its tests fail.
- The untagged Performance-namespace load tests now carry `Category=Performance`, so both test filters run the same 1303 tests.

### Pre-existing Playwright failures (not caused by career work)
17 of the 18 fail the same way on the pre-career merge base 340273b7. They cover SEO smoke against a non-SSR dev server, marketing selfie copy, model status and auto-repair connectivity. The 18th, `auto-repair-functionality:50`, passes when run alone on both trees, so it is flaky under the full run.
- `tests/auto-repair-functionality.spec.ts:50` should keep auth service accessible via debug context 
- `tests/auto-repair-simplified.spec.ts:157` should validate API connectivity for auto-repair operations 
- `tests/marketing-selfie-count-copy.spec.ts:13` ai-headshot-generator uses at least 5 copy 
- `tests/marketing-selfie-count-copy.spec.ts:4` how-it-works uses at least 5 copy 
- `tests/model-status-display.spec.ts:19` renders "Ready for training" when unified status returns ReadyForTraining 
- `tests/seo-metadata-smoke.spec.ts:101` sitemap.xml lists SEO routes 
- `tests/seo-metadata-smoke.spec.ts:21` has SEO metadata for founder-press-kit-photo-pack 
- `tests/seo-metadata-smoke.spec.ts:21` has SEO metadata for linkedin-executive-profile-photo 
- `tests/seo-metadata-smoke.spec.ts:21` has SEO metadata for pricing 
- `tests/seo-metadata-smoke.spec.ts:21` has SEO metadata for realtor-profile-photo-pack 
- `tests/seo-metadata-smoke.spec.ts:62` server-rendered HTML includes SEO tags for features 
- `tests/seo-metadata-smoke.spec.ts:62` server-rendered HTML includes SEO tags for founder-press-kit-photo-pack 
- `tests/seo-metadata-smoke.spec.ts:62` server-rendered HTML includes SEO tags for how-it-works 
- `tests/seo-metadata-smoke.spec.ts:62` server-rendered HTML includes SEO tags for linkedin-executive-profile-photo 
- `tests/seo-metadata-smoke.spec.ts:62` server-rendered HTML includes SEO tags for nurse-headshots 
- `tests/seo-metadata-smoke.spec.ts:62` server-rendered HTML includes SEO tags for pricing 
- `tests/seo-metadata-smoke.spec.ts:62` server-rendered HTML includes SEO tags for realtor-profile-photo-pack 
- `tests/seo-metadata-smoke.spec.ts:62` server-rendered HTML includes SEO tags for teacher-headshots 

## Rollout / rollback checklist (nonproduction rehearsal first)
1. **Baseline:** record the intended main SHA, the CI build artifact digest and the deployed production SHA/digest. Parity is **unresolved** until CI evidence exists.
2. **Migrations:** every career migration is additive (drops only in `Down`). Apply to staging SQL Server and check `has-pending-model-changes` reports none.
3. **Config:** `Features:CareerWorkspace=false` by default. Production keys stay unset until approved: OpenAI, `USAJobs:ApiKey`/`Email`. Set the `Career:Usage` policy values.
4. **Staff rollout:** enable the flag in staging, then production for staff only. Watch error rate, `CareerUsageEvent` cost against the cap, run failure rate and 503 `CareerBusy`/`CostCapReached` counts.
5. **Invited beta:** widen after 7 days without a threshold breach.
6. **Rollback:** (a) switch on the admin kill switch `generationDisabled` to stop new work immediately; (b) set `Features:CareerWorkspace=false`. Career routes return 403, the homepage is unchanged and photo delivery and checkout are unaffected. Data is preserved and tombstone replay still runs. Restoring the flag resumes work, and runs recover through the reaper and checkpoints.
7. **Monitoring thresholds (proposed):** 5xx > 1% over 15 min, cost > 80% of the monthly cap, or run failure > 10% triggers the kill switch. Rollback owner: repo owner.
8. **GO/NO-GO:** owner decision, recorded on #396. Currently **NO-GO** until the human gates below close.

## Human gates (open, owner)
- [x] Real SQL Server concurrency proof (2026-10-06, SQL Server 2022 container `mcr.microsoft.com/mssql/server:2022-latest`):
  - `dotnet ef database update` applied every migration cleanly, through `20261005233749_CareerGlobalUsageGuard`.
  - `CareerConcurrentCreateTests` (6 tests) run against SQL Server when `CAREER_SQLSERVER_TESTS` holds a connection string: parallel creates within the allowance, the per-user concurrency limit, the global queue cap across users, the user and global cost caps, and a new same-Idempotency-Key race (one run, one reservation). Passed 6/6 in 3 consecutive runs. A wrong-password run failed with `Login failed for user 'sa'`, which shows the SQL Server path is really used. Each test uses its own database and drops it afterwards.
  - Command: `CAREER_SQLSERVER_TESTS='Server=localhost,14333;User Id=sa;Password=…;TrustServerCertificate=True' dotnet test --filter FullyQualifiedName~CareerConcurrentCreateTests`
- [ ] #377 independent target-user observation (owner walkthroughs were coached).
- [ ] Production keys and provider rights: OpenAI, USAJOBS, licensed data.
- [ ] Owner approval of the usage budget and quotas (#395, currently provisional).
- [ ] Confirm the hosting backup expiry (stated as up to 35 days).
- [ ] CI/deploy parity evidence: main SHA, artifact digest, production digest.
- [x] Pre-existing SEO/marketing Playwright failures made green (owner chose option 1).

## Findings
| id | sev | finding | status |
|---|---|---|---|
| R1 | P2 | Five career e2e specs were stale after #393/#395 | fixed |
| R2 | P2 | Flag-off API tests depended on the environment default | fixed: explicit factory |
| R3 | P3 | Untagged Performance load tests | fixed: tagged |
| R4 | P2 | 15 stale photo/SEO Playwright tests and 4 flaky debug-context/axe tests | fixed in career/396-playwright-green (see below) |

Open P0/P1: **none**

## Playwright green-up (owner decision: fix, not quarantine)
- SEO static HTML regenerated with `npm run generate:seo-static`; sitemap lists nurse/teacher and the 3 use-case pack pages; the 3 pack pages get canonical routes (the `/use-cases/...` paths stay as aliases).
- `/pricing` is the packages page, not an SEO component page, so the live-DOM SEO check skips it; its prerendered crawler HTML is still checked.
- Marketing copy test now asserts the current one-clear-photo copy and that the old "at least 5" wording is gone.
- `model-status-display` became `photo-workspace-bootstrap`: the training dashboard it checked was replaced by the package photo workspace.
- Flaky tests: specs reading `__APP_DEBUG__` wait for it to register; the market-comparison axe test emulates reduced motion so it measures settled colours. 165/165 and 20/20 across 5 repeats.
