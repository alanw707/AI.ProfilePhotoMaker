# Career agent UX and page blueprint

Status: six-page UX brief confirmed by the user on 2026-09-22, using Impeccable `shape`. This is a planning artifact, not implemented or browser-verified UI. Product decisions follow [the pivot plan](career-agent-pivot-plan-2026-09-22.md).

## Experience thesis

Experienced U.S. professionals arrive with a practical question: "What should I do next, and what evidence supports it?" The application helps them understand their background, evaluate opportunities and act. Six primary pages carry the work. The agent remembers the confirmed goal and helps within every page; conversation does not replace the usable pages.

Keep AI Profile Photo Maker and aiprofilephotomaker.com. Add the descriptor **Your personal career assistant** to explain the broader purpose. The career core is free within transparent limits; photos are an optional paid capability. No financial portfolio, investing, trading, net-worth or lifetime-loss dashboard.

Visitor modes: signed-in workspaces **Operate**; public homepage **Persuade**; saved reports **Read**. These modes apply to this brief rather than changing global design rules for unrelated surfaces.

## Page map

| Page | Proposed route | Question it answers | Main action |
| --- | --- | --- | --- |
| Career agent | `/app/career` | What should I work on next? | Start or continue the next useful task |
| Career profile | `/app/career/profile` | What does the assistant know about me? | Confirm or update professional evidence |
| Career analytics | `/app/career/analytics` | How do I fit the market and what could I earn? | Compare and choose a target role |
| Career heatmap | `/app/career/heatmap` | Where are the relevant opportunities? | Compare or save an eligible location |
| Career roadmaps | `/app/career/roadmaps` | How do I reach my target? | Choose a path and complete its next action |
| Career materials | `/app/career/materials` | What have I prepared and what can I use? | Review, improve or export a material |

Supporting routes: guided setup (`/app/career/setup`), saved report (`/app/career/reports/:id`), specific roadmap (`/app/career/roadmaps/:id`), and material detail (`/app/career/materials/:id`). These open from the six pages; they do not add six more sidebar entries. Routes are proposals for implementation, not current features.

Existing `/app/enhance` remains the canonical photo editor and `/app/gallery` remains reachable for existing photos. Career materials links to them and preserves the return destination. Settings, usage/privacy and Help live at the bottom of navigation. Admin remains separate. The existing `/app` default can change to Career agent when the pivot is enabled, with a visible continuation link for unfinished photo packages.

## Shared shell and assistant

Desktop: labeled left navigation, a generous central workspace, and a collapsible right assistant panel. A compact top context row shows the current career goal and selected role/location when relevant. Do not repeat four large salary cards on every page. Monetary context belongs primarily in Analytics and the Heatmap.

On the Career agent page, the conversation and next-action summary are the main workspace. On Profile, Analytics, Heatmap, Roadmaps and Materials, the artifact leads and the assistant is contextual support. Opening "Explain this range" identifies the actual range and source in the panel; it does not make the user describe the screen again.

Keep one conversation associated with the active goal across navigation, with an explicit new-conversation action and history inside the agent page/panel. A change of page changes visible context, not the user's saved goal or factual profile. Page actions send explicit context to the agent; avoid hidden selections being mistaken for user approval.

Agent actions produce visible, reviewable changes: a proposed profile edit, a new report version, a resume diff or a roadmap task. The user can accept, edit or dismiss proposals. Never overwrite human-edited materials silently. Show saved status and allow recovery of the prior version.

The assistant shows concise task steps, sources and outputs. A persistent run indicator allows **View progress**, **Cancel**, or **Answer a question**. Explain partial completion with a recovery action. No unbounded spinner, invented percentage or private model-reasoning transcript.

Responsive behavior: at narrow widths use a navigation drawer and a full-height assistant sheet with **Back to workspace**. Keep one main scroll area and preserve the workspace's scroll position. On tablet, opening the agent may overlay rather than squeeze the central editor. Avoid an icon-only navigation rail unless labels are readily available and selection remains obvious.

## 1. Career agent

First viewport for a new user: "What would you like to change about your career?", resume upload/manual entry, and three example intents: **Find my next role**, **Understand my pay**, **Plan a career change**. Show a short privacy explanation before document submission. These prompts start work, not separate products.

For a returning user: current goal, latest brief, one recommended action with its reason, and active or interrupted tasks. Prior conversations and completed reports remain secondary. Example: "Your market brief is ready. Compare the two role directions before I tailor your resume."

Primary interaction: a proposed plan becomes a tracked run, then produces a saved result with **Open report**, **Compare locations**, or **Build my roadmap**. The home links the entire journey rather than competing with the specialist pages.

States: no profile; profile awaiting confirmation; run queued/working; user answer needed; partial result; completed; cancelled; provider unavailable; free quota reached. Quota exhaustion preserves access to existing work and shows the actual reset time when known.

## 2. Career profile

First viewport: compact identity and optional headshot, professional headline, latest resume and confirmation status, plus **Update profile**. An optional photo never occupies half the screen or pushes professional experience out of view.

