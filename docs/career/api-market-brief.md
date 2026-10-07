# Career API: market brief (#382)

Same envelope, auth, flag (403 `CareerWorkspaceDisabled`) and error shape as `api-profile-goal.md`. Design: ADR 0011. Runs: `api-agent-runs.md`.

## Start

`POST /api/career/runs` with `{ "task": "market_brief" }` and an `Idempotency-Key`. Needs a confirmed profile (409 `CareerProfileRequired`) and a goal with a confirmed occupation (409 `CareerOccupationRequired`). No model, no allowance spent. Steps: `read_goal` "Read your career goal", `look_up_wages` "Looked up wages and employment", `look_up_outlook` "Looked up the job outlook", `compare_alternatives` "Compared related occupations", `save_brief` "Saved your market brief". A completed run's DTO has `marketBriefId`.

## Read

`GET /api/career/market-briefs` → `{ "briefs": [summary…] }` newest first, max 20; summary = `{ id, occupationCode, occupationTitle, areaTitle, status, stale, createdAt }`.

`GET /api/career/market-briefs/{id}` → 200 (404 `CareerMarketBriefNotFound` when missing or another owner's):

```json
{
  "id": "guid", "runId": "guid",
  "status": "complete | partial",
  "occupation": { "code": "15-1252.00", "title": "Software Developers",
                  "published": { "oews": { "code": "15-1252", "match": "exact" }, "projections": { "code": "15-1252", "match": "exact" } } },
  "location": { "input": "Denver, CO", "resolution": "metro | state | national_only | unresolved",
                "local": { "code": "19740", "title": "Denver-Aurora-Centennial, CO", "type": "metro" } },
  "pinned": { "profileVersion": 3, "goalVersion": 2, "oewsRelease": "2025-05", "projectionsRelease": "2025-2035" },
  "stale": false, "staleReasons": [],                // "goal_changed" | "profile_changed" | "occupation_changed"
  "dataStale": false,
  "sections": [
    {
      "key": "wages | employment | outlook | alternatives",
      "title": "Wages",
      "status": "complete | unavailable | failed",
      "reason": null,                                // unavailable: "location_unresolved" | "not_published"; failed: "CareerReferenceUnavailable"
      "note": "Benchmark wages for this occupation. Not open jobs, not a personal salary prediction, not total compensation.",
      "figures": [
        { "key": "medianAnnual", "label": "Median annual wage", "value": 135980,
          "status": "available | not_available | top_coded | not_published",
          "unit": "usd_per_year | usd_per_hour | jobs | jobs_thousands | per_1000_jobs | ratio | percent | percent_rse | text",
          "areaCode": "99", "areaTitle": "U.S.", "sourceId": "oews" }
      ],
      "items": []                                    // alternatives only: [{ code, title, note: string | null, figures: [...] }]
    }
  ],
  "nextAction": { "label": "Check your target occupation", "route": "/app/career/occupation" },
  "sources": [
    { "id": "oews", "name": "…", "publisher": "U.S. Bureau of Labor Statistics", "referencePeriod": "2025-05", "publishedOn": "2026-05-15",
      "url": "…", "definitionsUrl": "…", "license": "Public domain (U.S. government work)", "citation": "…",
      "definition": "…", "coverage": "…" }
  ],
  "createdAt": "…"
}
```

Published `match` is `exact` (one O*NET occupation per published SOC code), `shared` (multiple detailed O*NET occupations use one published SOC estimate), or `broad` (fallback to a published SOC broad group). Broad and shared mappings name the published code and title in the section note (or each related occupation item's nullable `note`); exact mappings add no mapping note. Related occupations cite both sources in their figures and show their own as-of and coverage line.

`not_published` also covers wages outside the occupation's published pay basis (annual-only or hourly-only occupations). `value` is null unless `status` is `available` (`top_coded` carries the top code with unit). Wages section figures (per area, national first then local): `medianAnnual`, `pct10Annual`, `pct25Annual`, `pct75Annual`, `pct90Annual`, `meanAnnual`, `medianHourly`, `meanPrse`; local also `medianDifferenceAnnual` (local median − national median, dollars; only when both are available). Employment: `employment`, `employmentPrse`, and for local `jobsPer1000`, `locationQuotient`. Outlook: `employment2025`, `employment2035`, `changePercent`, `annualOpenings` (thousands), `typicalEducation` (text).

## Reference

`GET /api/career/market/reference` → `{ "sources": [...], "areaCount": 445, "occupationCount": 831 }` (503 `CareerReferenceUnavailable` when neither source loads).
