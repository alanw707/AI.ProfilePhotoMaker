# Career-agent implementation plan

Status: **complete and approved for development planning**, 2026-09-22. The user approved the 20-ticket breakdown, blocking relationships, testing seams and GitHub publication. All app capabilities below remain implementation work; this planning task did not build or deploy them.

- [Published specification #376](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376)
- [Local specification](specification.md)
- [Approved product scope](../career-agent-pivot-plan-2026-09-22.md)
- [Approved Impeccable six-page UX blueprint](../career-agent-ux-blueprint-2026-09-22.md)

## Start here

Two independent tickets are immediately available: **01, connected Impeccable prototype**, and **07, comparable-pay source qualification**. Start with the prototype for UX priority; qualification can run in parallel. Before code changes, establish an agreed isolated branch/baseline and preserve the current dirty worktree. Do not treat existing uncommitted edits as disposable or silently include them.

Work the dependency frontier: an issue is implementable only after its blockers and its own explicit decision gates are satisfied. The `ready-for-agent` label describes actionable specification quality, not absence of blockers. GitHub holds the authoritative issue state.

## Ticket graph and deliverables

| Ticket | GitHub / local brief | Blocked by | Demonstrable result |
| --- | --- | --- | --- |
| 01 | [#377: Validate the connected six-page career prototype](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/377) · [brief](tickets/01-validate-the-connected-six-page-career-prototype.md) | None | Connected, reviewed six-page prototype. |
| 02 | [#378: Save a manual career profile and goal behind the career flag](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/378) · [brief](tickets/02-save-a-manual-career-profile-and-goal-behind-the-career-flag.md) | 01 | Confirmed manual facts and goal survive reload. |
| 03 | [#379: Import a private resume and review extracted profile changes](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/379) · [brief](tickets/03-import-a-private-resume-and-review-extracted-profile-changes.md) | 02 | Private resume becomes reviewable facts. |
| 04 | [#380: Run one bounded career-agent task with durable recovery and quotas](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/380) · [brief](tickets/04-run-one-bounded-career-agent-task-with-durable-recovery-and-quotas.md) | 02 | Bounded summary task survives cancel/reconnect. |
| 05 | [#381: Confirm evidence-based occupation matches](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/381) · [brief](tickets/05-confirm-evidence-based-occupation-matches.md) | 04 | User-confirmed duty-based occupation match. |
| 06 | [#382: Produce a sourced national and local career market brief](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/382) · [brief](tickets/06-produce-a-sourced-national-and-local-career-market-brief.md) | 05 | Dated, attributable national/local career brief. |
| 07 | [#383: Qualify licensed comparable-pay evidence and calculation rules](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/383) · [brief](tickets/07-qualify-licensed-comparable-pay-evidence-and-calculation-rules.md) | None | Licensed evidence and reproducible cohort method qualified. |
| 08 | [#384: Show reproducible personalized comparable-pay analysis](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/384) · [brief](tickets/08-show-reproducible-personalized-comparable-pay-analysis.md) | 06, 07 | Personalized interval with honest sparse fallback. |
| 09 | [#385: Compare U.S. markets through an accessible heatmap and table](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/385) · [brief](tickets/09-compare-u-s-markets-through-an-accessible-heatmap-and-table.md) | 06 | Accessible national map/table and comparisons. |
| 10 | [#386: Browse eligible job observations with honest coverage](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/386) · [brief](tickets/10-browse-eligible-job-observations-with-honest-coverage.md) | 07, 09 | Eligible observed postings with real restrictions. |
| 11 | [#387: Create a target-specific career roadmap](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/387) · [brief](tickets/11-create-a-target-specific-career-roadmap.md) | 08 | Accepted evidence-backed roadmap. |
| 12 | [#388: Track roadmap work and review replanning changes](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/388) · [brief](tickets/12-track-roadmap-work-and-review-replanning-changes.md) | 11 | Progress and reviewed replanning preserve edits. |
| 13 | [#389: Draft and edit a fact-grounded targeted resume](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/389) · [brief](tickets/13-draft-and-edit-a-fact-grounded-targeted-resume.md) | 08 | Fact-grounded targeted resume with version history. |
| 14 | [#390: Prepare professional summaries and free document exports](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/390) · [brief](tickets/14-prepare-professional-summaries-and-free-document-exports.md) | 13 | Summary and usable free PDF/DOCX exports. |
| 15 | [#391: Connect optional photos without changing purchased rights](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/391) · [brief](tickets/15-connect-optional-photos-without-changing-purchased-rights.md) | 02 | Optional photo round trip preserves purchased rights. |
| 16 | [#392: Provide career privacy export, deletion and retention controls](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/392) · [brief](tickets/16-provide-career-privacy-export-deletion-and-retention-controls.md) | 03 | Career export/purge and retention controls. |
| 17 | [#393: Connect the complete contextual career-agent journey](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/393) · [brief](tickets/17-connect-the-complete-contextual-career-agent-journey.md) | 10, 12, 14, 15 | All six pages form one continuous agent-led journey. |
| 18 | [#394: Prepare the existing homepage for the free career core](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/394) · [brief](tickets/18-prepare-the-existing-homepage-for-the-free-career-core.md) | 01 | Truthful public pivot behind its own flag. |
| 19 | [#395: Calibrate sustainable free usage and operator controls](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/395) · [brief](tickets/19-calibrate-sustainable-free-usage-and-operator-controls.md) | 17 | Measured and approved free-use policy. |
| 20 | [#396: Verify release readiness, baseline parity and reversible rollout](https://github.com/alanw707/AI.ProfilePhotoMaker/issues/396) · [brief](tickets/20-verify-release-readiness-baseline-parity-and-reversible-rollout.md) | 16, 18, 19 | Evidence-backed GO/NO-GO and rollback runbook. |

Native GitHub `blocked_by` relationships mirror these 25 edges. Creation used the [documented GitHub dependency API](https://docs.github.com/en/rest/issues/issue-dependencies) through the repository-prescribed gh CLI. Every ticket references the parent specification; no existing parent issue was edited or closed.

## Milestones and exit gates

| Milestone | Tickets | Exit gate |
| --- | --- | --- |
| A — UX and evidence feasibility | 01, 07 | Approved connected UX, recorded user-comprehension review, source rights and representative pay evidence validated. Source failure blocks personalized release, not prototype work. |
| B — Private profile and agent foundation | 02–05 | Manual/resume confirmation, owner isolation, version conflicts and a bounded recoverable task demonstrated. Occupation ambiguity is user-resolvable. |
| C — Defensible market decisions | 06, 08–10 | National/local evidence, reproducible personalized interval, sparse fallback, accessible geographic comparison and eligible postings demonstrated. |
| D — Useful action and materials | 11–15 | Roadmap creation/progress, fact-grounded resume, readable free exports and optional photo handoff demonstrated. |
| E — Trust and connected experience | 16–17 | Complete journey, deletion/export and recovery verified; context cannot bypass permissions or overwrite edits. |
| F — Release preparation | 18–20 | Approved budget/quotas, honest marketing, all quality/privacy/photo gates, production-baseline evidence and rehearsed rollback. |

Milestones describe outcomes, not a strictly serial schedule. For example, 15 and 18 can start earlier when their own blockers finish. No duration or launch date is promised without implementation estimates and provider access.

## Concrete technical handoff

The specification defines:
- Existing Angular/.NET/SQL architecture and independent career/photo ledgers.
- Private aggregates, accepted versus proposed versions, evidence provenance and deletion ownership.
- Authenticated API families, optimistic concurrency, error semantics and idempotency.
- SQL-backed durable agent tasks, lease/fencing, retry reconciliation, cancellation and bounded usage.
- Private resume limits, quarantine/extraction, manual fallback and retention proposals.
- Versioned national evidence and a deterministic comparable-pay cohort/calculation policy.
- Six-page behavior, shared assistant context, material review/export and existing photo return paths.
- Server-side flags, progressive routing and rollback preserving paid photo delivery.

The ticket acceptance criteria are the implementation contract. Do not interpret source-selection proposals, starting evidence floors, runtime ceilings or proposed retention values as already measured production policy.

## Decision register and stop rules

| Decision | Implementation owner | Required evidence / stop rule |
| --- | --- | --- |
| Career visual world | 01 | Use Impeccable visual-direction review. Preserve name/logo/domain. Do not copy screenshot mascot, personal details or financial UI. |
| Resume parser/scanner | 03 | Supported format/quality, commercial licensing, bounded resources and quarantine behavior. Missing scanner prevents document parsing, not manual entry. |
| Text model/provider | 04 | Tool reliability, privacy/retention, latency and cost evaluated. No provider selected merely because the app already has an image API credential. |
| Posting source/license | 07 | Approved commercial rights and usable coverage. Do not buy services or accept new paid terms without user approval. |
| Personalized method | 07/08 | Versioned fixtures, evidence-floor and interval calibration across three occupation families/multiple geographies. Benchmark-only output cannot close the covered-case requirement. |
| Retention/backup expiry | 03/16 | Actual infrastructure/provider policy documented before beta; deletion replay tested on restored backup. |
| Free-use operating budget | 19 | User-approved spending ceiling and measured quotas. Saved results/basic exports remain available after generation limits. No career subscription introduced. |
| Runtime/build baseline | Before 02; verified in 20 | Current source targets .NET 10 while SDK pin names a preview. Resolve compatibility explicitly without hiding a runtime migration in the feature. |
| Production parity | 20 | Compare intended source SHA, built artifact digest and deployed revision. A matching local main alone does not prove production parity. |
| Public launch | 20 | Explicit owner GO after quality/privacy/source/cost gates. Preparing the plan or tickets is not permission to deploy. |

A blocked vendor decision has a specified outcome: retain dated benchmarks in development, mark current-posting/personalized capability unavailable, and do not market the full career release as complete. Do not silently narrow the approved product.

## Requirement-to-ticket coverage

| Requirement | Primary coverage |
| --- | --- |
| UX priority and six pages | 01, 02, 06, 09, 11, 13, 17 |
| Agent-first, research + in-app execution | 04, 05, 17 |
| Resume/background and user-confirmed memory | 02, 03, 16 |
| Nationwide career analysis | 05, 06, 09, 10 |
| Personalized earning potential, not personal net worth | 07, 08 |
| Roadmaps and practical progress | 11, 12 |
| Factual career materials and free exports | 13, 14 |
| Optional paid photos, existing customer continuity | 15, 18, 20 |
| Same name/domain and free career core | 18, 19 |
| Privacy, consent and no external side effects | Every storage/tool slice, 16, 20 |
| Latest main / production matching evidence | Baseline check below, 20 |
| Testable delivery and reversible release | Every ticket, 20 |

Financial/investing features, automated applications, outreach and new career subscriptions are excluded globally rather than left as ambiguous future subtasks.

## Validation plan

Testing seams were approved by the user: existing authenticated API integration factory and Playwright browser flows, supplemented by deterministic market fixtures and real SQL Server concurrency tests. Each implementation issue owns relevant behavioral tests. Do not replace production-like transaction tests with EF in-memory assertions.

Release acceptance includes:
1. Every displayed market number has its definition, source and period.
2. Covered personalized cohorts reproduce exact fixture calculations; sparse cases are explicit.
3. No fabricated qualification, cross-user disclosure or unauthorized side effect in the fixed evaluation set.
4. Cancellation, duplicate requests, lease expiry, stale edits and quota races do not lose work or double-consume allowance.
5. The full manual/resume-to-export journey works without a photo purchase, on mobile and keyboard.
6. Existing photo entitlement, preview, purchase, fulfillment and recovery regressions remain green.
7. Retention, deletion retries and restored-backup tombstones are demonstrated.
8. Target-user comprehension review covers the five tasks defined in the UX blueprint.
9. Selected cost limits and source/provider rights are approved.
10. Rollback rehearsal restores prior public/default routing without dropping data or breaking paid delivery.

These are future product-verification gates. Planning validation only checks artifact completeness, issue contents/labels and dependency integrity; no career application tests were run because no career implementation was made here.

## Rollout and rollback checklist

Before deployment: identify source SHA/artifact digest; pass CI and evaluation; verify migration compatibility; approve retention, source rights, budgets and release owner; record current feature-flag values. Keep existing photos available.

Progression: staff-only career access → invited beta → broader career entry → public homepage pivot last. Agree monitoring thresholds from actual cost/error baselines. Observe run completion, first useful brief, target selection, material export, meaningful roadmap action, source age, deletion failures, provider latency/cost and photo fulfillment. Do not collect resume content in analytics.

Rollback triggers include any confirmed cross-user disclosure, unauthorized charge/action, photo-fulfillment regression or unresolved breach of approved cost/error thresholds. Release owner disables relevant career actions/entry/public messaging; preserve saved content for read access unless privacy requires denial. Workers reconcile in-flight operations; never blindly retry charged work. Restore old /app entry. Do not drop additive schemas or wipe user data.

Resumption requires reproduced cause, passing regression evidence and an explicit release-owner decision. Production commands/flag locations must be verified against the actual deployment during 20, not guessed in this planning document.

## Baseline and publication record

- Work performed through Ubuntu WSL in the existing repository.
- Read-only check on 2026-09-22: local HEAD and remote main both resolve to `340273b7a594186707a5c0469ef9aa412198eb0e`.
- This does not prove production uses that source or that the dirty working tree equals the committed tree. No reset, cleanup, checkout, commit, push or deployment was performed.
- Published parent #376 and 20 implementation issues #377–#396, all with `ready-for-agent`.
- Existing unrelated issues and application files were preserved. Local planning artifacts remain uncommitted.
- Final read-back verified the parent specification and all 20 issue bodies against local copies; all 21 issues are open and carry `ready-for-agent`.
- All 25 native blocking edges exactly match the approved graph; the graph is acyclic and its initial frontier is #377 and #383.
- Validated 22 local implementation Markdown files: all relative links resolve and no publication placeholders remain. The scoped tracked-file whitespace check passed; no application test run is claimed.

## Completion definition

This goal is complete when the specification, 20 acceptance-criteria briefs, dependency graph, decision gates, test plan and rollout/rollback contract are finalized and published with verified links. It does not require solving the deliberately assigned provider/prototype implementation work, building the product, or launching it.
