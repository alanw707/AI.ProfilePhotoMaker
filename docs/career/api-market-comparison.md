# Career API: market comparison (#385)

Same envelope, auth, flag (403 `CareerWorkspaceDisabled`) and error shape as `api-profile-goal.md`. Design: ADR 0014. Source: the pinned BLS snapshot (ADR 0011).

## Metrics

`GET /api/career/markets/metrics` → 200:

```json
{ "metrics": [
  { "key": "median_wage", "label": "Median annual wage", "unit": "usd_per_year", "measure": "BLS OEWS median annual wage",
    "supported": true, "reason": null, "geographyLevels": ["national","state","metro"] },
  { "key": "employment", "label": "Employment", "unit": "jobs", "measure": "Estimated wage and salary employment",
    "supported": true, "reason": null, "geographyLevels": ["national","state","metro"] },
  { "key": "location_quotient", "label": "Employment concentration", "unit": "ratio",
    "measure": "Occupation's share of area employment relative to the nation", "supported": true, "reason": null,
    "geographyLevels": ["state","metro"] },
  { "key": "share_of_national_employment", "label": "Share of national employment", "unit": "percent",
    "measure": "This area's share of the occupation's national employment", "supported": true, "reason": null,
    "geographyLevels": ["state","metro"] },
  { "key": "projected_change", "label": "Projected employment change", "unit": "percent",
    "measure": "BLS Employment Projections", "supported": false, "reason": "national_only_source",
    "geographyLevels": ["national"] }
] }
```

## Compare

`GET /api/career/markets/compare?metric=median_wage&level=state|metro&areas=08,19740&q=den` → 200:

```json
{
  "occupation": { "code": "15-1252.00", "title": "Software Developers", "publishedCode": "15-1252", "mapping": "exact" },
  "metric": { "key": "median_wage", "label": "Median annual wage", "unit": "usd_per_year",
              "measure": "BLS OEWS median annual wage", "supported": true, "reason": null },
  "level": "state",
  "national": { "areaCode": "99", "areaTitle": "U.S.", "value": 135980, "status": "available" },
  "reference": { "release": "2025-05", "publishedOn": "2026-05-15", "coverage": "Nonfarm establishments in all 50 states and DC; national, state and metropolitan area cross-industry estimates.",
                 "definitionsUrl": "…", "citation": "…" },
  "areas": [
    { "areaCode": "08", "areaTitle": "Colorado", "type": "state", "value": 138390, "status": "available",
      "rank": 12, "rankedOf": 49, "selected": true, "shareOfNationalEmployment": 2.6 }
  ],
  "selectionLimit": 3,
  "truncated": false
}
```

- `level=state` returns every state (50 + DC) plus the nation; `level=metro` returns every metro area in the snapshot (including Alaska and Hawaii). `q` filters by area title (case-insensitive substring); `areas` marks up to `selectionLimit` areas as `selected` and is echoed, so filters survive a reload. An unknown metric or level answers 400 `ValidationError`; an unsupported metric answers 409 `CareerMetricUnsupported` with the reason; a goal without a confirmed occupation answers 409 `CareerOccupationRequired`.
- `areas` is echoed back and each area carries `selected`, which reflects the `areas` query parameter. The page keeps its own selection state and does not re-fetch on selection change, so `selected` is a convenience for other clients rather than the page's source of truth.
- `value` is null unless `status` is `available` (`top_coded` carries the published ceiling). `status`: `available | not_available | top_coded | not_published`. `rank` counts only `available` areas, best first for wage/employment/concentration (a higher value is better for these metrics); `rankedOf` is the number of ranked areas. Suppressed areas are never ranked as lowest, and a `top_coded` area shows its ceiling but is **not** ranked, because its true value is unknown. Ranks are computed on unrounded values and ties break by title then code, so they are deterministic.
- `national` is always present so every area can be compared with the nation; state and metro figures are never summed together.
- Bounded: the response is capped at 500 areas with `truncated: true` when the cap applies, and the same array powers the heatmap and the table.

## Save a location preference

`POST /api/career/markets/preference` with `{ "areaCode": "19740", "level": "metro", "confirmed": true }` and `If-Match: "goal-vN"` → 200 goal DTO (new version, `targetLocation` set to the area title, `preferredAreaCode` recorded) + `ETag`.

| Status | Code | When |
|---|---|---|
| 400 | `ValidationError` | missing/invalid `areaCode`/`level`, or `confirmed` not true |
| 404 | `CareerAreaNotFound` | the area is not in the snapshot at that level |
| 409 | `CareerGoalRequired` | no goal |
| 412 | `CareerVersionConflict` | stale `If-Match` |
| 428 | `CareerPreconditionRequired` | no `If-Match` |

The goal DTO gains `preferredArea`: `{ "code": "19740", "title": "Denver-Aurora-Centennial, CO", "level": "metro" }` or null, carried forward by later goal edits and restore, like the occupation.
