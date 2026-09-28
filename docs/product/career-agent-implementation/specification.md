# Agent-first career workspace: implementation specification

Status: implementation specification finalized 2026-09-22. The user approved the 20-ticket breakdown, blocking relationships, testing approach and GitHub publication. Product direction and six-page information architecture are also approved. This specification does not authorize a production deployment, paid data purchase, outbound application, or new domain.

## Problem Statement

Experienced U.S. professionals need to decide their next career move, understand relevant pay evidence, and prepare credible materials. A photo-only workflow cannot answer these questions. A chat-only answer is also insufficient: users need inspectable sources, editable facts, durable work, and a clear next action.

## Solution

Pivot AI Profile Photo Maker on aiprofilephotomaker.com into a personal career assistant with a useful free core and optional paid photos. The agent coordinates research and in-app execution across Career agent, Career profile, Career analytics, Career heatmap, Career roadmaps, and Career materials. Structured workspaces remain usable without chat. Existing photo customers keep their purchases, links, and fulfillment rights.

## User Stories

1. As a new visitor, I want the existing brand to explain its career purpose, so that I understand the broader service without changing sites.
2. As an existing photo customer, I want to continue my photo work and purchases, so that the pivot does not strand my paid assets.
3. As a professional, I want to start with a resume or manual background, so that missing a document does not prevent useful help.
4. As a professional, I want to understand document processing before submission, so that I can make an informed privacy choice.
5. As a professional, I want extracted facts attributed to their source, so that I can identify and correct mistakes.
6. As a professional, I want to confirm contradictory dates and claims, so that uncertain extraction never becomes authoritative history.
7. As a professional, I want to update one fact without losing the prior version, so that I can recover from mistakes.
8. As a professional, I want a goal with location, remote-work, role and effort preferences, so that advice fits my constraints.
9. As a professional, I want occupation suggestions explained through my duties, so that a misleading job title does not determine my analysis.
10. As a professional, I want to confirm ambiguous occupation matches, so that different careers are not mixed in one report.
11. As a professional, I want dated national and local wage benchmarks, so that I can understand the wider market.
12. As a professional, I want a personalized comparable-pay range supported by real observations, so that the analysis reflects relevant roles rather than an arbitrary percentile.
13. As a professional, I want to inspect a range's cohort, units, exclusions and limitations, so that I understand what it does and does not mean.
14. As a professional, I want sparse or suppressed evidence labeled honestly, so that absence of data is not mistaken for low pay.
15. As a professional, I want desired pay separate from estimated pay, so that my preference does not manufacture supporting evidence.
16. As a professional, I want to compare eligible U.S. locations, so that I can choose a realistic market.
17. As a keyboard or mobile user, I want equivalent map and table results, so that geographic analysis is accessible.
18. As a remote-work seeker, I want actual location restrictions visible, so that remote listings are not treated as universally eligible.
19. As a professional, I want postings, employment stock and projected openings distinguished, so that different measures do not imply false live demand.
20. As a professional, I want evidence-backed roadmap alternatives, so that I can choose between realistic trade-offs.
21. As a professional, I want tasks fitted to my available time, so that a roadmap leads to practical action.
22. As a professional, I want to record progress and review replanning changes, so that the agent does not erase my work.
23. As a professional, I want a targeted resume grounded in confirmed accomplishments, so that drafting does not invent qualifications.
24. As a professional, I want to review a material diff before accepting it, so that my own edits remain in control.
25. As a professional, I want a useful summary and free basic exports, so that the core journey does not require a purchase.
26. As a professional, I want photos to be optional and separate from the default resume, so that appearance does not determine my career analysis.
27. As a photo customer, I want exact allowances and explicit confirmation before generation or spending, so that an agent cannot silently consume my purchase.
28. As a returning user, I want my goal, reports and interrupted tasks restored, so that a disconnected browser does not lose work.
29. As a professional, I want bounded progress and cancellation, so that agent activity remains understandable and controllable.
30. As a professional, I want one contextual assistant across all pages, so that I do not repeatedly explain the artifact I am viewing.
31. As a free user, I want limits and reset times explained while saved work remains readable, so that exhaustion does not lock away my results.
32. As a professional, I want my career information exported or deleted, so that I control the assistant's saved knowledge.
33. As a professional, I want private content isolated from other accounts, so that document and report identifiers cannot expose my data.
34. As an operator, I want cost, provider failures and quality measured without logging resumes, so that the free service can be sustainable and private.
35. As an operator, I want a reversible same-domain rollout, so that a career incident does not break paid photo delivery.
36. As a professional, I want claims about career fit and pay independent of gender, ethnicity or my photograph, so that irrelevant appearance and demographic data are excluded.

