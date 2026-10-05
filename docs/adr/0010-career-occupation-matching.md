# ADR 0010: Occupation matches come from duty evidence against a pinned O*NET snapshot

Status: accepted (2026-10-05, ticket #381, spec #376)

## Context

#381 suggests a small set of occupations from the user's confirmed responsibilities, explains each with evidence, asks when the result is ambiguous and lets the user confirm one into the goal. Later market, pay and job reports (#382–#386) key off the confirmed occupation code, so the match must be reproducible, attributable and never decided by a job title alone.

## Decision

- **Reference snapshot.** `AI.ProfilePhotoMaker.API/Data/Reference/onet-snapshot.json.gz` is built by `scripts/career/build-onet-snapshot.py` from the O*NET 30.0 text database (August 2025, O*NET-SOC 2019 taxonomy, CC BY 4.0). It keeps, per occupation with core tasks (893): code, title, description, up to 12 core task statements (with O*NET task ids), up to 10 skills with importance ≥ 3.0, up to 12 technology examples (hot technologies first) and up to 25 reported/alternate titles. The snapshot carries its source name, release, taxonomy, URL, SHA-256 of the source zip, licence and the attribution text; the build is deterministic and the loader verifies the expected snapshot SHA-256 at startup, failing closed (`CareerReferenceUnavailable`, 503) on a mismatch or a malformed file. Codes are validated against `^\d{2}-\d{4}\.\d{2}$`. The UI shows the attribution wherever matches appear.
- **Deterministic matcher, no model.** Matching is a tool, not a model call, so results are reproducible from (profile version, snapshot release, matcher version). Input is an `OccupationMatchInput` built only from current title, industry, summary, skills and highlights; location, work arrangement, pay, goal pay, contact details and anything photo-related are not in the type and cannot influence the result. Duties are the highlights plus summary sentences. Each duty is compared with every core task by IDF-weighted overlap of normalised word stems (stop words removed); a duty counts as evidence for a task with at least two shared content stems and a weighted overlap above a fixed threshold. Skills count when they equal an O*NET skill or technology example after normalisation. A title match adds a small bonus **only to occupations that already have duty evidence**, so a title alone never produces a candidate.
- **Result.** Up to five candidates ordered by evidence, each with a strength label (`strong`, `moderate`, `weak`, from the count and weight of duty evidence; no percentages and no profile-completeness score) and:
  - `evidence`: pairs of the user's fact (field, index, text) and the O*NET task or skill it supports;
  - `missingEvidence`: core tasks of the occupation the profile does not describe (top 5);
  - `knownGaps`: the occupation's important skills/technologies with no match in the profile (top 5);
  - `unsupportedSkills`: the user's skills that do not support this occupation.
  If no occupation has at least one duty evidence item plus a second evidence item, the result is `unsupported` with guidance to describe responsibilities; nothing can be confirmed.
- **Ambiguity pauses the run.** The run task `occupation_match` (ADR 0009 runtime) reads the profile and goal, runs the matcher and saves the result as a step. When the top two candidates are in different SOC major groups and the runner-up's evidence weight is at least 80 % of the leader's (contradictory duties), the run asks "Which of these is closest to the work you want analysed?" with the candidates as choices plus "None of these", and waits in `needs_input`. The answer reorders (or, for "none", marks the result `unsupported`); it never invents a candidate.
- **No allowance, no model required.** The task makes no model call, so its reserved unit is always released and it runs even where no text model is registered.
- **Confirmation into the goal.** `CareerOccupationMatch` (one per run, owner-scoped, pinned profile/goal versions and snapshot release) is `proposed` until the user confirms one candidate with the goal's current `If-Match`. Confirmation creates a new goal version carrying `OccupationCode`, `OccupationTitle`, `OccupationReferenceRelease` and `OccupationMatchId` with `Source = "occupation_match"`. Later manual goal edits carry these fields forward. Stale goal → 412; profile changed since the match → 409 `CareerMatchStale` (start a new match); a code that is not a candidate, an unsupported result or an already decided match → 409; another owner's match → 404; no goal → 409 `CareerGoalRequired`.
- **Privacy.** Matches carry `OwnerId`, cascade on account deletion, join `CareerPrivateDataService` deletion coverage and follow the run retention rule (kept while the account exists; export with #392).

## Consequences

- Matching quality is measured on synthetic fixtures for at least three occupation families, nonstandard titles, contradictory duties and unsupported profiles; a better matcher or a model-assisted explainer can replace this one behind the same result shape, with a new matcher version.
- Updating to a new O*NET release means re-running the script, updating the expected SHA-256 and re-running the fixture suite; confirmed goals keep the release they were matched against.
