# ADR 0013: Comparable-pay analysis is pinned, reproducible and benchmark-first

Status: accepted (2026-10-05, ticket #384, spec #376). Builds on ADR 0011 (BLS benchmark) and ADR 0012 (evidence qualification, rule `candidate-1.0`).

## Context

#384 asks Analytics to "augment the broad occupational benchmark with a personalized comparable-pay interval derived deterministically from a qualified cohort, or an explicit sparse-evidence fallback". ADR 0012 concluded that **no provider qualifies** (commercial aggregation/display/retention rights unverified, `personalizedAllowed = false`), and #384 may not buy or accept terms. So the honest shape of this slice is: ship the whole pipeline, show the benchmark as the only evidence, and show the personalized interval as explicitly unavailable with reasons — never as a number, and never labelled personalized when it is a benchmark.

## Decision

- **Benchmark first, always shown.** The analysis shows the BLS OEWS benchmark for the confirmed occupation in the goal's area (national and local), with the pinned release, area title, units and the "occupational benchmark, not advertised pay" label. When OEWS is unavailable, the benchmark section is `unavailable` with a reason, and no substitute is invented.
- **Personalized interval only from a qualified cohort.** The personalized section is produced by `PayEvidenceRules` (ADR 0012) over observations from an `IPayObservationSource`. With no provider configured the source yields nothing, the cohort is empty, and the section is `unavailable` with `provider_rights_unverified` plus the gate reference; it carries a **null** interval. When a provider is later configured and the cohort passes the floors, the same code path produces the observed interval with its cohort counts — no rewrite. A cohort that fails the floors reports `insufficient_evidence` with the exclusion counts, never a smaller number.
- **Three things, never merged.** `benchmark` (published statistics), `personalized` (observed advertised pay from a qualified cohort, or null with reasons) and `scenario` (what the user's requested pay implies against the benchmark median: the gap in dollars and a percentage, labelled a preference and never evidence). Requested pay is only ever an input to the scenario.
- **Pinned inputs and reproducibility.** Every analysis row stores: profile version, goal version, occupation code/title, area code/title and resolution, requested pay and which end of the goal's desired pay it came from, OEWS release + snapshot SHA-256, projections release, rule version, provider-qualification state (gate statuses), the observation-source id and count, and a canonical input document with its SHA-256 (`inputHash`). `POST /api/career/pay-analyses/{id}/recompute` recomputes from the stored inputs and reports whether the figures and hash still match (`matches: true`) or diverged (`matches: false` with the differing fields) — it never overwrites the stored row. A changed input creates a **new** versioned row; nothing is edited in place.
- **Stale, not silently updated.** Profile, goal, occupation or area changes make a stored analysis `stale: true` with reasons, content unchanged. Source outage marks only the affected section `unavailable`/`failed` and keeps the rest.
- **No demographic or photo influence.** The analysis input type carries only career facts (occupation, area, requested pay, confirmed level/work arrangement when supplied). Tests assert that photo, gender, ethnicity and contact fields cannot reach the calculation, and that no model is called (the run task requires no text model and releases its allowance unit).
- **Cohort transparency.** When a cohort exists, the analysis reports included/excluded counts, exclusion reasons, employer count, largest-employer share, concentration and sensitivity flags, and the rule version; when it does not, it reports why (no qualified source, or the floors).
- **Privacy.** `CareerPayAnalysis` carries `OwnerId`, cascades on account deletion, joins `CareerPrivateDataService` coverage, and follows the run retention rule; the page states it. Observation data is never persisted — only counts and figures.

## Consequences

- #384 ships a benchmark-only pay view with an explicit blocked personalized state; enabling personalized pay later is a provider configuration plus a passing gate row, not a rewrite.
- The scenario is a preference comparison, not advice or a prediction; the copy says so.
- Reproducibility is verifiable by an independent party: same pinned inputs, same figures, same hash.