## Implementation Decisions

### Architecture and scope

- Extend the existing Angular 20 client and ASP.NET Core/.NET 10 API, SQL Server/EF Core, identity, private storage and payment infrastructure. No framework migration or multi-agent platform is required.
- Keep career services behind authenticated owner-scoped APIs and independent feature flags. Use a single orchestrator with a small server-enforced tool allowlist. Calculations, permission checks and paid entitlements are deterministic.
- Start with a SQL-backed work queue and hosted worker using durable checkpoints, expiring leases and fencing tokens. A deployment may run multiple API instances; only the active lease holder may commit a step. Existing headshot operation recovery is prior art, not a shared career/photo credit counter.
- Poll persisted run state initially; do not require streaming or a second realtime service. Suggested intervals: 2 seconds while visible/working, backing off to 10 seconds; pause polling in hidden tabs and refresh on return. Server state, not the browser timer, is authoritative.
- New migrations are additive. Career records never reinterpret existing photo packages, credits, identity fields or webhook state.

### Routes and information architecture

Primary routes are /app/career, /app/career/profile, /app/career/analytics, /app/career/heatmap, /app/career/roadmaps and /app/career/materials. Supporting setup and artifact detail routes do not become extra primary navigation items.

Career-enabled accounts may enter the career home from /app. Flag-off accounts retain the existing default. Preserve /app/enhance, /app/gallery, photo pricing, authentication and payment-return URLs. The existing name, logo, domain and email identity remain.

Desktop specialist pages prioritize the artifact with a collapsible assistant. Mobile uses a workspace plus an assistant sheet with focus/scroll restoration. The agent home leads with goal, active task, latest useful result and one next action; it is not an empty chat box. Prototype validation precedes production career UI.

### Data ownership and versioning

All private aggregates carry a server-derived owner ID, timestamps and a concurrency token. IDs alone never authorize access. Shared evidence is explicitly classified reference data, never inferred from an absent owner.

| Aggregate | Required contract |
| --- | --- |
| CareerProfile / ProfileVersion | Confirmed professional facts, duties, skill evidence and preferences; immutable accepted versions; draft proposals separate; fact provenance with document/page/span where supported |
| CareerGoal / GoalVersion | Target options, selected occupation, location/remote eligibility preferences, desired pay and weekly effort; active version |
| ResumeDocument | Private opaque storage key, checksum, detected format, size, processing/scan state, retention expiry and extraction result; never a public blob URL |
| Conversation / AgentRun / TaskStep | Goal, bounded messages, pinned input versions, operation IDs, state, lease/fence, retry count, checkpoints, safe progress summary, outputs and usage references |
| SourceDataset / MarketSnapshot / EvidenceReference | Source and license policy, release/reference dates, taxonomy/geography versions, immutable normalized measures, source links and content hashes; commercial source retention obeys contract |
| CompensationScenario / CareerReport | Input versions, cohort and calculation version, source snapshots, units/measure, counts/exclusions, output range or unavailability reason, sections and stale marker |
| Roadmap / RoadmapVersion / ActionItem | Selected scenario, assumptions, available effort, milestones, dependencies, evidence/output links and task status |
| Material / MaterialVersion | Kind, target, source fact IDs, content, human edits, proposed diff, accepted version and private export metadata |
| CareerUsageReservation / UsageEntry | Owner, policy version/window, idempotent operation, reserved/actual units and settlement status; no reuse of photo allowance fields |
| CareerDeletionRequest / DeletionTombstone | Scope, state, retry schedule and completed purge categories; minimal audit identifiers only |

