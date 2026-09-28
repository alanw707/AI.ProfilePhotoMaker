# Personal career agent: product pivot plan

Status: planning complete; the user confirmed the six-page UX brief on 2026-09-22 after confirming audience, agent scope, existing name/domain, and free-core commercial direction. Technical recommendations remain implementation proposals. No application implementation or production rollout has occurred as part of this planning work. UX and page definitions are the first design priority; see [the approved page blueprint](career-agent-ux-blueprint-2026-09-22.md).

## Product direction

Help experienced U.S. professionals decide their next career move and prepare for it. The user supplies a resume, professional background, preferences, and a goal. Their personal career agent develops a confirmed professional profile, investigates suitable roles across the country, explains compensation evidence, and turns the findings into a practical roadmap and application materials. Professional profile photos become one capability within that journey.

Proposed promise: **Understand your career options. Know the market. Take your next step.**

The main experience is an agent working toward the user's goal, with durable results and visible progress. Conversation coordinates the work; editable profiles, reports, resumes, maps, and roadmaps hold the results. Users can open and edit those workspaces directly.

Financial accounts, investments, trading, personal wealth, budgeting, and net-worth calculations are excluded. Use **market compensation range** for evidence about pay and **earning potential** for explicitly conditional career scenarios. Neither means the person's worth or a guaranteed offer.

## Decisions and planning method

This plan follows Matt Pocock's `ask-matt` routing into `grill-with-docs`, its `grilling` decision rounds, and `domain-modeling`. Research and repository inspection inform the decisions. The approved implementation specification and 20-ticket breakdown were subsequently published through `to-spec` and `to-tickets`; see the [implementation plan and issue index](career-agent-implementation/README.md). This document remains the product-direction reference rather than the implementation specification.

Confirmed by the user:

- Pivot the existing product toward a personal career assistant.
- Accept professional background and a resume; analyze the national job market; estimate market earning potential; provide guidance; include profile photos.
- Make the site agent-first.
- First audience: experienced U.S. professionals considering their next role.
- Agent scope: research and execution inside the app. Review is required before external applications, messages, or spending.
- Exclude the financial portion of the reference product.

Additional decisions confirmed by the user:

- Keep **AI Profile Photo Maker** and **aiprofilephotomaker.com**; directly pivot the existing site. Do not buy a domain or create a separate brand/product.
- Offer a useful free career core with reasonable usage limits and optional paid photo packages. Defer additional career pricing until usage and cost evidence exists.
- Prioritize UX and real pages, particularly Career Profile, Career Analytics, Career Heatmap, and Career Roadmaps, as illustrated by the four additional screenshots.
- Six-page structure confirmed on 2026-09-22: Career agent, Career profile, Career analytics, Career heatmap, Career roadmaps, and Career materials, with a shared contextual assistant.

Implementation planning recommendations, not represented as user approvals: start with one orchestrating agent; use the existing application stack; defer outbound application automation; validate recurring monitoring before selling a subscription. Free usage quotas, data vendor, and any future paid career offer are discovery outputs, not invented facts.

## Screenshot interpretation

The initial Lossdog screenshot shows chat as the central surface and a Career navigation group containing Profile, Analytics, Heatmap, and Roadmaps. Four additional screenshots show a profile editor, analytics with skills/completion diagrams, a U.S. compensation heatmap, and alternative roadmaps beside a persistent assistant. These visible elements inform the page blueprint. The screenshots do not establish Lossdog's data sources, calculation accuracy, agent capabilities, or business model. Their displayed salary figures and profile content are reference UI data, not verified facts about this user.

Retain the relationship between conversation and career workspaces. Exclude the financial portfolio, trade feed, watchlist, prediction/trading features, and digital assets. Do not copy the mascot, brand, artwork, or financial navigation.

## Agent-first website and application

### Public website

- Lead with career clarity and action, supported by a short example of a completed career brief. Clearly label illustrative data.
- Primary action: **Plan my next career move**. Offer resume upload or **Tell me about your background**.
- Explain the concrete outputs: relevant roles, national and local market evidence, compensation context, tailored materials, a roadmap, and optional professional photos.
- Show what the agent will do with a resume before upload; require explicit submission and explain retention and deletion.
- Preserve a discoverable **Professional photos** entry point for visitors arriving for the current product.
- Show source dates and limitations in the example report. Do not advertise guaranteed salary increases, placement, or comprehensive live nationwide vacancy coverage.
- Retain the name, domain, authentication origins and email identity. Add a clear career-assistant descriptor to the existing branding. Update the homepage promise and navigation directly; preserve useful photo SEO pages, customer routes and checkout return behavior.

