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

## /code-review (Standards + Spec)

| # | Axis | Severity | Finding | Status |
|---|---|---|---|---|
| R1 | Spec | P1 | "Render Analytics and report detail": no Analytics surface named. | Fixed: `/app/career/market` is the analytics view (alias `/app/career/analytics`, home link "Analytics: market brief"); recent briefs + national/local comparison; ADR 0011 records that multi-market comparison is #385. Playwright covers the alias. |
| R2 | Standards | P2 | Annual-only / hourly-only occupations showed the missing wage as "too few survey responses". | Fixed: pay basis packed from the snapshot; wages outside it are `not_published`. Tests on teachers (25-2021) and actors (27-2011). |
| R3 | Standards | P2 | Common city spellings (New York City, Louisville, Saint Louis, Honolulu, Boise, D.C.) fell back to state. | Fixed: city normalisation; 10 new resolver cases. |
| R4 | Spec | P2 | As-of date and coverage only inside the drawer; state fallback not labelled. | Fixed: each section shows "As of … (published …). Coverage: …"; local column reads "<state>, state figures" on fallback. |
| R5 | Spec | P2 | Retention not stated to the user. | Fixed: retention note under the brief. |
| R6 | Spec | P2 | A fully failed run had no recovery action. | Fixed: "Try again" on a failed run (Playwright). |
| R7 | Both | P3 | dataStale copy hard-coded "a year and a half" despite the setting. | Fixed: neutral copy. |
| R8 | Standards | P3 | Rapid brief switching could show an older response. | Fixed: brief loading uses `switchMap`. |
| R9 | Standards | P3 | Corrupt stored JSON → 500; replayed null step stalls until lease expiry; first snapshot load on a request thread; free runs still need allowance headroom. | Accepted for this slice (rows written only by the runner; consistent with occupation_match). |
| R10 | Spec | P3 | Export of briefs. | Owned by #392. |
| R11 | Both | P1 | (auditor) Shared published SOC estimates were marked exact and undisclosed, including related occupations; the Related occupations section had no as-of/coverage line. | Fixed: crosswalk gains a `shared` type for every O*NET code whose published SOC estimate covers other O*NET occupations (178 in each source, verified: no code missing or wrongly marked); mapping notes name the published code and title in the main sections and beside each related occupation; related as-of/coverage cites both item sources. Builder and Playwright tests cover 15-1299.08. |

## Live-review rerun after R11 (same script, real stack)

| Check | Result |
|---|---|
| Primary wages/employment figures vs the raw snapshot | 28 checked, 0 mismatches |
| Related-occupation figures vs the snapshot row of the code they were published under | 6 checked, 0 mismatches |
| Shared-mapping disclosure beside related occupations | 15-1299.08 and 15-1299.02 both name 15-1299; exact mappings show no note |
| Sections with an "As of … Coverage" line | 4 of 4 (Related occupations now included) |
| Screenshots | `docs/testing/career/382` refreshed at desktop 1280, mobile 390 and 320 |
| Overflow / axe WCAG 2.2 AA / page errors | 0 / 0 / 0 |

## /code-review of the R11 fix (focused, PR #409)

| # | Severity | Finding | Status |
|---|---|---|---|
| R12 | P2 | A related occupation showed figures from both sources but could disclose only one mapping (wage note preferred), so a future release mapping the sources differently would under-disclose. | Fixed: one disclosure per distinct published code, in figure order; test with a synthetic mixed mapping (`MixedMappingReference`). |
| R13 | P3 | The client `MarketAlternative` type lacked `note`; the component used a cast. | Fixed: `note: string \| null` on the interface, cast removed. |
| R14 | P3 | ADR said the loader verifies "the presence of both sources" while it validates each independently. | Fixed: ADR wording corrected. |

Open P0/P1: **none**.
