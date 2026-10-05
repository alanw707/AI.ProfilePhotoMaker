# Career API: comparable-pay analysis (#384)

Same envelope, auth, flag (403 `CareerWorkspaceDisabled`) and error shape as `api-profile-goal.md`. Design: ADR 0013. Rules: ADR 0012. Runs: `api-agent-runs.md`.

## Start

`POST /api/career/runs` with `{ "task": "pay_analysis" }` and an `Idempotency-Key`. Needs a confirmed profile (409 `CareerProfileRequired`) and a goal with a confirmed occupation (409 `CareerOccupationRequired`). No text model is required and no allowance unit is spent. Steps: `read_goal` "Read your career goal", `read_benchmark` "Looked up the occupational benchmark", `evaluate_cohort` "Evaluated advertised-pay observations", `build_scenario` "Compared your requested pay", `save_analysis` "Saved your pay analysis". A completed run's DTO has `payAnalysisId`.

## Read

`GET /api/career/pay-analyses` → `{ "analyses": [summary…] }` newest first, max 20; summary = `{ id, occupationCode, occupationTitle, areaTitle, status, personalizedAvailable, stale, createdAt }`.

`GET /api/career/pay-analyses/{id}` → 200 (404 `CareerPayAnalysisNotFound` when missing or another owner's):

```json
{
  "id": "guid", "runId": "guid",
  "status": "complete | partial",
  "occupation": { "code": "15-1252.00", "title": "Software Developers", "publishedCode": "15-1252", "mapping": "exact | broad | shared" },
  "location": { "input": "Denver, CO", "resolution": "metro | state | national_only | unresolved",
                "local": { "code": "19740", "title": "Denver-Aurora-Centennial, CO", "type": "metro" } },
  "pinned": { "profileVersion": 3, "goalVersion": 2, "oewsRelease": "2025-05", "oewsSnapshotSha256": "…",
              "projectionsRelease": "2025-2035", "ruleVersion": "candidate-1.0", "observationSourceId": null },
  "inputHash": "sha256-hex",                 // canonical pinned inputs; recompute must reproduce it
  "stale": false, "staleReasons": [],
  "sections": [
    { "key": "benchmark", "title": "Occupational benchmark", "status": "complete | unavailable | failed",
      "reason": null,                          // unavailable: "location_unresolved" | "not_published" | "source_unavailable"
      "label": "Occupational wage benchmark",  // never "personalized"
      "note": "Published BLS wages for this occupation, not advertised pay and not a prediction.",
      "figures": [
        { "key": "medianAnnual", "label": "Median annual wage", "value": 135980, "status": "available | not_available | top_coded | not_published",
          "unit": "usd_per_year", "areaCode": "99", "areaTitle": "U.S.", "sourceId": "oews" }
      ] },
    { "key": "personalized", "title": "Advertised pay from a qualified cohort", "status": "unavailable | complete | insufficient_evidence",
      "reason": "provider_rights_unverified | insufficient_observations | insufficient_employers",
      "interval": null,                        // { "low": 110300, "high": 195200, "unit": "USD / year", "definition": "…" } when available
      "cohort": { "included": 0, "excluded": 0, "employers": 0, "largestEmployerShare": 0, "concentrated": false,
                  "sensitive": false, "exclusionReasons": {} },
      "note": "No qualified source: no provider has granted written rights for ongoing commercial use." },
    { "key": "scenario", "title": "Your requested pay", "status": "complete | unavailable",
      "requestedAnnual": 150000, "benchmarkMedianAnnual": 135980, "gapAnnual": 14020, "gapPercent": 10.3,
      "note": "Your target is a preference, not evidence about what employers pay." }
  ],
  "blockedReasons": ["provider_rights_unverified"],
  "qualification": { "personalizedAllowed": false,
                     "gates": [ { "gateId": "G1", "requirement": "…", "status": "Unverified", "evidence": "…" } ] },
  "sources": [ { "id": "oews", "name": "…", "publisher": "U.S. Bureau of Labor Statistics", "referencePeriod": "2025-05",
                 "publishedOn": "2026-05-15", "url": "…", "definitionsUrl": "…", "license": "Public domain (U.S. government work)",
                 "citation": "…", "definition": "…", "coverage": "…" } ],
  "createdAt": "…"
}
```

`value` is null unless `status` is `available` (`top_coded` carries the top code). Benchmark figures: `medianAnnual`, `pct25Annual`, `pct75Annual` (plus the local row). Scenario figures are computed from the benchmark median and the goal's requested pay; the percentage is one decimal.

## Recompute

`POST /api/career/pay-analyses/{id}/recompute` → 200:

```json
{ "matches": true, "inputHash": "…", "storedInputHash": "…", "differences": [], "sections": [ …same shape as the stored read… ] }
```

`matches: false` (with `differences` naming the changed fields) means the stored row and a fresh computation disagree — the stored row is left untouched and the caller decides. 404 for another owner's analysis.

## Qualification

`GET /api/career/pay/qualification` → `{ "personalizedAllowed": false, "blockedReasons": ["provider_rights_unverified"], "gates": [ …G1–G8… ] }` from the ADR 0012 gate model.
