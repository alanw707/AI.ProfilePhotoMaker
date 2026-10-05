# ADR 0014: Market comparison is one normalized result rendered twice

Status: accepted (2026-10-05, ticket #385, spec #376). Uses the pinned BLS snapshot (ADR 0011) and the goal's confirmed occupation (ADR 0010).

## Context

#385 asks the user to explore national, state and metro evidence, compare up to three compatible places, and explicitly save a location preference to the goal. Two risks: a visual map drifting from the table beside it, and implying coverage or precision the public data does not have.

## Decision

- **One result, two renderings.** The API returns a single normalized comparison (`areaCode`, `areaTitle`, `type`, `value`, `status`, `rank`) and the page renders both the heatmap and the table from exactly that array. Parity is a tested invariant, not a design hope: a test asserts every table row's value equals the corresponding heatmap cell's value for the same area.
- **Metrics.** Supported: `median_wage` (median annual wage), `employment` (employment stock), `location_quotient` (employment concentration relative to the nation, which is BLS's own concentration measure) and `share_of_national_employment` (this area's share of the occupation's national employment). Unsupported metrics are returned in the payload with `supported: false` and a reason, and the UI renders them disabled with that explanation rather than hiding or faking them. In particular **projections are national-only** in the pinned snapshot, so a per-area projection metric is `supported: false` with reason `national_only_source` — the snapshot is not interpolated to invent state projections.
- **Geography.** States (50 + DC) and metropolitan statistical areas from the snapshot, including Alaska and Hawaii; the payload carries the source's own coverage text and the release, and the page shows it. No territories, no nonmetropolitan areas: that is disclosed, not silently absent. State and metro figures are never summed or mixed into a combined number — comparisons are per-place values only, because a metro sits inside a state and summing them would double count.
- **Missing, suppressed and low are distinct.** `status` is `available`, `not_available` (BLS `*`/`**`, too few responses or withheld), `top_coded` (BLS `#`, at or above the published ceiling) or `not_published` (no row). Missing cells are rendered with a distinct pattern and the legend says so, and they are never ranked as if they were the lowest values; ranks are assigned only among `available` values (top-coded values rank with their ceiling and are marked).
- **Legend and units.** The payload states the metric's unit, measure, reference period and publication date, and the page's legend renders them, so a colour is never the only carrier of meaning: the table carries the numbers, the heatmap carries the pattern, and both carry text labels.
- **Mobile is a searchable list.** Below the table breakpoint the page defaults to a searchable, keyboard-navigable list of places (same values), so the tile grid is never the only way in.
- **Comparison and saving.** Up to three places can be selected; the selection lives in query parameters (`metric`, `areas`) so a reload or a shared link keeps the filters. Saving a location preference writes a new goal version through the existing `If-Match` precondition, requires an explicit confirmation step in the UI, and is refused with 412 when the goal moved on. Nothing is saved implicitly by viewing or selecting.
- **Privacy and determinism.** The comparison is computed from the public snapshot and the goal's occupation; no profile text, photo, demographic or contact field reaches it, and no model is called. No new private entity is introduced, so no retention change is needed.

## Consequences

- A future geometry source (real boundaries) can replace the tile grid without changing the API, because the table is already the equivalent rendering.
- New metrics arrive as snapshot fields plus a supported/unsupported declaration; the disabled-with-explanation rule keeps the UI honest in the meantime.
