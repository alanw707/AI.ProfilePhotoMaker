# GPT Image 2 Pack Strategy Review and Implementation Plan

Source reviewed: `origin/feature/gpt-image-2-pack-strategy:docs/product/gpt-image-2-pack-strategy-plan.md`.

## Verdict

The strategy is directionally valid, but should be implemented as a vertical outcome-package layer on top of the existing preview-first `/enhance` workflow, not as a wholesale replacement of the current Free Preview / Starter Package / Pro Package model.

The strongest part of the strategy is the move from generic AI headshots to use-case-specific finished assets: LinkedIn/executive, realtor, and founder/press kit. This aligns with existing ADRs that say the product should compete on profile-photo workflow outcomes rather than raw headshot volume.

The risky part is the proposed output volume and pack breadth. Several proposed packs promise 8–15 images plus exports. Current implementation and docs emphasize smaller candidate sets, candidate reuse from Free Preview, scoring, refinement, export kits, and cost control. The plan is valid only if vertical packs are modeled as curated deliverables and recipes, not as more image volume.

## Domain alignment

Existing canonical terms:

- `Outcome package`: user-facing paid result unit.
- `Free Preview`: same-quality watermarked preview before upgrade.
- `Photo workspace`: primary workspace for source photo, variants, scores, selected best shot, refinements, augmentations, and exports.
- `Portrait style`: current style/prompt selection unit.
- `Platform export kit`: platform-ready crops/download assets.

Proposed term:

- `Vertical outcome pack`: an outcome package tailored to a concrete business use case, such as Realtor Pack or Founder Press Kit Pack.

This avoids introducing a vague competing term `pack` that could conflict with existing `Outcome package` and `Platform export kit`.

## Strategy corrections before implementation

1. Keep Free Preview first.
   - Do not start the user on a paid pack picker.
   - The first interaction should still produce a watermarked preview, then upsell the relevant vertical pack.

2. Do not lead with six packs.
   - Launch with three: LinkedIn / Executive, Realtor, Founder / Press Kit.
   - Keep Job Seeker, Dating, and Team in validation backlog.

3. Reduce promised output counts for self-serve MVP.
   - Current Starter = 3 candidates, Pro = 9 candidates.
   - Map vertical packs to this structure first.
   - Concierge can promise higher-touch delivery, not necessarily higher automated output count.

4. Separate recipe count from final deliverables.
   - Recipe variants can generate candidate diversity.
   - User-facing deliverable should be the selected best shot plus export kit, not dozens of raw outputs.

5. Treat Team/Company Pack as later.
   - Team requires account/team model, shared brand style, bulk workflow, admin UX, and support operations.
   - Do not mix it into MVP.

6. Validate demand before schema expansion.
   - Use current `OutcomePackageDefinition` where possible.
   - Add minimal recipe metadata only after the landing-page and checkout tests show traction.

## Recommended MVP offer structure

### Free Preview

- Upload and quality gate.
- One watermarked preview.
- Suggested vertical pack based on selected use case.
- Resume support if user refreshes or returns later.

### LinkedIn / Executive Pack

- Price target: $39–$49 self-serve.
- Uses current Starter/Pro mechanics initially.
- Deliverables:
  - 3 or 9 candidates depending tier.
  - Best LinkedIn / Best executive / Best approachable labels.
  - LinkedIn profile crop, square avatar, resume crop.
  - Limited refinements.

### Realtor Pack

- Price target: $59–$99 self-serve.
- Deliverables:
  - Business-trust portrait candidates.
  - Realtor/Zillow crop, flyer crop, square social crop.
  - Background recipes: modern office, neutral studio, upscale interior.
  - Labels: Best Zillow profile, Best luxury vibe, Best trust photo.

### Founder / Press Kit Pack

- Price target: $149–$299; concierge option $499+.
- Deliverables:
  - Headshot candidates plus one hero/banner asset in MVP, not the full 8+4+2 plan initially.
  - Press/podcast avatar crop, website/team-page crop, LinkedIn/X banner crop.
  - Concierge review option.