Use SQL uniqueness for owner plus idempotency key and task/tool operation identifiers. Use row-version/ETag optimistic concurrency for edits. Immutable versions support provenance and rollback; the active-version pointer changes atomically. User changes mark dependent artifacts stale rather than deleting them or silently regenerating. Persist normalized provenance references; do not duplicate entire resumes in every prompt or report.

### API conventions and boundaries

Reuse current authentication and response envelope: success, data, and a structured error with code/message. Add correlation ID, retry guidance and field errors without exposing provider internals. Lists are bounded and cursor-paginated. Owner identity always comes from authentication, never a body/query user ID.

| Endpoint family | Behavior |
| --- | --- |
| GET/PUT /api/career/profile | Read or conditionally save manual facts; return current version/ETag; stale If-Match returns 412 |
| POST /api/career/profile/proposals; POST /api/career/profile/proposals/{id}/accept | Draft explicit edits; accept chosen changes against a pinned base version |
| POST /api/career/resumes; GET/DELETE /api/career/resumes/{id} | Private multipart upload, processing state and removal; reject unsupported/oversized input before model work |
| GET/POST/PATCH /api/career/goals | Persist goal, select target and preferences with version preconditions |
| POST /api/career/runs | Allowlisted action, goal/input versions, explicit artifact context, idempotency key; 202 with stable run ID and status URL |
| GET /api/career/runs/{id}; POST .../{id}/answers; POST .../{id}/cancel | Owner-scoped persisted status, structured answer continuation and idempotent cancellation |
| GET /api/career/reports; GET .../{id} | Immutable saved report, sources and current/stale status; no implicit provider refresh on GET |
| GET /api/career/markets; GET /api/career/markets/compare | Occupation, geography type/IDs, metric and release filters; same normalized result powers map and table |
| GET /api/career/opportunities | Licensed observations with explicit eligibility/coverage; no global vacancy claim |
| GET/PATCH /api/career/roadmaps/{id}; POST .../{id}/proposals/{proposalId}/accept | Review/edit path and tasks; changes preserve completed work and enforce dependency validity |
| GET/PATCH /api/career/materials/{id}; POST .../{id}/versions/{version}/exports | Versioned editing and private PDF/DOCX exports; export read access does not depend on remaining model quota |
| GET /api/career/usage | Remaining free allowance, reservation state, policy version and UTC reset timestamp |
| POST /api/career/privacy/export; POST /api/career/privacy/delete | Auditable asynchronous owner-controlled export/purge; recent-auth requirement for deletion |
| Existing photo endpoints | Remain authority for packages, generation, payment and download; career tools cannot bypass their checks |

First profile creation does not need If-Match; subsequent writes do. Missing preconditions return 428; stale edits 412; cross-owner resources return 404; invalid transitions 409; quota exhaustion 429 with known reset; feature disabled 403 with a stable code. Replaying an idempotency key with different input returns 409. Reads never consume generation allowance.

### Intake and privacy defaults

- First release accepts text PDF, DOCX and manual text. Proposed starting limits: 10 MiB/file, 25 pages and 100,000 extracted characters; expose actual active limits. Validate magic bytes, decompressed archive bounds and file type; reject encrypted/macro-enabled inputs and zip bombs. Never execute embedded content.
- Quarantine uploads until configured malware screening succeeds; scanner failure is fail-closed for document parsing. Manual input remains usable. Parser selection is based on a fixture evaluation, commercial license review and process resource limits.
- Scanned/image-only or unreadable resumes get an explicit manual/paste alternative. OCR is deferred, not silently treated as empty experience.
- Show processing/retention notice before upload and keep consent version. Raw uploads default to purge 30 days after processing, or immediately on user request. Confirmed facts and accepted artifacts persist until user deletion; provider intermediate payloads are not retained unnecessarily.
- Proposed privacy defaults: purge unaccepted extraction proposals and raw conversation after 30 days; retain accepted artifacts independently. Policy must be reflected in product controls and reviewed before beta. Provider retention/region and backup expiry are launch gates, not assumptions.
- Resumes, postings and chat are untrusted content. Use validated tool arguments and allowlisted connectors, block arbitrary URL retrieval, sanitize rendered content, minimize PII in model requests and redact logs. Name/contact details are excluded from market matching; photo gender/ethnicity/image signals are never passed.
- Deletion first prevents new access and cancels relevant work, then retries private blob and derived-row purges. Retained billing records stay separate. Backup restore must replay deletion tombstones before serving users; publish the actual backup-expiry period before launch.

