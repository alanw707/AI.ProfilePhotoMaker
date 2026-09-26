# Comparable-pay source and calculation qualification

Status: **benchmark-first release decision; production personalized pay deferred**. Read-only checks on 2026-09-23 UTC. This report records the source-qualification work for [Career ticket #383](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/383). No credential, paid service, contract, posting feed, or user data was acquired.

## Decision

No evaluated source currently clears all four gates for the proposed U.S. career product: permitted ongoing use and retention, broad U.S. occupation/geography coverage, explicit comparable pay/level/arrangement attributes, and enough independent employers for a stable cohort. The first release must not display the occupational wage benchmark as an individualized pay range. A dated benchmark can still be developed and shown under its own label.

**Owner decision, 2026-09-23:** use the free, separately labeled BLS occupational benchmark for the first career release; do not create an Adzuna account or buy a feed for this release. Personalized advertised-pay analysis is deferred, not replaced by the benchmark. This resolves the product choice but does not prove the original ticket's real covered-cohort criterion or finish benchmark ingestion. Those remain explicit implementation/evidence gates; the existing ticket should not be closed on the strength of this decision alone.

For that fallback, BLS OEWS is the qualified **source category**, not a completed product feed. A display should say “Occupational wage benchmark,” name the SOC occupation and metropolitan/statistical area, the May 2025 reference period, the actual published annual P25–P75 values if available, a retrieval date and suppression/unavailable state. BLS percentiles describe workers in the occupation, not currently advertised jobs or this user's likely offer. Do not combine occupation percentiles by averaging them, infer a level from years of experience, or imply remote-state eligibility. The prototype's fictional dollar values remain fictional until a separately verified BLS ingestion replaces them. If the requested occupation/area or percentile is suppressed or unavailable, show an unavailable state; do not interpolate a different occupation or geography into a personal estimate.

Adzuna is the most plausible national feed to qualify next, because its documented search API exposes U.S. listings and pay fields; **selection for production is conditional** on a key, direct U.S. data sampling, negotiated ongoing aggregation/storage rights, attribution, price and measured cohort coverage. Its public terms explicitly limit non-permitted ongoing aggregated use after a 14-day trial. The project owner confirmed on 2026-09-23 that no Adzuna account exists yet. There is no approved provider or quote at this point; no account or terms have been accepted on the owner's behalf.

## Candidate assessment

| Source | Accessible evidence | Useful fields / gaps | Ongoing rights, retention and price | Verdict |
| --- | --- | --- | --- | --- |
| [NYC Open Data Jobs NYC Postings](https://data.cityofnewyork.us/City-Government/Jobs-NYC-Postings/kpav-sd4t/about_data) | Public dataset sampled live without key; NYC [FAQ says open data has no use restrictions](https://www.nyc.gov/opendata/get-started/FAQs). | Job ID, agency, title, career level, salary range/frequency, workplace text and posting date exist. No reliable remote eligibility or independent national employer sample. | Open data portal; no fee or private credentials encountered in read-only sample. Check dataset updates and attribution before any product use. | **Methods fixture only**. One municipal employer in one city; cannot represent the national market or meet five-employer floor. |
| [Adzuna API](https://developer.adzuna.com/docs/search) | Developer docs and [terms](https://developer.adzuna.com/docs/terms_of_service) reviewed. Key required; no user account/key supplied, so no live sample or coverage claim. | Search example includes salaries and locations; explicit level, arrangement, pay provenance, employer diversity and deduplication quality remain unverified. | Public default rate limits: 25/min, 250/day, 1,000/week, 2,500/month. Non-listed commercial uses have 14-day validation trial; ongoing aggregate use needs written consent; attribution required. Actual license, cache/retention and price require provider response. | **Preferred qualification candidate, not approved**. Stop before production integration. |
| [USAJOBS API](https://developer.usajobs.gov/api-reference/) | [Key application and terms](https://developer.usajobs.gov/apirequest/index) reviewed; no key requested. | Federal posts include salary/location and structured series, but represent the federal sector; not a general private-sector market. Remote/location fields and pay basis require sampling. | API key/application required. Terms permit internal normalization/deduplication with displayed values unchanged, attribution and return links; stand-alone redistribution prohibited. Price not published in reviewed docs. | **Supplemental federal opportunities only**, pending key and field review. |
| [Greenhouse Job Board API](https://docs.greenhouse.io/job-board.html) | Public GET documented without authentication. | IDs, updated time, employer board and location; [pay transparency fields](https://support.greenhouse.io/hc/en-us/articles/10028084062491-Add-pay-transparency-to-a-job-post) depend on each employer's configuration. Remote rules and seniority lack universal normalized fields. | Docs establish public read access for employer career pages, not a blanket right to aggregate many employers into a separate salary product. Commercial reuse/retention and cost are unverified. | **Potential supplemental direct-link source only** after explicit rights review; no bulk aggregation assumed. |
| [BLS OEWS May 2025 tables](https://www.bls.gov/oes/tables.htm) | Official national, state and metro occupational wage estimates, including published percentiles; released in 2026. | Occupation and geography support a benchmark, not current posting, employer, level, remote eligibility or individual-offer attributes. [BLS describes annualized percentile method and its limits](https://www.bls.gov/opub/hom/oews/calculation.htm). | [BLS-published material is public domain](https://www.bls.gov/opub/copyright-information.htm) with requested source citation; no paid key. Its [service terms](https://www.bls.gov/developers/termsOfService.htm) require access-date citation and disclaimer for API-derived analysis. | **Qualified benchmark source only**, pending file ingestion and field/suppression QA. Never a personalized advertised-pay cohort. |

These are source qualification judgments, not legal opinions or negotiated rights. Public access does not prove an ongoing data product is authorized.

## Live public-data sample

The read-only sampler at [sample-nyc.mjs](sample-nyc.mjs) fetched a bounded selection of external NYC postings through the official public API, requesting only fields needed for method evaluation. On **2026-09-23 11:10 UTC**, it returned **1,451 rows** under a 5,000-row cap. It retained no raw job text or salary records. The borough parser recognized Manhattan, Bronx, Queens, Brooklyn and Staten Island in some workplace strings; this is heuristic location parsing, not a validated geographic crosswalk.

| Broad family / example borough | Candidate rows | Annual salary rows | Distinct city agencies | Independent employers | Output |
| --- | ---: | ---: | ---: | ---: | --- |
| Software / Manhattan | 36 | 35 | 8 | 1 | Benchmark fallback |
| Software / Bronx | 9 | 9 | 1 | 1 | Benchmark fallback |
| Healthcare / Manhattan | 14 | 14 | 4 | 1 | Benchmark fallback |
| Healthcare / Brooklyn | 13 | 11 | 2 | 1 | Benchmark fallback |
| Operations / Manhattan | 11 | 10 | 6 | 1 | Benchmark fallback |
| Operations / Brooklyn | 9 | 8 | 6 | 1 | Benchmark fallback |

The title/category matching is a deliberately broad diagnostic heuristic, not an occupation-code classifier. Multiple NYC agencies do **not** satisfy the independent-employer requirement: the employer is the City of New York. Borough from workplace text cannot establish remote eligibility. The source does not provide an explicit remote/work-arrangement field in the requested schema, so the sampler records eligibility as unknown and excludes all rows from a personalized cohort. Posting date is not necessarily last verified date. These failures are informative results, not missing implementation exceptions.

The sampler prints per-family/borough counts, exclusion outcomes and two shortened SHA-256 hashes of public job IDs, with no raw salary rows. Re-run with `node research/comparable-pay/sample-nyc.mjs` from the worktree root. Live counts will change and should be treated as a dated sample, not a stable fixture.

## Cross-employer public-board probe

A second ephemeral research probe at [sample-greenhouse.mjs](sample-greenhouse.mjs) read 14 publicly accessible Greenhouse employer boards on **2026-09-23 11:24 UTC**. The script selects a few broad role families and three named metros, extracts at most one plausible annual USD pay interval from each posting's public text, deduplicates by employer/job ID, and prints counts only. It stores no posting text, job rows or actual observed pay values. This activity tests source feasibility; Greenhouse's documented public GET access does **not** establish permission for a commercial salary aggregation product.

| Role / metro | Matching posts | Posts with one plausible annual range | Included after recency/dedup | Employers with included pay | Candidate decision |
| --- | ---: | ---: | ---: | ---: | --- |
| Software / San Francisco | 96 | 20 | 19 | 3 | Fallback: below five-employer floor |
| Software / New York | 37 | 5 | 5 | 3 | Fallback |
| Software / Seattle | 17 | 5 | 5 | 1 | Fallback |
| Product design / San Francisco | 15 | 5 | 5 | 2 | Fallback |
| Operations / San Francisco | 17 | 6 | 6 | 3 | Fallback |
| Operations / New York | 6 | 1 | 1 | 1 | Fallback |

The remaining sampled family/metro cells also fell back; several had no parseable pay. This independently tests three role families across San Francisco, New York and Seattle and demonstrates that even a 96-post software candidate pool does not meet the proposed five-employer floor after pay and freshness filtering. The source may contain more valid pay in structured or differently formatted fields; the conservative text parser is **not** a production extractor. Cross-board posting terms, exact work arrangements, seniority and role-code mapping remain unverified. No positive covered cohort from real postings was established.

The probe reinforces the stop decision: do not relax the 10-observation/five-employer floor merely to display a number. To meet the first-release personalized requirement, obtain a source with rights and enough well-structured observed pay across employers and regions, then run the same covered/sparse evaluation on a permitted retained snapshot.

### Structured pay-transparency field check

The [Greenhouse Job Board API documentation](https://docs.greenhouse.io/job-board.html) also exposes `pay_input_ranges` through an individual public job GET with `pay_transparency=true`. The first text-only probe did not inspect that field. A bounded follow-up on **2026-09-23 11:56 UTC** ran [probe-greenhouse-structured.mjs](probe-greenhouse-structured.mjs) against the first eight matching software/San Francisco jobs per each of the same 14 boards. Five boards returned at least one structured USD min/max range: Airbnb 2, Databricks 8, Reddit 1, Figma 2 and Coinbase 1, **14 sampled posts total**. Asana and Stripe returned no structured ranges for their sampled posts; Discord had two detail errors; Gusto timed out. These are counts, not retained pay amounts or a representative coverage estimate.

This improves the field-feasibility picture but does **not** create a qualified covered cohort. The documented structured range object contains amount and currency but no normalized annual/hourly basis. A second run at **11:58 UTC** found all 14 sampled USD ranges attached to posts updated within 90 days, but free-text range title/blurb signaled annual pay for only eight, all from **one employer**; three signaled hourly pay without known annual hours and three supplied no basis signal. Those signals are diagnostic text, not a validated normalized basis. Level, employment type, cross-post deduplication and remote restrictions also remain unresolved. The 14 results cannot be merged mechanically with the text probe's 19 because they may be the same requisitions. Public GET access still does not establish a license for a commercial cross-employer salary product. A production adapter would need to verify those fields and rights before evaluating the 10/5 rule.

## Candidate calculation rule, version 1.0

The deterministic [cohort.mjs](cohort.mjs) and [synthetic fixtures](fixtures.mjs) define the proposed starting rule. This rule is **validated as code behavior**, not validated as an estimator of real offers.

1. Include only employer-disclosed USD annual pay, or hourly pay with explicit annual hours. Exclude unknown basis, unknown/ineligible work-location status, malformed ranges, stale observations, nonmatching occupation family/geography and unconfirmed level or employment type when those filters are requested.
2. Deduplicate by canonical requisition ID. A production adapter must additionally reconcile reposts and multi-location IDs; this fixture does not prove that ability.
3. Use a 90-day observation lookback and a starting floor of **10 independent observations from at least 5 employers**. A concentration report names the largest employer's share. Recalibrate the floor with real source coverage; do not lower it to create a number.
4. When supported, calculate the **25th percentile of annualized lower bounds** and **75th percentile of annualized upper bounds**, using linear interpolation at position `(n−1)×p`, rounded to whole USD. Show included/excluded counts, lookback and calculation version. It is an observed advertised-pay interval, not a confidence interval, base-salary guarantee or individual's worth.
5. When the floor is not met, return `occupational benchmark fallback` with a null personalized interval. The benchmark must carry its own source, measure and period and is never silently substituted as individualized pay.

Three synthetic 12-observation cohorts across software, operations and nursing in Denver and Seattle pass the starting floor with six fictional employers apiece. Expected lower/upper interval outputs are respectively **$110,300–$195,200**, **$84,300–$156,200**, and **$97,300–$162,200**, all explicitly synthetic. The five focused Node tests verify interpolation, normalization, exclusions, duplicates and sparse fallback. No model-generated salary observations or years-of-experience multiplier are used.

### Sensitivity and exclusions

| Scenario | Effect |
| --- | --- |
| 12 observations / 6 employers | Candidate interval available in synthetic fixture; maximum employer share 2/12. |
| 7 observations / 6 employers | Fallback under the 10-observation floor. |
| 12 observations / 1 employer | Fallback under the five-employer floor. |
| Unknown remote eligibility | Observation excluded, even if workplace city is known. |
| Hourly pay with explicit 2,080 annual hours | Eligible for normalization; hours must be supplied by source or a disclosed user assumption, never guessed. |
| Salary model rather than employer-disclosed range | Excluded from personalized cohort; may be shown separately only with its own evidence/terms. |
| Duplicate requisition, stale date, incompatible currency or pay basis | Excluded with reason count. |

The broad source sample cannot calibrate the 10/5 floor, level matching or advertised interval against actual offer outcomes. Nor does the 25th/75th bound rule have a measured calibration error yet. This is a reproducible candidate, not a final compensation model.

## Decision gates before #384 integration

1. Obtain authorized Adzuna API access for a bounded U.S. sample and written terms allowing this product's ongoing aggregation, display, retention/deletion and attribution. Record quote, rate limits and monthly cost at forecast usage. Do not sign or purchase on this ticket.
2. Reproduce covered and sparse cases in at least three occupation families and multiple distinct U.S. metros/states with actual employer diversity, explicit pay/level/geography/arrangement and valid refresh/expiry metadata. Sample independent employers, not agency labels.
3. Quantify how many users would get a covered personalized range versus a fallback. Measure duplicate/repost rate, employer concentration, unavailable pay, false remote eligibility and stale rows.
4. If source fields or terms fail, test an alternative provider through the same gate. Do not market the benchmark as personalized pay. Ticket #384 remains blocked by the evidence gate regardless of GitHub's dependency link.
5. Review the calculation against actual observations and a small holdout of user-reported offer ranges where consent and coverage exist. Version any change to the floor or percentile rule and update the fixtures.

## Verification record

`node --test research/comparable-pay/cohort.test.mjs` passed 5/5 focused tests on 2026-09-23. The live sample returned valid official API data and produced fallback for all tested family/borough cohorts. Provider pricing, national coverage and production license are **unverified**; this negative finding is the selected-provider decision for now.