## Implementation plan

### Phase 0 — Validation landing pages, no backend schema change

Goal: test whether vertical offer positioning converts.

Tasks:

1. Add product landing pages:
   - `/use-cases/linkedin-executive-profile-photo`
   - `/use-cases/realtor-profile-photo-pack`
   - `/use-cases/founder-press-kit-photo-pack`
2. Add pricing cards that use current checkout bridge.
3. Track analytics events:
   - vertical page view
   - CTA click
   - checkout start
   - purchase success
   - preview generated
   - paid generation confirmed
4. Add example-output slots using existing sample images or generated safe placeholders.
5. Do not create new database tables in this phase.

Exit criteria:

- At least one vertical gets materially better CTA rate than generic pricing.
- At least one paid checkout path is used in local/manual validation.

### Phase 1 — Add vertical intent to current workflow

Goal: attach a use case to the existing Free Preview -> Starter/Pro flow.

Tasks:

1. Add `PackIntent` or `UseCaseCode` client state:
   - `linkedin_executive`
   - `realtor`
   - `founder_press_kit`
2. Pass it through `/app/enhance` from landing page query params.
3. Store selected intent in session/local draft so payment return/resume preserves it.
4. Show vertical-specific copy in `/enhance`:
   - upload tips
   - style recommendations
   - result labels
   - export suggestions
5. Keep existing `PackageCode` values initially: `free_preview`, `starter_package`, `pro_package`.

Exit criteria:

- A user can enter from each vertical page and see tailored `/enhance` copy.
- Existing preview-first package flow still works.

### Phase 2 — Recipe-based generation, minimal schema

Goal: convert vertical intent into controlled prompt recipes.

Tasks:

1. Add static server-side recipe registry first, not database tables:
   - use case
   - scene/background
   - outfit direction
   - framing/crop
   - expression/tone
   - identity constraints
   - negative constraints
   - export target
2. Extend `HeadshotGenerationRequestDto` with optional `UseCaseCode` and `RecipeCode`.
3. Update `HeadshotGenerationService` prompt building:
   - base portrait style prompt
   - plus vertical recipe modifier
   - plus identity/realism constraints
4. Use deterministic recipe selection by candidate index so retries are stable.
5. Add tests proving same request produces same recipe/correlation set.

Exit criteria:

- Realtor/Founder/LinkedIn candidates differ by recipe, not by random generic prompting.
- Idempotency still returns same candidates on retry.

### Phase 3 — Result labels and curation

Goal: make outputs feel like a finished pack instead of a gallery dump.

Tasks:

1. Add client-side labels from recipe metadata:
   - Best LinkedIn profile
   - Best executive look
   - Best Zillow profile
   - Best press bio
2. Reuse current score/ranking where available.
3. Add manual override/select best shot in the Photo Workspace.
4. Show vertical-specific next action:
   - Download LinkedIn kit
   - Download realtor kit
   - Prepare founder press kit

Exit criteria:

- Results grouped and labeled by use case.
- User can identify and export the best shot without understanding prompts.

### Phase 4 — Export kit expansion

Goal: make each vertical pack deliver platform-ready assets.

Tasks:

1. Extend existing platform export kit presets:
   - LinkedIn profile crop
   - square avatar
   - resume crop
   - Zillow/Realtor crop
   - flyer crop
   - podcast avatar
   - website bio crop
   - LinkedIn/X banner crop
2. Keep deterministic crops initially.
3. Bundle outputs into ZIP using existing export path.
4. Gate exports by entitlement.

Exit criteria:

- Each first-three vertical pack has at least 3 useful exports.
- Download package works from selected best shot.

### Phase 5 — Checkout/product model refinement

Goal: make package definitions source of truth once demand is proven.

Tasks:

1. Decide whether verticals become:
   - separate `OutcomePackageDefinition` records, or
   - same Starter/Pro records with `UseCaseCode` metadata.