### Agent lifecycle and permissions

States: queued, working, needs-input, completed, failed, cancelled. A cancelled or terminal run never becomes working again; retry creates a new linked attempt unless a failed internal step is safely replayed under the original operation. Cancellation sets a durable request; workers stop scheduling steps and discard late writes. Usage already incurred is disclosed.

Proposed default execution envelope: 12 tool steps, 5 minutes active execution, 2 retries for retryable provider failures, and a configurable per-run monetary ceiling. Paused needs-input time does not consume active duration; expire abandoned runs after 7 days. All limits are server-configured and measured before widening access.

Each run pins profile/goal versions and an allowed action scope. Safe research can save a new report. Profile edits, target changes, roadmap replacement and material changes create proposals requiring acceptance. A stale run may save a historical result with a warning but cannot replace a newer active artifact. Tools expose concise observable steps, never private reasoning.

Unknown provider outcome is reconciled before retry; do not blindly reissue potentially charged photo operations. Reserve career allowance atomically before provider work, settle actual usage once, and release unused reservation after failure/cancel reconciliation. A paused run releases unspent capacity and re-reserves on continuation. Limits have an account window plus per-run/provider caps; photo operations use their existing independent ledger.

No outbound submission, email, social posting, payment initiation or autonomous photo generation tool is included in the first-release allowlist. A photo action opens the existing explicit user-confirmation flow with the exact entitlement/price and selected goal context.

### Market evidence and personalized pay

BLS occupational wage benchmarks, O*NET duties/skills and projection data use versioned imports, validated code crosswalks and attributable source releases. API responses label geography, definition, reference period and publication date. Missing, suppressed and stale are distinct; never encode them as zero. The benchmark is not total compensation or an individualized prediction.

A licensed posting source must pass rights, caching/retention, coverage, freshness and expense review before production integration. Qualification must yield representative reports for at least three occupation families and multiple geographies, including sparse/ambiguous cases. This is a release prerequisite; a generic benchmark alone does not satisfy personalized pay.

Initial algorithm for validation, not a salary-prediction promise:
- Filter to confirmed occupation/duties, explicit level when supplied, employment type, pay basis, selected geography and known work-arrangement eligibility.
- Use only employer-disclosed, compatible pay observations for personalized ranges. Keep provider-modeled pay separate. Exclude unknown currency/basis; annualize only with explicit documented hours/period. Do not extrapolate equity/bonus.
- Deduplicate requisitions and reposts using provider IDs and normalized employer/title/location evidence; cap or group repeated employer observations and show concentration.
- Proposed evidence floor: at least 10 independent current observations from at least 5 employers in a 90-day lookback, subject to stricter provider expiry. All floor values are versioned and must be calibrated in the qualification ticket.
- For range observations, define the displayed interval as the 25th percentile of normalized lower bounds to the 75th percentile of upper bounds using an explicitly versioned interpolation rule; a single-value observation has identical bounds. Label this an observed advertised-pay interval, not a confidence interval, offer prediction or exact personal worth.
- Report included/excluded counts, employer concentration, observation dates, matching dimensions, omitted filters and sensitivity to the selected cohort. Insufficient evidence yields an explicit broader occupational fallback.
- Do not infer salary multipliers from years of experience, profile completeness, photograph, demographics or desired pay. Do not infer unknown seniority or remote eligibility simply to fill the cohort.
- The qualification evaluation may change this initial interval rule or floor with written rationale and fixed fixture expectations before beta; version every released calculation.

National comparisons use matching geography boundaries and snapshot releases. Wage, employment concentration, projections and observed-posting metrics remain separate. Disable unsupported metric controls with an explanation rather than fabricating data. Remote unknown eligibility stays unknown and cannot appear in an eligible-only list.