Sections in useful reading order: current goal and location preferences; professional summary; work experience and accomplishments; skills and supporting evidence; education/certifications; projects and work samples; optional community work and profile links. Contact information stays private by default and is not repeated in every view.

Upload a new resume, compare extracted changes to the confirmed profile, then accept individual changes or confirm all supported facts. Each fact can show **From resume**, **Added by you**, or **Needs confirmation**. Resolve contradictory dates and responsibilities without silently picking one.

Completion is an honest checklist of information needed for the chosen task: for example, a target location is missing. It is not a grade of professional quality. Missing certifications or volunteer experience are not automatically deficiencies.

Agent actions: **Help write my summary**, **Ask about this experience**, **Find missing evidence**, **Review my profile changes**. Editing the profile marks affected reports as out of date while retaining their previous versions.

## 3. Career analytics

First viewport: selected occupation and geography, evidence date, and a clear compensation comparison. Use three directly labeled concepts: occupational wage benchmark, personalized comparable-pay range when available, and the selected target-role scenario. The personalized range links to the matching responsibilities, role level, location and observations; it does not merely rename a national average. Source definitions distinguish wages from base salary or total compensation. A requested salary is a preference, displayed separately.

Follow with role fit and evidence gaps, then skills and market context. Prefer a labeled comparison chart or evidence table over a radar diagram with unexplained scales. If a radar is later used, its scales and scoring method must be consistent, inspectable and matched by a readable table.

A strengths/gaps view relates each requirement to actual experience and source evidence. Distinguish **not provided**, **needs validation**, and **known gap**. Never derive competence from profile completeness. Do not copy the reference's overall career grade or precise personal dollar value without a defensible measurement model.

Agent actions: **Explain this estimate**, **Compare another role**, **What would strengthen my case?**, **Make this my target**. Selecting a target proposes a roadmap, preserving the source assumptions that produced it.

Sources open in a detail drawer with measure, geography, period, sample/coverage and limitations. Unsupported personalization falls back to an explicitly labeled occupational benchmark.

AI impact is a possible later subsection about exposure of job tasks, backed by a named, reviewed methodology. The supplied screenshot alone does not establish one. Do not ship a guessed layoff probability or career-risk grade.

## 4. Career heatmap

First viewport: role selector, search location, work arrangement and relocation filters, metric selector, map legend and an equivalent results table. Compensation is the default suggested metric. Demand, employment concentration and projected outlook are separate selectable measures rather than one unexplained opportunity score.

Map uses U.S. state/metro boundaries appropriate to the data. Include Alaska/Hawaii and other source-covered areas accessibly; show the actual geographic coverage. Missing or suppressed values appear as **Data unavailable**, with a distinct pattern/label rather than the lowest-value color.

Selecting a region opens a summary with pay evidence, available posting observations, remote restrictions, date and **Compare** / **Save to goal**. Allow a compact comparison of up to three regions as the initial interaction recommendation. Avoid aggregating mutually overlapping metro and state counts.

Agent actions: **Explain this location**, **Compare with my current market**, **Find eligible remote options**, **Update my roadmap for this market**. A saved destination changes preferences only after confirmation.

On mobile, lead with search and a ranked list, with a **Map** toggle. A usable result never depends on tapping a tiny polygon. The table supports keyboard navigation, named sorting and the same filters as the map.

## 5. Career roadmaps

First viewport before choosing a path: target outcome and a comparison of plausible alternatives—**Closest fit**, **Higher ambition**, and **Steadier transition**—only where the user's situation supports distinct alternatives. Do not force three routes when evidence supports fewer.

Each option states role/goal, supporting experience, missing evidence, time/effort assumptions, dependencies and trade-offs. Timelines are editable scenarios, not promised promotions. Compensation comes from the selected target-role evidence, not an invented increase.

After selection: show **This week** first, then 30/60/90-day milestones and any longer horizon. Tasks have a concrete output, rationale, effort estimate, dependency and status. Examples: document a project result, tailor a resume to a role, prepare interview examples, or review a relevant credential requirement.

Keep **What the agent knows** available in an expandable facts section, as in the reference. Changes go through the same confirmed-profile workflow. Assistant suggestions do not automatically reschedule or delete human-authored tasks.

Agent actions: **Adjust to my available time**, **Help me do this task**, **Explain why this matters**, **Replan from my progress**. Completion updates the roadmap and records supporting work; it does not automatically raise a salary estimate.

## 6. Career materials

First viewport: materials grouped by target role with latest versions, saved state and readiness for review. Tabs or sections: **Resumes**, **Professional summaries**, **Photos**. Keep report links and roadmap context attached to the relevant target.

Resume editor shows source facts, a draft and a reviewable change history. Agent drafting preserves actual accomplishments and asks for evidence where details are missing. Basic document export belongs to the free career core; do not present an unsupported ATS compatibility guarantee.