### Signed-in application

Navigation: **Career agent**, **Career profile**, **Career analytics**, **Career heatmap**, **Career roadmaps**, and **Career materials**, with Settings and Help as utility items. Career materials contains resumes, professional bio/LinkedIn drafts, and the existing photo workspace. Analytics contains pay analysis and role comparisons. Page labels intentionally preserve the clear grouping in the user's reference images.

The default home presents the current goal, the latest useful result, outstanding questions, active work, and one recommended next step. New users receive a guided starting prompt rather than an empty chat box. Returning users resume their saved goal.

Conversation and results share a workspace. Desktop can show an active report beside the conversation; mobile opens the same saved result as a full-screen view. Chat is optional for structured edits. Navigation and task status remain keyboard accessible, and the map has an equivalent sortable table.

### First complete journey

1. User: "I have ten years in operations and want a better next role without relocating."
2. Agent accepts a PDF/DOCX resume or pasted background, extracts facts with provenance, and highlights missing or ambiguous details.
3. User confirms the profile and goal: role interests, location, remote/hybrid preference, relocation limits, desired pay, and time available for development. Current salary is optional and does not anchor the market estimate.
4. Agent shows a short task plan: identify plausible roles, compare markets and pay, then propose next steps. The user can edit it.
5. Agent produces a saved career brief: a small set of plausible role directions, supporting experience, missing evidence, national/local market context, a compensation benchmark, and clear uncertainty.
6. User explores the national map and compares eligible locations or remote opportunities. Excluded locations remain understandable; remote does not imply eligibility everywhere.
7. User selects a target. Agent drafts a targeted resume and a 30/60/90-day roadmap, with tasks tied to evidence gaps and user availability.
8. Agent offers photo preparation when it supports the selected goal. The user may reuse an existing headshot, create one within their photo entitlement, or skip it. Photo completion never blocks market analysis.
9. User reviews and exports the materials. Later sessions resume the same goal, completed work, and source-backed report.

Example compensation response format: "For this occupation and region, this source reports these wage percentiles for this reference period. Comparable postings provide this separate advertised range. Your confirmed experience supports these roles; these gaps remain." Populate numbers only from retrieved evidence.

## Capability boundaries

| Capability | First release | Subsequent expansion |
| --- | --- | --- |
| Professional profile | Resume + background; editable, versioned facts; user confirmation | Multiple target-role profiles and richer work samples |
| Career agent | Research, compare, explain, draft, save, resume and track work | User-enabled periodic market refresh and change notifications |
| National market analysis | National benchmark plus state/metro comparisons; coverage and source dates | Better industry and seniority segmentation as data supports it |
| Compensation | Sourced occupational benchmark plus a personalized comparable-pay range when evidence supports it; explicit uncertainty and visible fallback | Richer total-compensation and longer-term scenarios with additional validated evidence |
| Opportunity map | Employment concentration, pay and available posting evidence as distinct views | Changes over time with comparable datasets |
| Guidance | Role alternatives, evidence gaps, prioritized roadmap, interview preparation prompts | More detailed interview coaching and progress-driven revisions |
| Career materials | Targeted resume, professional summary, optional photo/export kit | Additional reusable application kits |
| External actions | Export drafts and open original job links | Applications/outreach only as a separately scoped, reviewed capability |

No employer-side candidate screening, hire/no-hire decisions, opaque employability score, invented credentials, automatic mass applications, or photo-based career ranking.

## Market evidence and compensation method

### Source strategy

