# Career API: journey funnel (admin)

Answers "how far do people get in the career workspace, and do they come back?" from the server's own records.
Admin role only, like the rest of `api/admin/career`; not gated by `Features:CareerWorkspace`.
Shown on `/admin/career-usage` (Journey funnel) and pulled by `scripts/career/stage3-usage.sh`.

- `GET /api/admin/career/funnel?from&to` (ISO, UTC; default the last 30 days; `from >= to` is 400 `Validation`)
  → `{ from, to, measuredAt, started, steps: [{ step, users }], returns: { activeOnTwoOrMoreDays,
  activeSevenOrMoreDaysAfterStart, activeDaysP50, activeDaysMax } }`.
- **Cohort:** owners whose career profile was created in `[from, to)`. `started` is the cohort size.
- **Steps** (journey order; each counts cohort owners who have reached it by `measuredAt`):

| `step` | Source |
| --- | --- |
| `profile_saved` | `CareerProfiles` (the cohort itself) |
| `goal_saved` | `CareerGoals` |
| `occupation_confirmed` | any `CareerGoalVersions` row with an occupation code |
| `market_brief` / `pay_analysis` | `CareerMarketBriefs` / `CareerPayAnalyses` |
| `roadmap_built` / `roadmap_accepted` | `CareerRoadmaps` (any / status `accepted`) |
| `resume_drafted` | `CareerMaterials` of kind `resume` |
| `material_exported` | `CareerUsageEvents` action `export`, outcome `ok` (export files expire; the ledger row stays) |
| `photo_selected` | `CareerPhotoSelections` |
| `photo_package_bought` | paid `UserPackageEntitlements` (has a payment, not refunded or revoked) created on or after the owner started career |

- **Returns:** an active day is a UTC day with a saved change, a draft or an export (profile/goal/material versions,
  agent runs, usage events, briefs, pay analyses, roadmaps, roadmap progress, photo selection). The start day counts.
  `activeSevenOrMoreDaysAfterStart` counts owners active again at least 7 days after their profile was created.

## Why server-side, not browser events

GA4 (`AnalyticsService`) only runs after the visitor accepts analytics cookies, so a browser-event funnel would miss
everyone who declines. Every step above is already a saved record, so the server can count all users without new
tracking. GA4 still records page views on `/app/career/*` for consenting visitors.

## Limits

- Counts only; no owner ids or hashes. Small cohorts can still be recognisable to an admin; read them as such.
- Deleted career data drops out of every row (deletion wins). A roadmap accepted and later dismissed no longer counts.
- Visits that only read are not recorded, so `returns` understates people who come back just to look.
- Market comparisons are not stored, so they have no step.