2. If separate records, add fields:
   - `UseCaseCode`
   - `DisplayCategory`
   - `DefaultRecipeSetCode`
   - `LandingPageSlug`
3. Preserve `InternalCreditPackageId` bridge only until checkout migration is complete.
4. Update entitlement display to avoid confusing locked/no-generations-left states.

Exit criteria:

- Product/pricing UI can explain what user bought without hidden credit language.
- Backend can validate use-case entitlement cleanly.

### Phase 6 — Concierge overlay

Goal: monetize higher-intent users without over-automating.

Tasks:

1. Add concierge CTA to Founder and Realtor pages.
2. Capture request fields:
   - role/business
   - target platforms
   - preferred vibe
   - deadline
   - notes
3. Create manual fulfillment admin checklist or support workflow.
4. Do not build full admin dashboard yet.

Exit criteria:

- Concierge request can be purchased or submitted.
- Manual delivery path exists.

## Key risks

1. Cost risk from over-promising 10–15 images.
   - Mitigation: keep automated candidate count tied to Starter/Pro limits; sell curation/export value.

2. Product confusion between package, pack, style, recipe, and export kit.
   - Mitigation: canonicalize `Vertical outcome pack`; keep `Portrait style` and `Platform export kit` distinct.

3. Identity preservation failures.
   - Mitigation: strict upload gate, visible preview-first flow, limited paid retry policy, avoid claims of exact likeness.

4. SEO/landing pages without operational delivery.
   - Mitigation: start with three pages and manual concierge only where needed.

5. Schema churn before validation.
   - Mitigation: static recipe registry first; database expansion after conversion signal.

## Recommended first implementation slice

Build only this first:

1. Add three vertical landing pages.
2. Add `useCase` query param into `/app/enhance`.
3. Tailor `/enhance` copy and recommended portrait styles by use case.
4. Add static recipe registry and use it for paid candidates only.
5. Add result labels from recipe metadata.
6. Extend export presets for LinkedIn, Realtor, Founder.
7. Add analytics for vertical funnel.

Do not implement Team, Dating, full recipe database, full admin, or 15-output packs yet.

## Implementation status

Implemented first slice while preserving Starter/Pro checkout:

- Three vertical landing pages exist under `/use-cases/...`, include showcase placeholder sections, and emit vertical pack page-view analytics in addition to existing SEO CTA analytics.
- `/app/enhance` accepts `useCase` query params for `linkedin_executive`, `realtor`, and `founder_press_kit`.
- `/enhance` shows a vertical use-case picker and applies use-case-specific recommended styles and export defaults.
- `HeadshotGenerationRequestDto` and frontend request models carry optional `UseCaseCode` / `RecipeCode`.
- `HeadshotRecipeRegistry` provides deterministic static recipes for the first three verticals and appends recipe constraints to OpenAI prompts for paid candidate generation only; Free Preview keeps the base portrait prompt.
- Candidate DTOs carry use-case / recipe / label metadata where available.
- Result ranking copy can surface recipe labels such as Best Zillow/Realtor profile and Best press bio.
- Vertical funnel analytics now cover page view, checkout start, purchase-return success, preview generated, and paid generation confirmed with `useCaseCode` and package context.
- Platform export presets now include realtor square, realtor flyer, podcast/press avatar, and founder banner exports.
- The glossary now defines `Vertical outcome pack` as a guided use-case layer inside existing outcome packages.

Deferred by design:

- Team/company product.
- Dating and Job Seeker packs.
- Full recipe database/admin tooling.
- Separate named vertical checkout products.
- 10–15 output promises and full concierge fulfillment system.

## Decision

The first shippable slice will keep the current Starter/Pro checkout packages and make verticals guided use-cases inside `/enhance`.

Separate named vertical products are deferred until one vertical proves conversion. The immediate implementation should treat vertical intent as workflow metadata that influences copy, recipes, labels, and exports while preserving the existing Free Preview -> Starter Package / Pro Package upgrade path.