| Source | Proposed use | Limits and access gate |
| --- | --- | --- |
| [BLS OEWS tables](https://www.bls.gov/oes/tables.htm) | Occupational wage and employment benchmarks at national, state and metro levels | Historical survey estimates; show reference period and publication date, not "live salary" |
| [BLS Employment Projections](https://www.bls.gov/emp/data/occupational-data.htm) | Occupational outlook and projected openings | Projections are distinct from current vacancies; label horizon |
| [O*NET database](https://www.onetcenter.org/database.html) | Occupation taxonomy, tasks, skills and transferable experience | Preserve version and required attribution; taxonomy matching needs user review |
| [Adzuna developer API](https://developer.adzuna.com/overview) | Candidate source for current advertisements and advertised-pay observations | App credentials required; assess U.S. coverage, deduplication, commercial terms, caching and redistribution before selection |
| [CareerOneStop developer resources](https://www.careeronestop.org/Developers/LinkToUs/link-to-us.aspx) | Alternative source to investigate for career and employment data | Developer landing page verified; detailed API page did not load in this research. Endpoint suitability, approval and rights remain unverified |

Official O*NET licensing is described in its [license agreements](https://www.onetcenter.org/license_agreements.html). Retain dataset-level attribution and review exceptions. A public endpoint is not by itself proof of commercial redistribution permission.

### Processing rules

1. Extract evidence from the resume without converting uncertain text into asserted facts. Let the user correct titles, dates, skills, and responsibilities.
2. Match duties and demonstrated skills to one or more occupation codes; do not rely on job title alone. Version occupation-code crosswalks between datasets. Ask when plausible matches would materially change the report.
3. Retrieve national and selected state/metro wage distributions. Keep geographic fallback visible; suppressed data stays unavailable.
4. Present a reference interval such as published P25–P75 only when available. Label it a wage distribution, not a confidence interval or a prediction of the user's salary.
5. Add current postings only from approved providers. Deduplicate employer/requisition/location matches, record sample size and dates, remove expired entries, and separate remote eligibility from office location. Show postings as observed coverage, not the whole market.
6. Keep employer-disclosed pay separate from provider-modeled pay. Normalize currency and pay period only when hours and basis are known. Do not blend wage measures, base salary, bonus and equity into an unlabeled number.
7. Start personalization with comparable roles, responsibilities, location and qualifications. Do not assign a salary percentile from years of experience or an LLM's intuition. If evidence cannot support personalization, show the occupational benchmark and say what remains unknown.
8. Show evidence quality in plain language: occupation match, geographic specificity, source age, sample availability, and agreement. Do not display an invented numerical confidence probability.
9. Potential future pay is a scenario tied to a stated target role and demonstrated requirements; a course or new photo never produces a promised salary uplift.

The personalized estimate is part of the intended first-release outcome, not satisfied by a generic salary lookup alone. Build a reproducible comparable cohort from confirmed responsibilities, target role level, location, work arrangement and employment type where the source supplies those attributes. Record the inclusion/exclusion rules and why each comparison applies. Calculate the observed pay range deterministically, show its definition and coverage, and keep it separate from the wider occupational benchmark. Agree minimum evidence and calibration criteria during the data spike; the model must not invent missing salary observations or an experience-based pay multiplier. If a user's cohort is too sparse, show the broader benchmark as a clearly labeled fallback and name the missing evidence. That fallback is a supported individual case, not a substitute for implementing personalized analysis for adequately covered cases.

The [BLS OEWS FAQ](https://www.bls.gov/oes/oes_ques.htm) distinguishes occupational wage distributions from individual pay, describes the multi-panel estimation approach, and cautions about time-series comparisons. Its wages are not total compensation: coverage includes some incentive pay but excludes benefits and several bonus categories. These definitions must accompany comparisons. Do not label the OEWS measure simply "base salary."

The first data spike must produce one reproducible report for a representative role and geography, with the exact records, source versions, mapping and transformations retained. Live API integration has not been verified by this planning work.

### National coverage acceptance

- Nationwide occupation baseline plus state/metro comparison wherever the source publishes data; map missing and suppressed cells explicitly.
- Separate views for wages, employment concentration, projected outlook and observed postings. Never treat employment stock as open jobs.
- Remote jobs require location/work-authorization eligibility from the posting; unknown restrictions are visible.
- A missing posting feed still permits a dated benchmark report, but disables claims about current vacancy demand.

## Agent behavior and architecture

Use one orchestrating agent with focused tools in the existing application. Separate agents are unnecessary for the first release unless evaluation demonstrates a benefit.

The agent receives the confirmed career profile, current goal, preferences, selected evidence, and permitted actions. It can extract resume facts, propose profile edits, query market snapshots, compare role scenarios, draft materials, update a roadmap, and request an existing photo operation. Computations and authorizations live in deterministic services; the model explains their results.

Durable objects: CareerProfile and ProfileVersion, ResumeDocument, CareerGoal, Conversation, AgentRun, TaskStep, MarketSnapshot, EvidenceReference, CompensationScenario, CareerReport, Roadmap/ActionItem, and MaterialVersion. All are owned by the authenticated user or explicitly designated shared reference data. CareerProfile is separate from the current photo/account UserProfile.

Run states: queued → working → needs-input → working → completed, with failed and cancelled states. Persist task checkpoints and result versions server-side. A disconnected browser reconnects to the same run. Retried tool calls use stable operation IDs; stale results cannot overwrite work based on a newer profile version. Completed steps survive partial provider failures. A user can cancel work and see any already-incurred allowance use.

Bound each run by steps, time, provider calls and cost. Before paid generation, disclose the exact allowance consumed and require the user's action. Enforce ownership, entitlements and action permissions in server tools, including calls initiated by the model. A chat instruction cannot bypass those checks.

Treat resumes, postings and retrieved pages as untrusted content. They cannot supply tool instructions, change permissions, request secrets, or direct arbitrary network fetches. Use controlled retrieval, vetted URL handling, structured tool schemas, minimal PII in provider requests, and redacted logs. Keep intermediate private reasoning out of the UI; show task summaries, source evidence and results.

Persisted memory is user-visible: confirmed profile facts, preferences and goals are inspectable, editable and deletable. Raw conversation should not silently become authoritative professional history. A user correction marks dependent reports/materials as needing refresh.

## Reuse and migration from the current product

Current repository evidence: Angular 20 dependency manifest; ASP.NET Core targeting .NET 10; SQL Server/EF Core models; existing identity/auth guards; Stripe payments; Azure/local storage; headshot generation, package entitlement and retention services. Some product prose still mentions Angular 19, so source manifests take precedence for implementation planning.

| Existing area | Treatment |
| --- | --- |
| Authentication, account settings, admin/support | Reuse; extend consent, export/delete and product-health views |
| Photo workspace, generation and export | Retain as Career materials → Professional photos, reachable directly |
| Photo packages and webhook fulfillment | Preserve purchased allowances and delivery behavior; wrap capability behind an agent tool |
| OutcomePackageDefinition | Photo-specific fields remain intact; add separate career entitlements rather than reinterpreting candidate counts as agent tasks |
| Storage and retention | Reuse infrastructure, add private resume/material categories and explicit retention policy |
| Current UserProfile | Keep account/photo fields; new career model excludes gender, ethnicity and image-derived attributes from pay/role analysis |
| Existing routes and marketing | Pivot this same domain to the career shell behind a rollout flag; preserve photo links and checkout return routes |

ADR-0001 and the current PRODUCT.md make the photo workflow the primary product. ADR-0006 records the agreed new product direction for future implementation; current PRODUCT.md remains a description of the shipping product until that implementation. ADR-0002's photo provider/style behavior and ADR-0005's server-side purchase delivery still apply inside the photo capability. ADR-0004's separation of product health and support operations remains useful.

Use additive schema migrations and independently switchable career routes/features. Rollback disables career entry points without dropping career records or changing existing customer entitlements. The existing photo experience remains reachable during beta and interrupted payments still complete correctly.

## Commercial offer and operating economics

Confirmed direction: **free career core, optional paid photos**. Free users can complete the useful journey: create a profile, receive a sourced career brief and pay context, explore geographic options, maintain a roadmap, and prepare basic resume/bio materials. Do not withhold the first useful report or basic export behind a newly invented career paywall. Existing photo previews, packages and allowances continue; users may skip photo generation or use their own image.

Use transparent per-period research/drafting quotas and cached reference data to control free usage. Existing results remain readable when a quota is exhausted; show reset timing and available actions. Do not market unlimited usage. Determine the actual quota after measuring a complete journey, then record it before beta. Limit upload sizes, expensive refresh frequency, provider concurrency and daily service spend.

Lossdog's [current terms, section 10](https://lossdog.com/terms-of-service/) explicitly describe a free service. The [publisher's February 5, 2026 interview summary with Tom Sosnoff](https://marketmoverspod.com/e/he-sold-thinkorswim-for-750m-then-built-a-billion-dollar-trading-empire-now-tom-sosnoff-is-doing-it-again/) describes initial free access while testing demand and later development of a broader ecosystem. That is a publisher summary, not a verified current revenue breakdown. This research did not establish advertising, referral commissions, data sales, or subscriptions as Lossdog's funding model. Free access does not establish zero operating cost or profitability.

Record per-active-user parsing, retrieval/licensing, model, document export, photo, storage and support costs. Track photo conversion and margin separately; do not assume photo sales subsidize the career service until observed. Beta capacity is bounded by the operating budget and measured cost per active user. Any future career price, premium limit, sponsorship or organizational licensing offer requires its own evidence and decision. No live Stripe prices are created during planning.

Recurring monitoring could later support a subscription if returning users value changed opportunity evidence and roadmap follow-through. Opt-in frequency, notification controls, meaningful-change detection and unsubscribe behavior would be part of that scope.

## Delivery sequence and acceptance gates

The full pivot requires all slices below. A shell or generic chatbot alone does not satisfy it. Estimates wait until data-provider access and the first vertical slice establish the work involved.

| Slice | Concrete outcome and acceptance | Depends on |
| --- | --- | --- |
| 0. UX and evidence spike | Prototype the six core pages and agent panel; inspect provider rights/cost; reproduce a cited role/location report; validate navigation and understanding with target users | Current planning |
| 1. Goal → confirmed profile | Agent entry, private resume ingestion, edit/confirm extracted facts, persist goal, resume after reload; one profile cannot be read by another user | 0 |
| 2. Profile → market brief | One bounded durable run produces national/local wage evidence, plausible role options and uncertainty; simulated outage gives honest partial results | 1, data access |
| 3. National exploration → target | Map and accessible table compare eligible geographies; separate wages/postings/outlook; user selects target and can inspect sources | 2 |
| 4. Target → career action kit | Versioned targeted resume + summary + editable 30/60/90-day roadmap; export usable documents; no invented accomplishments | 2; 3 for geography-specific selection |
| 5. Career kit → photo offer | Agent hands off to photo workspace, respects consent/allowances, returns selected export to kit; no duplicate charge on retry | 4; existing fulfillment |
| 6. Free-core beta and same-domain pivot | Validate quota/resume behavior, retention/deletion, cost, accessibility, optional photo fulfillment and phased routing; publish only substantiated claims | 1–5, measured usage limits |

Draft ticket boundaries mirror these outcomes and must include blocking edges and acceptance criteria when published. Do not split into database-only, API-only and UI-only tickets that postpone a usable journey.

## Tests, evaluation and release criteria

Prefer observable behavior at the authenticated API and browser seams. The repository already has a CustomWebApplicationFactory for API integration tests and Playwright coverage for photo purchase, promoted preview and recovery; extend those patterns. Use SQL Server for transaction/concurrency claims rather than relying on an in-memory database substitute.

- Intake: valid PDF/DOCX/text, scanned and unreadable documents, conflicting dates, partial histories, file size/type restrictions, user correction and deletion. Unsupported extraction offers a clear manual path.
- Analysis: occupation ambiguity, suppressed wages, incompatible taxonomies, stale sources, zero salary observations, misleading remote eligibility, duplicated postings and source disagreement. Store deterministic fixtures and source versions.
- Agent: prompt injection in a resume/posting, cross-user tool arguments, unknown tool names, cancellation, browser reconnect, provider timeout, cost cap, repeated requests and profile-version changes.
- Materials: unsupported accomplishments remain absent, user edits persist, exports render correctly, links resolve, and a roadmap fits the user's available effort.
- Photo integration: old purchase recovery and private preview delivery remain valid; a career run cannot consume an unauthorized photo entitlement; photo score never changes career fit or pay.
- Accessibility: keyboard and screen reader flow; visible focus and task updates; mobile resumability; map/table parity and usable export controls.
- Privacy: resume and derived artifacts are private; deleting a career profile removes its derived content/indexes under the defined retention policy; required billing records remain separate. Restore from backup must not resurrect deleted user content into the active app.

Recommended beta gates, to be calibrated during slice 0: every displayed numeric market claim has a source/definition/date; zero fabricated qualifications or unauthorized side effects in the evaluation set; complete core journey for representative cases across at least three occupation families and multiple geographies; personalized comparable-pay ranges are reproducible for covered cases, with explicit fallback for sparse cases; source outages produce partial/unavailable states; no regression in paid photo delivery. Assess helpfulness with real target-user review, not only automated tests.

## Planning coverage review

The planning artifact and the future application have different completion criteria. The release tests above specify future implementation work; they have not been run against a career application in this planning session.

| User requirement | Evidence in the plan | Current planning status |
| --- | --- | --- |
| Use Matt's skills | Decision rounds, canonical terminology, ADR-0006, delivery slices and proposed behavioral testing seams | Applied; user confirmed the six-page brief on 2026-09-22 |
| Resume and professional background | Confirmed-profile journey, extraction/correction rules, Profile page and intake tests | Specified |
| National market analysis | Source strategy, occupation/geography matching, national coverage acceptance and Heatmap page | Specified; live provider validation belongs to the data spike |
| Estimate earning potential | Personalized comparable cohort, deterministic range, evidence criteria, benchmark fallback and Analytics page | Specified without a false precision claim |
| Career guidance | Role alternatives, evidence gaps, editable milestones, materials and Roadmaps page | Specified |
| Photos within the offer | Materials integration, optional paid allowances, resume/photo separation and existing delivery preservation | Specified |
| Exclude financial product features | Product direction and capability exclusions | Explicit |
| Agent-first approach | Shared goal/conversation, focused tools, durable runs and contextual actions on six pages | Specified |
| Keep existing name/domain | Confirmed decisions, public-site plan and ADR-0006 | Confirmed |
| Free core with optional paid photos | Commercial model, quota behavior, basic exports and optional photo journey | Confirmed direction; quota amounts intentionally await cost evidence |
| UX pages from all reference images | Six-page blueprint with first viewports, actions, states, navigation and mobile behavior | Written; structure confirmed by the user on 2026-09-22 |
| Use Impeccable | Installed WSL skill, context loading, Shape/Operate guidance and page brief | Applied at planning scope; no mockups or UI build claimed |

The user confirmed the six-page brief on 2026-09-22. The requested planning deliverables are complete. The next design phase is visual direction and a connected prototype within this scope; it is not a missing planning artifact or an implemented capability. No additional monetization or domain decision is needed to begin that phase.

Track intake-to-confirmation, first useful brief completion, target selection, roadmap action completion, return use, material exports, photo attachment, paid fulfillment, provider cost and refunds. Treat interview/offer outcomes as optional user-reported measures, not proof of causation. The primary success measure is a user completing a useful career action based on their brief.

## Remaining research and decision register

- Page review: complete; user confirmed the six-page brief. Audience, agent scope, name/domain and free-core direction are settled. Refine the proposed testing seams when turning the plan into implementation specifications.
- Data spike: vendor rights, actual coverage, freshness, payload schema, quotas/cost and ability to keep evidence snapshots. No purchased feed or live integration is yet established.
- Resume spike: select parser/OCR path with extraction quality, file safety, rendering and retention measurements.
- Career-agent spike: choose model/runtime based on tool-use reliability, latency, resume privacy and measured cost; the plan does not require changing the app framework.
- Commercial discovery: free quotas and operating budget first; any new career pricing remains deferred. Preserve existing paid-photo package rights.
- Brand work: keep current name/domain, update descriptor and career positioning. Screenshot colors/mascot are not requirements.
- Implementation readiness: reconcile current worktree ownership and branch from an agreed baseline; do not include unrelated workspace changes in the pivot.

## Evidence inspected

Repository: AGENTS.md; CONTEXT.md; PRODUCT.md; ADRs 0001–0005; app routes and UI dependency manifest; API project target; ApplicationDbContext; UserProfile; OutcomePackageDefinition; RetentionPolicyService; IHeadshotGenerationService; API integration test factory; existing UI test inventory.

Visual references: the initial Lossdog screenshot and four additional Analytics/Profile/Heatmap/Roadmap screenshots inspected directly on 2026-09-22. External source links above were researched on that date. This is a product/technical plan, not evidence of an implemented career product or validated salary prediction model.
