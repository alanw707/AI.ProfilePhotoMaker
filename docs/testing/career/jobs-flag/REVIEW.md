# Jobs page flag (PR #450) — review

Reviewed 2026-10-11, after merge (commit 05ea1c8d). Two delegated read-only reviews (separate
model sessions, static reading, no tests run) plus the author's own checks. Both reviewers reported
no blockers and agreed on the four questions below.

## What the change does

`Career:Jobs:Enabled` (default `false`) → `/api/config/client` `features.careerJobs`.
Off: `GET /api/career/jobs/observations` and `/jobs/source` answer 404 `CareerJobsDisabled`; the UI
route `/app/career/jobs` redirects to `/app/career`; "Open postings" is left out of "More pages".
The USAJOBS source, settings and secrets are untouched.

## Questions checked

| Question | Result | Evidence |
|---|---|---|
| Does anything else reach the jobs source while the flag is off? | No | `IJobObservationService` is injected only in `CareerMarketController`; both jobs actions carry `[RequireCareerJobs]`. No agent tool, export or journey code references it. |
| Does anything else link to `/app/career/jobs`? | No | The only link was the "More pages" entry (`career-shell.component.ts`), now filtered by `careerMorePages`. |
| Can the route guard run before the runtime config has loaded? | No | `app.config.ts` loads `/config/client` in an `APP_INITIALIZER`, which Angular awaits before the first navigation. If that request fails, the build-time default (`careerJobs: false`) applies, so it fails closed. |
| Filter order | Correct | Authorization runs first (live: unauthenticated → 401), then the controller-level workspace filter (403 for accounts outside the audience), then the action-level jobs filter (404). The source is not called (`Queries` stays empty in `CareerJobsFlagTests`). |
| Is the flag read per request? | Yes | `CareerJobsFeature.IsEnabled` reads `IConfiguration` each call, like `CareerFeatureGate`. |

## Findings

1. **Fixed in this follow-up — no deploy setting for the flag.** PR #450 relied on the built-in default.
   `simple-deploy.yml` now has `CAREER_JOBS_ENABLED: 'false'`, passed as `careerSettings.jobsEnabled`
   to `Career__Jobs__Enabled` in `simple-deploy.bicep`. Turning Jobs on is a one-line change plus a deploy.
2. **Fixed in this follow-up — `PRODUCT.md` still listed job observations as a live journey step.**
3. **Open, low — a signed-in tab that was open before the deploy** keeps its old config until reload
   and could still show the link; the API answers 404 and the page shows its error state. Self-heals on reload.
4. **Open, low — hidden code still ships.** The Jobs component is lazy-loaded, so it costs nothing at
   runtime, but it and ~60 API tests are maintained for a page nobody can see. If Jobs is not brought
   back (with a private-sector source) by the next feature review, delete it rather than carry it.
5. **Open, low — 404 vs 403.** Other "switched off" career answers use 403 `Disabled`; Jobs uses 404 so
   the endpoint looks absent. This is deliberate and documented in `docs/career/api-job-observations.md`.

### Raised by the delegated reviewers (all low or info; none fixed yet)

6. **Test gap — filter order is not pinned.** `CareerJobsFlagTests` has no unauthenticated case
   (expected 401, not 404) and no workspace-off-with-jobs-on case (expected 403). A change in filter
   order would not be caught. Live, unauthenticated did return 401.
7. **The "unset" test is really "default false".** `CareerWorkspaceEnabledFactory` loads
   `appsettings.json`, which sets the key to `false`, so the `?? false` fallback is never exercised.
8. **Permissive flag parsing (suspected).** `GetValue<bool?>` throws on a value such as `"yes"`. A typo
   in the deploy value would break the jobs endpoints and the public `/api/config/client`, which the
   app loads at startup. Safer: `bool.TryParse`. Not reproduced.
9. **The deploy value is a literal** (`CAREER_JOBS_ENABLED: 'false'`), so switching Jobs on needs a
   commit, like the other `CAREER_*` settings. A repository variable would avoid that.
10. **Other callers of the bicep file were not searched.** A caller passing a `careerSettings` object
    without `jobsEnabled` would fail. The deploy workflow passes it.
11. **`careerMorePages` is computed once** per shell instance, and the controller still constructs the
    jobs service for unrelated market endpoints. Both harmless today.

## Gates at merge

API 1350 passed / 0 failed / 1 skipped; Karma 746 passed; Playwright 381 passed / 1 skipped;
`build:mvp-v1` succeeded; CI (CodeQL, static analysis) passed; Simple Deploy succeeded.
Live: `/app/career/jobs` → `/app/career`, no Jobs link, signed-in API → 404 `CareerJobsDisabled`.
