# #386 job observations: live-stack review

Simulated review on the LocalDev stack (in-memory DB, career flag on, **no USAJOBS key configured** — the objective's explicit no-key path), not target-user evidence. No API mocks in the live script. Synthetic, fictional profiles. Script: `AI.ProfilePhotoMaker.UI/tests/ux/career-jobs-review.mjs`; raw results in `checks.json`; screenshots at desktop 1280, mobile 390 and 320.

## What the live stack verifies (no key)

| Check | Result |
|---|---|
| `GET /api/career/jobs/source` | 200, `configured: false`, name USAJOBS, coverage "U.S. federal agencies only; not the private-sector market.", attribution present, source URL present |
| `GET /api/career/jobs/observations` | 200 with `coverage.available: false`, reason `source_not_configured`, an empty list and the counts block — **an honest state, not an error** |
| Area resolution from the goal | "Denver, CO" → metro 19740 (Denver-Aurora-Centennial, CO) without any feed call |
| Page | explains that the source is not configured, names the source, shows the attribution ("displayed values are unchanged") and two links to the benchmark pages, and states that postings are not employment totals, an outlook or a vacancy count |
| Benchmark pages while the feed is down | market brief, pay analysis and market comparison all still render with no error |
| Filters | area, eligible-only, remote and search persist in query params and survive a reload (area "Denver, CO", remote "unknown", search "software") |
| Expired session | `/auth/login?…&returnUrl=/app/career/jobs` |
| axe WCAG 2.2 AA | 0 violations at 1280, 390 and 320 px |
| Overflow / tap targets / page errors | 0 / none below 24 px once a checkbox's label is measured (its real pointer target) / 0 |

## What fixtures prove (unit + Playwright)

`JobObservationServiceTests` (36), `CareerJobObservationApiTests` (8) and `tests/career-job-observations.spec.ts` (12) cover the paths a missing key cannot exercise live:

- mapping into the contract shape, including hourly vs annual rate intervals;
- duplicate provider ids dropped and counted, and reposts (same agency, title, locations and pay) dropped and counted with the newest kept;
- a multi-location posting as one observation, eligible when any location matches, with the matching location flagged;
- remote `unknown` excluded by an eligible-only filter and counted, returned when `remote=unknown`, and `remote=eligible` returning only eligible;
- expired postings excluded and counted, with their pay not presented as current;
- other-location-only postings excluded and counted;
- the 25-observation cap with `truncated`, deterministic sort, the `q` filter;
- the source-URL allowlist: a non-https or third-party URL is dropped and the observation still returns, without a link;
- `stalePreference` naming both the saved place and the current filter;
- malicious listing content: a title containing `<img src=x onerror=alert(1)>` is rendered as literal text with no element injected (no `innerHTML`, no `bypassSecurityTrust`);
- exactly one provider request with `ResultsPerPage ≤ 25` even when the provider reports hundreds of results (no pagination walk);
- no persistence: the EF model has no posting entity and the service exposes only read methods.

## Findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | — | The no-key path is the documented honest unavailable state, and the benchmark surfaces are unaffected. Nothing outstanding. | — |
| 2 | P3 | USAJOBS documents a remote indicator but no reliable per-posting *ineligibility* field, so the adapter can only emit `eligible` or `unknown`; location mismatches are excluded as `otherLocationExcluded` instead. | Recorded in ADR 0015 and the contract rather than inventing a restriction; `ineligible` stays in the contract for a provider that states one. |
| 3 | P3 | My first two probes reported the area filter as not restoring and a 20 px input as a tap-target failure; both were the script's selectors (the area field is `#job-area`, and a checkbox's real target is its label). | Fixed the probes; the recorded evidence now matches the page. |
| 4 | P3 | Live verification of the populated list needs an operator key, so the observation-list evidence is fixture-level. | Stated here; the key request is the recorded operator gate, and no paid service is involved. |
| R1 | P1 | `eligibleOnly=true` leaked `ineligible` postings (only unknown was dropped). | Fixed: requires `eligible`; `remoteIneligibleExcluded` counted; test asserts the ineligible row is gone. |
| R2 | P2 | A repost key without close date, series or grade swallowed distinct openings. | Fixed: key includes them; distinct-close-date and series/grade tests, true repost still collapses. |
| R3 | P2 | Counts did not reconcile and truncation ignored the provider total. | Fixed: `fetched`, `keywordExcluded`, `remoteFilterExcluded`, `cappedByLimit`; `truncated` also when the provider total exceeds the returned rows; sum test. |
| R4 | P2 | `observedFrom/To` overstated coverage; no occupation looked "available". | Fixed: `postedFrom/postedTo`; `occupation_required` unavailable state. |
| R5 | P2 | Adapter followed redirects, read an unbounded body, accepted non-default ports. | Fixed: no redirects, 1 MB bounded read, `IsDefaultPort`; tests for each. |
| R6 | P3 | Mapping errors could 500; unbounded locations; inverted pay range. | Fixed: any error is `source_unavailable`; 10 locations; min>max is `not_available`. |
| R7 | P2 | UI copy claimed user-specific eligibility. | Fixed: "Remote stated by the posting", "Only postings that state remote work"; no "eligible" beside a posting's status. |

R1-R7: all fixed.

Open P0/P1: **none**.
