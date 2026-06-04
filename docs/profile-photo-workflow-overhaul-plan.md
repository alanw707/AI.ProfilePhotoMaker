# Profile Photo Workflow Overhaul Plan

Date: 2026-05-17
Status: Accepted for local Docker implementation

## Product direction

AI.ProfilePhotoMaker will move from a generic AI headshot generator toward a professional profile photo workflow. The product should help an individual professional get one excellent usable profile photo package: score the current photo, generate a focused candidate set, select the best shot, refine or augment it, and export platform-ready assets.

Primary promise:

> Your best professional profile photo, from one upload.

Supporting promise:

> Score, improve, and export a LinkedIn-ready headshot in minutes — no photoshoot or model training required.

## Canonical terms

Use the glossary in `CONTEXT.md`.

Key terms:

- Instant headshot
- Professional profile photo workflow
- Professional profile photo package
- Outcome package
- Credit ledger
- Photo workspace
- Profile photo score
- Best shot selector
- Platform export kit
- Photo adjustment
- Premium augmentation
- Outfit upgrade
- Creative style pack
- Advanced custom photoshoot pack

## Product principles

- Compete on confidence and convenience, not raw headshot volume.
- Hide credit language from the primary user experience; keep credits as the internal ledger.
- Keep Replicate custom model training hidden as a legacy/fallback advanced capability.
- Keep creative styles as a friendly secondary path, not the main product promise.
- Prefer one-photo, instant, role-aware workflows.
- Show value before purchase where possible.

## Target buyer

Primary buyer: individual professional who needs one excellent LinkedIn/profile photo quickly.

Initial role-specific workflows:

- General professional
- Founder / executive
- Tech / engineering
- Healthcare / clinical
- Realtor / sales
- Creative / creator
- Legal / lawyer
- Finance

Role is selected before generation and remains editable after the first result.

## Outcome packages

### Free Preview

- Source photo score
- One same-quality watermarked instant preview
- Creative style pack access
- No platform export package

### Starter Package

- Three generated candidates
- Best shot selector
- Basic photo adjustment
- Selected platform export kit
- One or two refinements

### Pro Package

- Nine generated candidates
- Best shot selector
- Profile score delta
- Selected platform export kit
- Three to five refinements
- Two or three premium augmentations
- Extra role or vibe attempts

## Feature roadmap

### 1. Product IA and copy overhaul

- Reposition homepage around profile photo score, best shot, and export package.
- Replace training/model/credit language in primary UX.
- Use primary CTA: `Get my profile photo score`.
- Use secondary CTA: `Try creative styles`.
- Rename gallery-facing concepts toward `Photo workspace` where practical.

### 2. Outcome package model in UI

- Present Free Preview, Starter Package, and Pro Package.
- Hide raw credits from pricing and primary Photo Workspace.
- Keep credit ledger internally for provider cost, package consumption, refunds, and admin/debug.

### 3. Photo workspace

The workspace should contain:

- source photo
- generated candidates
- score per candidate
- selected best shot
- refinements
- premium augmentations
- platform exports
- package download

### 4. Profile photo score V1

Use a hybrid scoring model:

- deterministic checks where practical: face presence, resolution, blur, brightness, crop/framing
- AI/rubric checks for subjective dimensions: professionalism, approachability, confidence, attire, role fit, platform fit

Show:

- overall professional-readiness score
- subscores
- plain-English feedback
- improvement delta after generation for paid packages

### 5. Best shot selector

- Rank generated candidates for the selected role/platform.
- Explain why the recommended photo is strongest.
- Let the user override the recommendation.

### 6. Platform export kit

Allow user-selected exports for:

- LinkedIn profile
- LinkedIn banner-safe square/avatar
- Gmail / Google avatar
- Slack / Teams avatar
- GitHub avatar
- Resume headshot
- Website bio / speaker profile
- Original high-resolution image

Primary action: `Download package`.
Secondary action: single-image download.

### 7. Photo adjustment

Start with post-generation adjustments:

- crop
- zoom
- rotate
- brightness
- contrast
- sharpness
- reset/revert

Avoid building a full Photoshop clone.

### 8. Premium augmentation

Paid generative edits beyond basic photo adjustment:

- relighting
- professional polish / face retouching
- outfit upgrade
- background upgrade

Boundaries:

- preserve identity
- avoid credential impersonation
- avoid sexualized clothing
- avoid drastic body changes
- avoid luxury/status deception

Pro packages include limited premium augmentations; extras can be purchased as add-ons.

### 9. Creative style pack

- Keep playful styles as a secondary link.
- Do not paywall by default.
- Rate-limit for provider-cost control.
- Do not let creative styles distract from professional profile photo workflow.

### 10. Teams later

Potential future team product:

- invite employees
- company brand kit
- consistent background/style
- admin download and review
- compliance/security positioning

## Backend implications

- Keep `/api/headshots/generate` as the instant generation path.
- Add scoring service abstraction for profile photo score.
- Add workspace/package state model or extend existing image/package records carefully.
- Add export generation endpoints for selected platform formats.
- Keep credits as internal ledger while exposing outcome package state externally.
- Keep Replicate routes/services for hidden advanced custom photoshoot pack.
- Preserve provider/model metadata for audit and provider comparisons.

### Package and data model

Use a separate outcome layer above the current credit tables.

`OutcomePackageDefinition` is the source of truth for user-facing package content. V1 fields: `Id`, `Code`, `Name`, `Description`, `Price`, `Currency`, `StripePriceId`, nullable `InternalCreditPackageId`, `IncludedCandidateCount`, `IncludedRefinementCount`, `IncludedPremiumAugmentationCount`, `IncludesPlatformExportKit`, `IncludesScoreDelta`, `IsActive`, and `DisplayOrder`.

`UserPackageEntitlement` records a user's right to use a package. V1 fields: `Id`, `UserId`, `OutcomePackageDefinitionId`, nullable `SourcePaymentTransactionId`, `Status`, `RemainingPackageUses`, `RemainingCandidates`, `RemainingRefinements`, `RemainingPremiumAugmentations`, `PlatformExportKitAvailable`, `ActivatedAt`, nullable `ConsumedAt`, nullable `ExpiresAt`, `CreatedAt`, and `UpdatedAt`.

During migration, `CreditPackage` remains the checkout/ledger bridge. A successful purchase grants internal credits and creates the corresponding `UserPackageEntitlement`.

## Frontend implications

- Rework Photo Workspace around photo workspace and package progress.
- Rework pricing around outcomes, not credits or output counts.
- Add role selection before generation.
- Add score display before and after generation.
- Add candidate comparison/best shot selector.
- Add export picker and download package flow.
- Move creative styles to a secondary route/link.
- Keep advanced custom photoshoot pack hidden behind feature flag/admin/debug.

## Tests

Add or update tests for:

- package/outcome copy replacing credit/training language
- runtime flag behavior
- scoring service deterministic checks
- best shot selector ranking contract
- export kit selected outputs
- download package creation
- premium augmentation entitlement/add-on behavior
- Replicate advanced path remains hidden/preserved

## Rollout

Recommended flags:

- `Features:ProfilePhotoWorkflowOverhaul`
- `Features:OutcomePackagesVisible`
- `Features:ProfilePhotoScoreVisible`
- `Features:CreativeStylePackVisible`
- `Features:PremiumAugmentationsVisible`
- existing `Features:ReplicateTrainingFlowVisible`

Roll out in stages:

1. Copy/IA only
2. Outcome package UI with credit ledger hidden
3. Photo workspace shell
4. Score V1
5. Best shot selector
6. Export kit
7. Premium augmentation add-ons
8. Team workflow exploration

## Non-goals for first pass

- Delete Replicate backend.
- Remove internal credit ledger.
- Build a full Photoshop clone.
- Launch teams product.
- Promise scientific scoring certainty.
- Add credential-specific outfit impersonation.