Photo section offers **Use my photo**, **Improve my photo**, or **Create a professional photo**. Link to the existing workspace and show exact paid allowances when needed. Returning from payment or editing restores the selected career goal. No photo score or appearance trait feeds pay or role analysis.

Default resume exports omit the headshot; photos are separate assets the user explicitly chooses to include where appropriate. Career materials remain useful without buying photos. Existing customers retain their photo downloads and package fulfillment.

Agent actions: **Tailor this resume**, **Improve this summary**, **Prepare my profile photo**, **Export my materials**. Public sharing, publication and external submission are excluded from the first release; exports remain a user-controlled action.

## Public and supporting pages

Homepage at the existing `/`: existing name plus career descriptor, clear career promise, one starting action, a labeled example of a career brief, an explanation of the agent workflow, and a secondary photo entry. Keep the current name readable; don't make users infer the pivot from the domain.

**How it works** can be a homepage section initially. **Photos** and **Photo packages** explain the existing optional paid capability; existing `/pricing` must state that it describes photo packages, not the free career core. Existing photo-oriented acquisition/SEO pages remain accurate and link to both relevant journeys.

Reuse login, signup and recovery. Guided setup supports upload or manual input; asks only for the information needed for the first brief; shows extracted facts for confirmation; saves progress. Settings includes privacy, saved profile/memory, resume removal, account deletion/export, usage and notification controls. Existing support and legal pages must be reviewed for the new data processing before launch.

Saved career report: readable, dated, versioned and exportable, with role/location assumptions, sources, range definitions and next steps. It is a detail view of agent work rather than another dashboard. A report remains readable after a new profile version, with a visible outdated indicator.

## Impeccable design guidance

The installed WSL skill was loaded with its context command, `shape`, `new-work`, and Operate guidance. Product purpose, audience and behavior were established through the prior user answer rounds. The current site's source and DESIGN.md were inspected. No new installation is needed.

The screenshots pin the career page family and companion-agent relationship. They do not explicitly pin Lossdog's yellow/black palette, mascot, exact numbers or component geometry. Keep the existing logo/name and reusable typography/controls as the baseline for the page plan. The current Studio Proof Desk composition is specific to the photo workspace; portraits should not dominate market analysis.

For the page prototype, use task-oriented hierarchy: contextual heading, one important result, clear next action, then supporting detail. Carry the same navigation, evidence labels, edit affordances and status language across all six pages. Avoid repeated hero-size salary cards, equal-weight boxes for every fact, completion rings without meaning, decorative agent avatars, and an empty-chat first impression.

This approved brief defines information architecture and behavior. A final palette/theme, cross-page visual system, and compositional mockups are the next design deliverable. Run Impeccable's visual-direction process for any replacement visual world; preserve confirmed branding. Do not claim the existing DESIGN.md already defines a finished career UI or replace it with an unbuilt design system.

## States, content range and accessibility

- Profile must support no resume, one role, long multi-role histories, incomplete dates, long titles and repeated edits without losing facts.
- Evidence can be available, incomplete, stale, unavailable or conflicting; all have readable text and a next action. Unknown is never displayed as zero.
- Preserve user input on failures and reconnects. Do not navigate away from an edited document when an agent task finishes.
- Numeric values use consistent units, period, definitions and rounding; examples remain explicitly illustrative. Do not reuse the screenshots' personal data as demo content.
- Support desktop, tablet and narrow phone layouts, both existing theme preferences where applicable, 200% zoom, keyboard navigation, visible focus, named controls and screen-reader progress updates.
- Use at least 44px interaction targets, readable contrast and reduced-motion support. Maps/charts need equivalent text/table access.
- Scope of permission and paid allowance appears at the action that needs it. Declining a photo purchase leaves all career work available.

## Prototype validation and handoff

Next deliverable: one connected prototype spanning all six pages with clearly synthetic professional data. Start with Profile → Analytics → Heatmap → Roadmap, with the assistant persisting across the flow; connect Agent home and Materials into that same journey. Include empty/loading/partial/failure examples and the mobile assistant transition. Do not mistake standalone attractive screenshots for a tested experience.

Observe target users completing five tasks: correct a resume fact; explain what a pay range means; compare two eligible markets; select and edit a roadmap; prepare materials without buying a photo. Record misunderstandings and navigation failures before backend expansion. Check keyboard and mobile versions in the same bounded review pass.

Implementation testing seams: existing authenticated API for saved career facts/runs/evidence/materials and Playwright for end-to-end navigation, edits, run resumption, private data boundaries and photo handoff. Market math and source mappings receive deterministic fixtures. User research assesses whether the experience is useful and understandable.

The user explicitly confirmed the six-page brief on 2026-09-22, satisfying Impeccable `shape`'s confirmation checkpoint. No application code or visual mockup was produced in this planning pass. The [approved implementation plan](career-agent-implementation/README.md) makes prototype validation ticket 01 and a blocker for dependent production UI; the full product scope remains the pivot plan, not just the prototype.
