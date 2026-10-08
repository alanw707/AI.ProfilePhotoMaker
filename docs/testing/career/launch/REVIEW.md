# Career workspace launch (#396), 2026-10-07

## Decision: GO (owner, 2026-10-07)

- #377 independent observation: **waived** for this launch by the owner, because there are no customers yet.
- Model: `gpt-5-mini-2025-08-07`, reasoning effort `minimal`, priced at $0.25 / $2.00 per 1M input/output tokens. Uses the existing production `OpenAI__ApiKey` (Key Vault `OpenAiApiKey`). The live adapter test passed locally.
- Data: only the bundled BLS/O*NET snapshots. Production has no USAJOBS key, so Open postings shows the honest "unavailable" state (`NoJobObservationSource`).
- Approved caps (policy `2026-10-launch`): 20 drafts per user per month, $1 per user per month, **$2 global per UTC day** (new `Career:Usage:DailyModelCostCapUsd`), $25 global per month.

## Rollout

| Stage | PR / SHA | Setting | Simple Deploy |
|---|---|---|---|
| 1: owner allowlist | #433 / `b45d1951` | `CAREER_WORKSPACE_ENABLED=true`, `AUDIENCE=Allowlist`, emails from repo variable `CAREER_WORKSPACE_ALLOWED_EMAILS` | run 37710399413 success |
| 2: everyone | #434 / `1a6b377c` | `CAREER_WORKSPACE_AUDIENCE=Everyone` | run 37714093962 success |

## Live verification

- Stage 1, signed in as the owner: `/api/config/career-access` returned `{enabled:true}` while the public config stayed `careerWorkspace:false`. The Career link showed at 1280px. Profile saved (01), goal saved (02), and a real-model `profile_summary` run went queued → working → completed in about 4s (runs `b1e92d6a…`, `9890fefe…`; screenshots 03). The allowance dropped from 20 to 19.
- Stage 2: the live API `/api/config/client` returns `careerWorkspace: true`. The Career link shows (04).
- `/app/enhance`, `/app/gallery`, `/pricing` and `/app/settings` load signed in after both stages, with no 5xx responses (`stage1-photo-*`, `stage2-photo-*`).

## Gates on the deployed SHA `1a6b377c`

| Gate | Command | Result |
|---|---|---|
| API | `dotnet test AI.ProfilePhotoMaker.API.Tests` | 1335 passed, 1 failed, 1 skipped. The failure was `PerformanceTestRunner.ExecuteFullPerformanceTestSuite`, a memory benchmark that ran while Karma and Playwright loaded the machine. It passed 1/1 when rerun alone (`--filter PerformanceTestRunner`). The skipped test is the live OpenAI test, which needs a key. |
| Karma | `ng test --watch=false --browsers=ChromeHeadless` | 741/741 |
| Build | `npm run build:mvp-v1` | success |
| Playwright | `npx playwright test --project=chromium` (LocalDev API on 5032) | 318 passed, 1 skipped, 0 failed |

## Monitoring thresholds

- Any 5xx on `/api/career/*` over 1% of requests in 15 minutes → investigate.
- `CareerDailyCostCapReached` on two days in a row, or monthly spend above $20 → review the caps.
- Run failure rate above 10% per day (status `failed`) → turn generation off with the admin kill switch (`PUT /api/admin/career/controls generationDisabled=true`).
- Any photo checkout or fulfillment error → roll back immediately.

## Rollback (owner: Alan)

1. Fastest option, no deploy: use the admin kill switch `generationDisabled=true`, which stops new runs only.
2. Full rollback: in `.github/workflows/simple-deploy.yml` set `CAREER_WORKSPACE_ENABLED: 'false'` (or set `CAREER_WORKSPACE_AUDIENCE: 'Allowlist'` to shrink access), then push to `main`. Simple Deploy redeploys. Career data is kept, and the photo routes are unaffected.
