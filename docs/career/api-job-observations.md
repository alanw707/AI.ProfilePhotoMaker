# Career API: job observations (#386)

Same envelope, auth, flag (403 `CareerWorkspaceDisabled`) and error shape as `api-profile-goal.md`. Design: ADR 0015. Nothing is persisted.

## Read

`GET /api/career/jobs/observations?area=19740&eligibleOnly=true&remote=all|eligible|ineligible|unknown&q=` → 200:

```json
{
  "occupation": { "code": "15-1252.00", "title": "Software Developers" },
  "area": { "input": "Denver, CO", "resolution": "metro", "code": "19740", "title": "Denver-Aurora-Centennial, CO" },
  "coverage": {
    "available": false,
    "reason": "source_not_configured",          // source_not_configured | source_unavailable | null
    "sourceId": "usajobs", "sourceName": "USAJOBS",
    "coverage": "U.S. federal agencies only; not the private-sector market.",
    "attribution": "Job postings from USAJOBS (U.S. Office of Personnel Management). Displayed values are unchanged.",
    "sourceUrl": "https://www.usajobs.gov/",
    "retrievedAt": "2026-10-05T18:30:00Z",
    "observedFrom": null, "observedTo": null,   // posting date range in the returned set
    "counts": { "matched": 0, "shown": 0, "duplicateIds": 0, "duplicateReposts": 0, "expired": 0,
                "remoteUnknownExcluded": 0, "otherLocationExcluded": 0 }
  },
  "preferences": { "areaCode": "19740", "stalePreference": false, "note": null },
  "observations": [
    {
      "observationId": "usajobs:812345678",
      "title": "IT Specialist (Applications Software)",
      "organization": "Department of Veterans Affairs",
      "locations": [ { "city": "Denver", "state": "CO", "areaCode": "19740", "match": "user_area" },
                     { "city": "Colorado Springs", "state": "CO", "areaCode": null, "match": "other" } ],
      "multiLocation": true,
      "pay": { "min": 98500, "max": 128000, "unit": "usd_per_year", "basis": "annual", "status": "available" },
      "postedOn": "2026-09-28", "closesOn": "2026-10-20",
      "remoteEligibility": "unknown",            // eligible | ineligible | unknown
      "remoteNote": "The posting does not state a remote restriction.",
      "series": "2210", "grade": "GS-12",
      "sourceUrl": "https://www.usajobs.gov/job/812345678",
      "sourceId": "usajobs"
    }
  ],
  "truncated": false,
  "note": "Postings are observations, not employment totals or an outlook: a list of open federal jobs is not the labour market."
}
```

- `pay.status`: `available | not_available | top_coded`; `pay.unit` is `usd_per_year` or `usd_per_hour` and `basis` follows the provider's rate interval (annual or hourly), converted to nothing — displayed as published, with the basis stated. An expired posting's pay is not returned as current: the observation is marked `expired: true` and excluded from the default list.
- `remoteEligibility` is what the provider states: USAJOBS gives an indicator, so it yields `eligible` or `unknown` and never `ineligible` (a location that does not match your area is excluded as `otherLocationExcluded` instead). Remote-eligible postings are not location-filtered, because remote work is not tied to the listed office.
- `remoteEligibility`: `unknown` observations are never returned when `eligibleOnly=true` (counted in `counts.remoteUnknownExcluded`); `remote=eligible` returns only `eligible`, and `remote=unknown` only `unknown`.
- `closesOn` in the past marks the observation `expired: true`; the default list excludes it and counts it. `q` filters on title/organization substrings (case-insensitive). `area` defaults to the goal's preferred area, else the goal's location text.
- Bounded: one provider page, at most 25 observations returned, `truncated: true` when the cap applies.
- `sourceUrl` is validated as an `https` URL on the provider's host allowlist; anything else is dropped and the observation is returned without a link rather than with an unverified one.
- `coverage.available: false` with a reason is the honest unavailable state: the list is empty, the page says why, and **no other endpoint changes behaviour** (the benchmark brief, pay analysis and market comparison still work).

## Reference

`GET /api/career/jobs/source` → `{ "sourceId": "usajobs", "name": "USAJOBS", "configured": false, "coverage": "…", "attribution": "…", "sourceUrl": "…" }` — the page uses this to explain the unavailable state without a second request to the provider.
