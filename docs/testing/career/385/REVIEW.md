# #385 market comparison: live-stack review

Simulated review on the LocalDev stack (in-memory DB, career flag on, real BLS OEWS May 2025 snapshot), not target-user evidence. No API mocks. Synthetic, fictional profiles. Script: `AI.ProfilePhotoMaker.UI/tests/ux/career-markets-review.mjs`; raw results in `checks.json`; screenshots at desktop 1280, mobile 390 and 320.

## Results

| Check | Result |
|---|---|
| States + nation | 51 state areas and the national reference; 1 area has no rankable figure (BLS suppressed it) |
| Values vs the raw snapshot | 50 available states checked against `bls-snapshot.json.gz`, 0 mismatches (Colorado $138,390; national $135,980) |
| Heatmap/table parity | 51 heatmap cells and 51 table rows, **0 value differences** — both render the same bounded payload |
| Coverage disclosure | the page names the source, measure, May 2025 release (published 2026-05-15) and the snapshot's coverage text, and says these are benchmarks, not job openings |
| Metro level | 393 metro areas including Alaska (2) and Hawaii (2: Kahului-Wailuku, Urban Honolulu); no nonmetropolitan areas included |
| Unsupported metric | `projected_change` is a **disabled** radio with `aria-describedby` and the reason "Only national projections are published, so this metric is not available per market." — never selectable |
| Suppressed / missing cells | rendered with a distinct hatch and the legend says they are "never treated as low values"; they are excluded from ranks |
| Legend | unit, measure, reference period, publication date, colour scale and pattern meaning all stated in text |
| Filters | `metric`/`level`/`q` live in query params: after a reload the search box still held "colo" and the table still showed only Colorado |
| Keyboard | focusing a tile and pressing Enter selected it (the row checkbox became checked) |
| Save a location | explicit confirmation dialog first, then the goal went from version 2 to 3 with `preferredArea` = Colorado (state) and "Saved to your career goal." |
| No confirmed occupation | 409 `CareerOccupationRequired` |
| Expired session | `/auth/login?…&returnUrl=/app/career/markets` |
| Mobile | at 390 and 320 px the tile grid is replaced by a searchable list of 51 areas (same values), the table sits behind "Show as a table", and there is no horizontal overflow |
| axe WCAG 2.2 AA | 0 violations on every measured screen (1280/390/320, default and filtered states) |
| Page errors | 0 |

API fixtures (`MarketComparisonServiceTests`, `CareerMarketComparisonApiTests`) additionally cover rank determinism and tie-breaking, top-coded ceilings, the share arithmetic, the 500-area cap, preference validation (400/404/409/412/428), carry-forward through a goal edit and a restore, cross-user isolation, flag-off and the migration's additive shape.

## Findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | — | Nothing outstanding: the slice meets the parity, coverage, disabled-metric, missing-value, keyboard/mobile, filter-persistence and confirmation requirements, verified live as well as in fixtures. | — |
| 2 | P3 | My first two review probes reported the unsupported metric as not disabled and the mobile list as empty; both were wrong selectors in the review script, not page defects (the control is `[data-metrics] input[disabled]` with `[data-metric-reason]`, and the list is `[data-list] li[data-area]`). | Fixed the probes and re-ran, so the recorded evidence matches the page. |
| 3 | P3 | Rank is computed before the search filter, so a filtered row keeps its true national rank rather than re-ranking the filtered set. | Accepted and intended: a rank that changed as you typed would misrepresent where the area really stands. |

Open P0/P1: **none**.