### Roadmaps, materials and photo continuity

Roadmaps offer closest-fit, higher-ambition and steadier-transition options only when evidence supports distinct choices. Tasks have a concrete output, evidence/rationale, editable effort, dependency and status. Start with this week and 30/60/90-day milestones. Replanning is a diff; no automatic salary uplift from task completion.

Resume drafts link claims to confirmed facts. Missing metrics become review prompts outside final copy, not invented accomplishments. Preserve human edits and accepted versions. Free basic PDF/DOCX export uses an accessible, text-readable template; no ATS guarantee. Headshots are omitted by default and offered as separate assets.

Career materials handoff uses server-validated resource ownership and allowlisted relative return destinations. Payment returns continue to follow existing photo fulfillment. A paid-photo decline never blocks career work or export.

### Feature flags and rollout

Use distinct server-controlled flags for career access, career worker actions, licensed posting/personalized-pay integration and public career messaging. UI flags are presentation only; API checks remain authoritative. Keep global and per-account kill switches.

Roll out staff accounts, then invited beta, then broader career routing after release approval. Homepage messaging flips last. Existing photo routes remain available at every stage. Flag rollback stops new career work and public promises while preserving readable saved results where safe; an emergency privacy switch can deny affected access. Do not roll back additive migrations or delete career data as a routine rollback.

## Testing Decisions

User-approved testing seams:
- Observable API behavior through the existing authenticated integration-test factory: ownership, validation, versioning, durable runs, generated artifacts and failure responses.
- Existing Playwright browser patterns for the complete career journey, mobile/keyboard navigation, reconnect/recovery, old photo entry and purchase return.
- Deterministic source fixtures for taxonomy mapping, deduplication, unit normalization and pay calculations; real SQL Server tests for uniqueness, transaction isolation, quota races and worker fencing. In-memory EF tests do not prove these invariants.

Each ticket adds only tests for its behavior, extending these seams. Prefer assertions on saved/read results and permitted actions over private helper calls or prompt wording.

Evaluation fixtures include at least three occupation families, multiple geographies, contradictory resumes, unreadable documents, sparse pay, stale sources, duplicates, malicious resume/posting instructions, cross-user identifiers, provider timeouts and stale profile versions. Pin all model/source/calculation versions. A prompt/model change reruns regression evaluation.

Release gates: every displayed market number has source/definition/date; covered-case personalized ranges reproduce from fixtures; no fabricated qualification, unauthorized side effect or cross-user disclosure in the fixed evaluation set; deterministic recovery/cancel/quota tests pass; complete no-purchase journey exports readable materials; photo fulfillment regression suite passes. These are test gates, not guarantees of universal model correctness.

Impeccable prototype review covers all six pages, empty/loading/error/sparse/stale states, 200% zoom, keyboard focus, screen-reader status, reduced motion and a narrow phone layout. Research with representative users checks comprehension of pay and successful completion of five specified tasks; visual polish does not substitute for comprehension.

## Out of Scope

New brand/domain; financial net worth, investing or trading; employer hiring decisions; opaque employability scores; automated applications/outreach; public profile publishing; recurring monitoring subscription; purchased feeds without approval; OCR in first release; automatic inclusion of headshots in resumes; salary guarantees; replacing the photo payment system; a framework rewrite.

## Further Notes

This specification is a plan, not a claim that the career product exists or that production matches main. Before implementation, reconcile the dirty worktree without discarding user changes, identify the baseline commit, and choose an isolated codex/ branch. Before release, separately compare the deployed artifact digest/source SHA with the intended main commit.

The existing SDK pin is a .NET 10 preview while API packages target .NET 10. Treat build/runtime compatibility as a baseline check; do not silently fold a runtime upgrade into a career ticket.

Unresolved launch decisions are explicitly owned by their delivery tickets: visual direction (01); parser/scanner and retention disclosure (03/16); text model/provider privacy and per-run ceiling (04/19); licensed posting source and calibrated cohort criteria (07); operating budget, actual free quotas and provider retention/backup expiry (16/19). None requires a new domain or a new paid career offer.
