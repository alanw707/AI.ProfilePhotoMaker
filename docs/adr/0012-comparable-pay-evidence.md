# ADR 0012: Comparable-pay evidence is qualified, versioned and fails closed

Status: accepted (2026-10-05, ticket #383, spec #376). Rule version `candidate-1.0`.

## Context

#384 is meant to show a personalized comparable-pay range from real advertised pay. That needs job-posting observations, and no posting provider has been licensed: #383 is explicitly "a verifiable discovery slice, not an invented production integration". The risk is labelling a BLS occupational benchmark "personalized", or letting a model invent salary rows.

The qualification work for this ticket already exists on this branch and is the report of record: **`research/comparable-pay/QUALIFICATION.md`** (read-only provider checks on 2026-09-23 UTC; live NYC Open Data and 14 Greenhouse boards sampled; no key, account, contract or paid terms acquired). It is re-verified here on 2026-10-05, and its rule is validated as code by `research/comparable-pay/cohort.mjs` + `fixtures.mjs` + `cohort.test.mjs` (5/5 Node tests pass).

## Decision

### 1. Provider qualification: no provider passes; personalized analysis is blocked

`research/comparable-pay/QUALIFICATION.md` assesses NYC Open Data Jobs, Adzuna, USAJOBS, Greenhouse boards and BLS OEWS against access, fields, rights/retention and price. The binding gate is commercial rights for ongoing aggregation, display and retention:

- **NYC Open Data** — open, but one municipal employer in one city: fails coverage and the five-employer floor.
- **Adzuna** — the most plausible national feed (documented U.S. search with pay fields, 25/min, 250/day, 1,000/week, 2,500/month), but ongoing aggregated commercial use needs written consent and attribution, and no account or quote exists: rights **unverified**.
- **USAJOBS** — federal sector only; stand-alone redistribution prohibited: fails coverage and rights for a general market.
- **Greenhouse job boards** — public GET access to individual employer boards is documented, which is not a licence to aggregate many employers into a salary product; the structured `pay_input_ranges` field carries amounts but no normalized annual/hourly basis, and level, employment type, cross-post dedup and remote restrictions stay unresolved.
- **BLS OEWS** — public domain, but occupational benchmarks, never advertised postings.

No source clears all gates, and this ticket forbids accepting new paid terms. Therefore **personalized advertised-pay analysis is blocked for production release** (`personalizedAllowed = false`). #384 may present only a *benchmark* range from the pinned BLS OEWS snapshot (ADR 0011), labelled as a benchmark, with the reason and a reference to the qualification report. Re-enabling personalized analysis requires a written rights decision recorded in this ADR plus a passing gate row for at least one provider.

### 2. Observation rules (version `candidate-1.0`, deterministic, no model)

`research/comparable-pay/cohort.mjs` is the reference implementation; the API port must reproduce its fixture outputs exactly. Input per observation: canonical requisition id, employer, role/occupation family, geography, level, employment type, work-location eligibility, whether the employer disclosed the range, currency, pay basis, annual hours (for hourly), low, high, updated date.

1. **Include** only employer-disclosed USD annual pay, or hourly pay **with explicit annual hours** (never an assumed 2,080). Exclude unknown basis, missing annual hours, unknown or ineligible work location, malformed ranges, non-USD, stale observations, non-matching occupation family/geography, and unconfirmed level or employment type when those filters are requested.
2. **Deduplicate** by canonical requisition id. Reposts and multi-location ids still need provider-side reconciliation; the fixture does not prove that ability.
3. **Lookback** 90 days, with a floor of **10 independent observations from at least 5 employers**. Report the largest employer's share. Recalibrate with real coverage; never lower the floor to produce a number.
4. **Interval** = P25 of annualized **lower** bounds to P75 of annualized **upper** bounds, by linear interpolation at position `(n−1)×p`, rounded to whole USD. It is an observed advertised-pay interval: not a confidence interval, a base-salary guarantee, or a person's worth.
5. **Fallback** when the floor is not met: `occupational benchmark fallback` with a **null** personalized interval; the benchmark carries its own source, measure and period and is never silently substituted as individualized pay.
6. **No synthesis.** No model-generated salary rows, no years-of-experience multiplier, no inferred seniority.

### 3. Fixtures, versions and evidence

Sanitized synthetic fixtures (`research/comparable-pay/fixtures.mjs`) cover three families (software, operations, nursing) across Denver and Seattle with six fictional employers each, plus edge cases (duplicate requisition, unknown remote eligibility, modelled pay, stale, hourly with explicit hours, unknown basis, different level). Expected synthetic intervals: **$110,300–$195,200**, **$84,300–$156,200**, **$97,300–$162,200**. Every displayed number must be reproducible from the fixtures; a rule change means a new version and re-pinned expectations, and the Node tests and the API tests must agree.

## Consequences

- #384 ships a benchmark-only comparable-pay view until a provider passes the gates in writing; the UI must say "benchmark, not advertised pay" and why personalized analysis is unavailable.
- The observation model, rules and fixtures are ready for a licensed provider, so enabling it later is a data-source change, not a rewrite.
- The report is a dated judgment, not a legal opinion: public access does not establish an ongoing right to aggregate and retain.
