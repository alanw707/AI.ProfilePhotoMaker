# #383 comparable-pay evidence qualification: review

Deliverable of record: **`research/comparable-pay/QUALIFICATION.md`** (provider checks dated 2026-09-23 UTC, re-verified 2026-10-05). Decision record: **ADR 0012**. This slice adds no endpoints, no database entities and no UI; it qualifies sources and fixes the calculation rules as versioned, testable code.

## Decision

**Personalized advertised-pay analysis is blocked for production release.** No evaluated provider clears all gates, and this ticket forbids accepting new paid terms:

| Source | Coverage | Rights for ongoing aggregation/display/retention | Verdict |
|---|---|---|---|
| NYC Open Data Jobs | one municipal employer, one city | open data, attribution check needed | methods fixture only |
| Adzuna | documented U.S. search with pay fields | ongoing aggregated commercial use needs written consent; no account or quote exists | preferred candidate, rights **unverified** |
| USAJOBS | federal sector only | stand-alone redistribution prohibited | supplemental only |
| Greenhouse boards | per-employer public GET; `pay_input_ranges` has amounts but no normalized basis | public read access is not a licence to aggregate many employers into a salary product | supplemental, rights unverified |
| BLS OEWS | national/state/metro occupational benchmarks | public domain with citation | qualified **benchmark** only, never a personalized cohort |

`PayEvidenceGates` encodes the G1–G8 rows and returns `personalizedAllowed = false` with `blockedReasons: [provider_rights_unverified]`, so the report is reproducible from code. #384 must present the BLS benchmark labelled as a benchmark.

## Rule `candidate-1.0` (ported from the validated research implementation)

Employer-disclosed USD pay only, annual or hourly **with explicit annual hours** (never an assumed 2,080); unknown basis, non-USD, unknown/ineligible work location, malformed ranges, stale observations, non-matching occupation family/geography and unconfirmed level or employment type are excluded with reason counts; dedup by canonical requisition id; 90-day lookback; floor of 10 observations from at least 5 employers; interval = P25 of annualized lower bounds to P75 of annualized upper bounds by linear interpolation at `(n−1)×p`, rounded to whole USD; largest-employer share reported; below the floor the interval is **null** with an `occupational benchmark fallback` decision. No model-generated rows, no experience multiplier, no inferred seniority.

## Verification

| Check | Result |
|---|---|
| Reference implementation `node --test research/comparable-pay/cohort.test.mjs` | 5 passed, 0 failed (unchanged) |
| .NET suite `dotnet test --filter "FullyQualifiedName!~Performance"` | 902 passed, 1 skipped (live OpenAI), 0 failed |
| Release build `-warnaserror` | clean |
| Pre-existing test files touched | none (only the new fixture, new test file and the test csproj) |
| Fixture intervals asserted as literals in C# | software/Denver $110,300–$195,200; operations/Denver $84,300–$156,200; nursing/Seattle $97,300–$162,200 |
| Independent recomputation | I re-derived every fixture figure from the raw synthetic observations with a separate Python implementation: all three families, the edge-case run (13 included, $109,400–$194,800), employer concentration and the exclusion counts matched exactly — the expectations are not self-fulfilling |
| Interpolation | `(4−1)×0.25 = 0.75` → 17.5 and `(4−1)×0.75 = 2.25` → 32.5, asserted in C# |
| Boundary | 90-day observation included, 91-day excluded; 7 observations → null interval; 12 observations from 1 employer → null interval |
| Sanitized data | synthetic observations only, no real postings or personal data |

## Findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | — | No provider clears the rights gate, so personalized pay cannot ship. | Accepted and recorded: this is the ticket's expected negative outcome, and the release gate blocks rather than mislabels. |
| 2 | P3 | The research rule was JavaScript-only, so production code could have drifted from the validated rule. | Addressed by porting it to `PayEvidenceRules` with the same reason strings, arithmetic and fixture numbers, cross-checked against the Node tests. |
| 3 | P1 | (review) `Evaluate` took a caller-supplied `providerQualified` boolean, so a caller could report authorization while the gate rows said no provider passes. | Fixed: the decision is derived from the gate rows only (`PayGateDecision.PersonalizedAllowed` = every row Passed), the boolean is gone, and tests prove a covered cohort stays blocked under the current rows while an all-passed row set authorizes it. |
| 4 | P3 | The old qualification test did not exercise that bypass. | Fixed: `AQualifiedCohortIsStillBlockedWhileTheProviderRightsAreUnverified` covers the covered-cohort case. |

Open P0/P1: **none**.
