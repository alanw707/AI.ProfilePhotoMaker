# ADR 0012: Comparable-pay evidence is qualified, versioned and fails closed

Status: accepted (2026-10-05, ticket #383, spec #376)

## Context

#384 is meant to show a personalized comparable-pay range from real advertised pay. That needs job-posting observations, and no posting provider has been licensed (#383 is a discovery slice: "This is a verifiable discovery slice, not an invented production integration"). Meanwhile BLS OEWS gives public-domain occupational benchmarks (ADR 0011). The risk is labelling a benchmark "personalized", or inventing salary rows from a model.

## Decision

### 1. Provider qualification gates (report: `docs/career/pay-evidence-qualification.md`)

A provider may back personalized advertised-pay analysis only if every gate passes:

| Gate | Requirement |
|---|---|
| G1 U.S. coverage | Postings for the occupations we serve, not a narrow segment |
| G2 Pay attributes | Explicit pay **and** pay basis (annual/hourly) on a usable share of postings |
| G3 Level/location attributes | Explicit seniority/level and location (or an explicit remote restriction) |
| G4 Remote honesty | Location restrictions visible, so remote is not treated as universally eligible |
| G5 Deduplication | Stable observation ids, or enough fields to dedupe reposts |
| G6 Freshness | Publication date, so a 90-day window is enforceable |
| G7 Commercial rights | Display, cache and retention rights for a paid product, in writing |
| G8 Cost | Pricing compatible with a free career tier |

G7 is the binding gate. Free/keyed APIs (USAJOBS, Adzuna, The Muse, RemoteOK, Arbeitnow) either restrict redistribution and caching, require attribution/link-back, cover only a segment (USAJOBS is federal-only), or publish terms that cannot be verified as granting commercial display and cache rights from the public pages. **No provider passes G7 today**, and the issue forbids accepting new paid terms, so:

**Personalized advertised-pay analysis is blocked for production release.** `personalizedAllowed` is false; #384 may only present a *benchmark* range from the pinned BLS OEWS snapshot, labelled as a benchmark, with the blocked reason and the qualification report reference. Enabling personalized analysis later requires a written rights decision recorded in this ADR and a passing G1–G8 row.

### 2. Observation rules (versioned `pay-evidence-1`, deterministic, no model)

Input is a set of observations: `observationId`, `sourceId`, `occupationCode` (O*NET/SOC), `areaCode`, `postedAt`, `payMin`, `payMax`, `payBasis` (`annual` | `hourly` | `unknown`), `level` (`entry|mid|senior|lead|unknown`), `employerId`, `remote` (`eligible|restricted|unknown`).

- **Basis normalization.** `annual` is taken as posted; `hourly` × 2,080 with the assumption stated in the output; `unknown` basis is **excluded** (`missing_basis`) — never guessed.
- **Validity.** Require both `payMin` and `payMax` (`missing_pay`), `payMin > 0` and `payMax >= payMin` (`invalid_range`). A single advertised value is treated as a missing interval.
- **Level.** An observation whose `level` is `unknown` is excluded (`level_unknown`) unless the goal pins an explicit level, in which case only that level is kept (`level_mismatch` otherwise). No seniority is inferred from the title.
- **Deduplication.** Drop repeated `observationId` (`duplicate_id`) and reposts sharing the same `employerId`, normalized pay, `areaCode` and `occupationCode` (`duplicate_repost`).
- **Lookback.** Keep observations posted within 90 days before `asOf` (`stale`).
- **Cohort.** Keep the observation's occupation crosswalk code and area (or the goal's area), and exclude `remote = restricted` from an eligible-only cohort (`remote_restricted`).
- **Minimum evidence.** At least 10 independent observations from at least 5 distinct employers (`insufficient_observations`, `insufficient_employers`). Below either, the result is `insufficient_evidence`: no range is published, only the exclusions and a BLS benchmark fallback.
- **Interval.** Normalize each observation to an annual midpoint, sort, and take percentiles by linear interpolation between order statistics (the R-7/BLS convention). Report p25, p50 (median), p75, plus the observed minimum and maximum envelope. The interval is always described as advertised pay, never as a prediction or total compensation.
- **Employer concentration.** Report the largest employer's share; flag `concentrated` above 40 %.
- **Sensitivity.** Recompute the median with the largest employer removed and with hourly-normalized observations removed; report both medians and flag `sensitive` when either moves the median by more than 10 %.
- **Exclusions and fallback.** Every excluded observation is counted by reason; missing attributes are reported by kind; the benchmark fallback is always available from the pinned OEWS snapshot and is labelled a benchmark.
- **No synthesis.** The engine emits only values derived from observations; it never generates a row, and it never multiplies by experience.

### 3. Fixtures and versioning

Sanitized, synthetic observations (no real postings) live with the tests, covering at least three occupation families, multiple geographies, and the covered, sparse, duplicate-heavy, concentrated, sensitive and mixed-basis cases. Each fixture records the rule version `pay-evidence-1` and its expected result; changing a rule means a new version and re-pinned expectations. Every displayed number must be reproducible from the fixture.

## Consequences

- #384 ships a benchmark-only comparable-pay view until a provider passes G1–G8 in writing; the UI must say "benchmark, not advertised pay" and why personalized analysis is unavailable.
- The observation model, rules and fixtures are ready for a licensed provider, so enabling it later is a data-source change, not a rewrite.
