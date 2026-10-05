# #382 market brief: live-stack review

Simulated review on the LocalDev stack (in-memory DB, career flag on, real BLS OEWS May 2025 + Employment Projections 2025–35 snapshot), not target-user evidence. No API mocks. Synthetic, fictional profiles. Script: `AI.ProfilePhotoMaker.UI/tests/ux/career-market-review.mjs`; raw results in `checks.json`; screenshots at desktop 1280, mobile 390 and 320.

## Results

| Check | Result |
|---|---|
| Start without a confirmed occupation | 409 `CareerOccupationRequired` |
| Software developer (15-1252.00), goal "Denver, CO" | resolved to metro 19740 Denver-Aurora-Centennial, CO; brief `complete`; sections wages/employment/outlook/alternatives all complete |
| Every wage and employment figure vs the raw snapshot (read independently of the API) | 28 checked, 0 mismatches |
| Spot values on the page | national median $135,980; Denver median $137,610; difference +$1,630 |
| Outlook | employment 2025/2035, change 10.2 %, 95.3 thousand annual openings, typical education: all equal the projections table |
| Sources | OEWS 2025-05 (published 2026-05-15) and projections 2025-2035 (published 2026-08-27), with definitions, coverage, licence and citation |
| Allowance | not spent (used 0, reserved 0); no model needed |
| Source drawer | opens with the citation, closes with Escape |
| Another owner's brief | 404 |
| Reload, then profile changed | brief reopens from `?brief=`; afterwards `stale: true` (`profile_changed`), content unchanged, banner shown |
| Unresolved location ("Atlantis, ZZ") | "We could not place" text; wages/employment `unavailable` (`location_unresolved`); next action "Add a city and state to your goal" |
| Expired session | `/auth/login?…&returnUrl=/app/career/market` |
| Overflow, axe WCAG 2.2 AA, page errors | 0 / 0 / 0 on every screen |

API fixtures (`MarketBriefBuilderTests`, `MarketReferenceTests`, `MarketAreaResolverTests`, `CareerMarketBriefApiTests`) cover every figure against the snapshot, top-coded and suppressed cells, broad and missing crosswalks, partial failure (projections unavailable keeps wages and employment), tampered/wrong-licence snapshots, staleness reasons, data ageing and owner deletion. Playwright checks every `[data-figure]` against the formatter.

## Findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | P2 | Source drawer rendered as a box pinned to the top-left corner (a global reset removed the dialog's auto margin), covering the page header. | Fixed: full-height right-hand drawer that scrolls inside; review asserts its position and citation. |

Open P0/P1: **none**.
