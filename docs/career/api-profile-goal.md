# Career API: profile and goal (#378)

All endpoints require authentication and `Features:CareerWorkspace=true`. Responses use the existing envelope:

```json
{ "success": true, "data": { ... }, "message": null, "error": null }
{ "success": false, "error": { "code": "ValidationError", "message": "...", "fieldErrors": { "targetRole": "Required." }, "correlationId": "..." } }
```

| Status | Code | When |
|---|---|---|
| 401 | `Unauthorized` | not signed in |
| 403 | `CareerWorkspaceDisabled` | flag off |
| 404 | `CareerProfileNotFound` / `CareerGoalNotFound` / `CareerVersionNotFound` | none yet, or not yours |

`GET /api/career/profile/versions` returns an empty list (200) when no profile exists. An `If-Match` that is not exactly the current `"profile-vN"` / `"goal-vN"` tag (including `*` and weak `W/` tags) is treated as stale (412).
| 400 | `ValidationError` (+ `fieldErrors`) | invalid input or `confirmed` not true |
| 409 | `CareerGoalAlreadyExists` | POST goal when one exists |
| 412 | `CareerVersionConflict` | `If-Match` is stale (body includes `currentVersion`) |
| 428 | `CareerPreconditionRequired` | update without `If-Match` |

Writes return the new `ETag` header (`"profile-v3"`, `"goal-v2"`) and the same value in `data.etag`.

## Profile

`GET /api/career/profile` → `CareerProfileDto` or 404 `CareerProfileNotFound`.

`PUT /api/career/profile` (`If-Match` required once a profile exists) body:

```json
{
  "currentTitle": "Data analyst",          // required, ≤120
  "industry": "Healthcare",                // optional, ≤120
  "yearsExperience": 4,                    // optional, 0–60
  "location": "Denver, CO",                // optional, ≤120
  "summary": "…",                          // optional, ≤2000
  "skills": ["SQL", "Tableau"],            // ≤50 items, each 1–80 chars, de-duplicated case-insensitively
  "highlights": ["Built weekly KPI report"], // ≤20 items, each 1–300 chars
  "workArrangement": "hybrid",             // optional: onsite | hybrid | remote | flexible
  "confirmed": true                        // must be true
}
```

`CareerProfileDto`:

```json
{
  "id": "guid", "version": 3, "etag": "\"profile-v3\"",
  "facts": { ...same fields as the body except confirmed... },
  "provenance": { "source": "manual", "confirmedAt": "2026-09-30T12:00:00Z", "restoredFromVersion": null },
  "createdAt": "...", "updatedAt": "..."
}
```

`GET /api/career/profile/versions` → `[{ "version": 3, "createdAt": "...", "source": "manual", "currentTitle": "...", "isActive": true }]` newest first, max 50.

`GET /api/career/profile/versions/{version}` → `{ "version": 2, "facts": {...}, "provenance": {...}, "createdAt": "...", "isActive": false }`.

`POST /api/career/profile/versions/{version}/restore` (`If-Match` required) → new active `CareerProfileDto` copying that version. Restoring is an explicit acceptance: `confirmedAt` is the restore time and `provenance.restoredFromVersion` names the source.

## Goal

`GET /api/career/goals` → active `CareerGoalDto` or 404 `CareerGoalNotFound`.

`POST /api/career/goals` → 201 `CareerGoalDto` (409 if one exists). `PATCH /api/career/goals/{id}` (`If-Match` required) → updated `CareerGoalDto`. Both take:

```json
{
  "targetRole": "Senior data analyst",   // required, ≤120
  "targetLocation": "Seattle, WA",        // optional, ≤120
  "workArrangement": "remote",            // optional: onsite | hybrid | remote | flexible
  "desiredPayMin": 95000,                 // optional, USD per year, 0–1,000,000
  "desiredPayMax": 120000,                // optional, ≥ desiredPayMin
  "weeklyEffortHours": 5,                 // optional, 1–40
  "confirmed": true
}
```

`CareerGoalDto`:

```json
{
  "id": "guid", "version": 2, "etag": "\"goal-v2\"",
  "goal": { ...fields above except confirmed... },
  "basedOnProfileVersion": 3, "isStale": false,
  "provenance": { "source": "manual", "confirmedAt": "..." },
  "createdAt": "...", "updatedAt": "..."
}
```

`isStale` is true when a profile exists that the goal was not confirmed against: its active version is newer than `basedOnProfileVersion`, or a profile was saved after a goal created without one (`basedOnProfileVersion: null`). Re-saving the goal clears it. A goal created with no profile is not stale until a profile is saved.

`GET /api/career/goals/{id}/versions` → `[{ "version": 2, "createdAt": "...", "targetRole": "...", "isActive": true }]`.

`GET /api/career/goals/{id}/versions/{version}` → `{ "version": 1, "goal": {...}, "basedOnProfileVersion": 1, "provenance": {...}, "createdAt": "...", "isActive": false }`.

`POST /api/career/goals/{id}/versions/{version}/restore` (`If-Match` required) → new active `CareerGoalDto` copying that version, re-based on the current profile version (so not stale).

Another owner's goal ID always returns 404.

## Client flag

`GET /api/config/client` → `features.careerWorkspace` (boolean). The UI must still treat 403 `CareerWorkspaceDisabled` as authoritative.
