# Refinement feature investigation

Date: 2026-09-02  
Scope: read-only implementation investigation; no application code changed and no paid/provider generation invoked.

## Executive verdict

Refinement is **partly correct but not consistently modeled**.

The paid-candidate path has a coherent core: a refinement is a one-output regeneration, it spends a refinement allowance instead of candidate allowance or legacy credits, it records which candidate it replaces, and the resumable workspace restores the replacement in the same slot.

However, the product currently has two materially different operations called refinement:

1. **Candidate regeneration** starts from the original uploaded source through `/api/headshots/generate`, preserves the package style/use-case, and replaces a candidate slot in workspace state.
2. **Gallery refinement** edits the selected saved image through `/api/enhancement/enhance`, always applies the `headshot_linkedin` preset, creates an independent `photo_refinement` image, and does not join the candidate replacement chain.

This split explains why the feature is unclear. It also exposes confirmed allowance-accounting, recovery, package-selection, and UI-state defects. The current behavior should not be treated as fully correct until the high-priority findings below are addressed.

## Intended behavior found in repository evidence

The strongest current product sources agree on these invariants:

- Refinement is a restrained finishing step in a professional profile-photo package, after selecting a strong candidate (`CONTEXT.md`, **Professional profile photo workflow** and **Photo workspace**; `PRODUCT.md`, **Operating Context**).
- Candidate generation, refinements, premium augmentations, and exports are independent allowances and must not be presented or consumed interchangeably (`PRODUCT.md`, **Capabilities and Constraints**; `docs/design/app-enhance-flow.md:5-8`).
- The normal package journey exposes refinement after candidate fulfillment and describes the action as **Regenerate selected photo** (`docs/design/app-enhance-flow.md:25`).
- No remaining refinements must not block review or export (`docs/design/app-enhance-flow.md:63-65`).
- A user-requested regeneration spends one refinement, while provider/storage failures should receive a free retry (`docs/plans/portrait-style-enhance-workflow.md:57-60`).
- Recent commits deliberately permit a saved Gallery photo to be refined before the current package's candidate generation is complete (`0455d7f`) and distinguish this state in package-progress copy (`760fd69`).
- Current database package definitions are the executable source of truth: Free Preview 0, Starter 2, Pro 5 refinements (`AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs:601-651`). Purchase/admin grant logic copies those counts into `UserPackageEntitlement.RemainingRefinements` (`AI.ProfilePhotoMaker.API/Services/OutcomePackageService.cs:110-166`; `AI.ProfilePhotoMaker.API/Services/AdminService.cs:270-383`).

The broader product-evolution plan proposes guided quick actions such as “More natural,” “Better lighting,” and “Softer smile” (`docs/product/gpt-image-2-product-evolution-plan.md:180-201`), but no accepted flow document makes those controls a current requirement. The implemented paid-candidate action is only “Try another version / Regenerate selected proof” (`AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/photo-enhancement.component.html:818-839`).

## Canonical vocabulary versus implementation

| Concept | Intended distinction | Current implementation |
| --- | --- | --- |
| Candidate | One owed package output | `RemainingCandidates`; `/api/headshots/generate` with `IsRegeneration=false` |
| Refinement | One paid finishing attempt/replacement | `RemainingRefinements`; either headshot regeneration or Gallery enhancement |
| Premium augmentation | A directed generative edit such as relighting/outfit/background | Separate `RemainingPremiumAugmentations`; `/api/enhancement/enhance` |
| Photo adjustment | Non-generative crop/zoom/rotate/brightness/contrast | Browser preview values, applied during export; no refinement allowance |

“Refinement” is overloaded at the implementation seam. Candidate regeneration and Gallery image-to-image enhancement have different inputs, prompts, persistence, recovery, and entitlement attribution despite sharing the same allowance and nearly identical UI wording.

## Entry points and current end-to-end behavior

### Entry point A: completed package candidate in Photo Workspace

1. The finishing section appears only when candidate fulfillment is considered complete and `PremiumAugmentationsVisible` is enabled (`photo-enhancement.component.html:818`).
2. The user selects a candidate and clicks **Regenerate selected proof** (`photo-enhancement.component.html:823-839`).
3. `regenerateCurrentCandidate()` sets `_nextRequestIsRegeneration=true` and calls the shared generation coordinator (`photo-enhancement.component.ts:1942-1952`).
4. Client gating requires:
   - paid package selected;
   - an enhanced image/source;
   - an active entitlement of that package with a positive refinement count;
   - loaded portrait style;
   - Turnstile when configured;
   - biometric consent;
   - no blocking quality gate (`photo-enhancement.component.ts:596-639`, `929-951`).
5. The client persists a 24-hour interrupted request containing `clientRequestId`, package, source path, style, `isRegeneration`, and `replacesProcessedImageId` (`photo-enhancement.component.ts:1040-1139`, `2020-2057`).
6. It posts one output to `/api/headshots/generate` with `isRegeneration=true` and the selected candidate ID (`photo-enhancement.component.ts:1994-2069`; `headshot-generation.service.ts:70-83`).
7. The API authenticates, requires verified email and Turnstile, then calls `HeadshotGenerationService` (`AI.ProfilePhotoMaker.API/Controllers/HeadshotsController.cs:92-170`).
8. The service validates that the replacement is an owned successful instant-headshot/promoted-preview candidate and that its recorded original source equals the request source (`HeadshotGenerationService.cs:83-103`).
9. It checks a deterministic correlation ID for an already-persisted result, then checks source existence and package refinement allowance (`HeadshotGenerationService.cs:112-189`, `526-545`).
10. OpenAI Images edits the **original source image**, not the selected candidate image. The current portrait style/use-case prompt is used; the selected candidate ID is replacement metadata only (`HeadshotGenerationService.cs:230-291`; `OpenAIHeadshotGenerationProvider.cs:25-50`).
11. The output is stored as a successful `ProcessedImage` with `GenerationMode="instant_headshot"` and `ReplacesProcessedImageId=<selected id>` (`HeadshotGenerationService.cs:270-296`).
12. Only after provider generation and persistence, the service decrements the selected package's refinement allowance (`HeadshotGenerationService.cs:298-315`; `OutcomePackageService.cs:572-590`). Legacy credits cost zero when outcome-package services are active (`HeadshotGenerationService.cs:108-111`).
13. The UI replaces only the selected slot and reloads entitlements (`photo-enhancement.component.ts:2070-2123`, `2597-2627`).
14. On a later resume, `OutcomePackageService` rebuilds base candidates then walks replacement links in creation order, replacing each matching slot (`OutcomePackageService.cs:275-320`).

**Retry behavior:** the headshot route is idempotent for the same persisted `clientRequestId` and replacement ID. A dropped response can return the existing generated image without spending another allowance. Provider failure before persistence/consumption does not spend the refinement. Source expiry blocks the request before provider/allowance use.

### Entry point B: Refine from Gallery

1. Every completed Gallery image exposes **Refine / Refine in Studio**; the action is not limited by image class (`gallery-image-actions.component.html:32-34,57`; `.ts:45-50`).
2. Gallery navigates to `/app/enhance?refineImageId=<id>` (`AI.ProfilePhotoMaker.UI/src/app/pages/gallery/gallery.component.ts:248-250`).
3. Studio fetches `/api/profilephotoworkflow/images/{id}/studio-source`. The API verifies ownership and storage existence, then returns the selected processed/storage image (`ProfilePhotoWorkflowController.cs:84-115`).
4. `loadGalleryImageForRefinement()` clears the normal candidate list, selects the Gallery image, hard-codes `enhancementType='headshot_linkedin'`, and attempts to select the “best active paid package” (`photo-enhancement.component.ts:1225-1258`).
5. Gallery context bypasses the normal candidate-completion requirement (`photo-enhancement.component.ts:834-856`; commits `0455d7f`, `760fd69`).
6. Clicking the same **Regenerate selected proof** control enters `startEnhancement()`, but because the enhancement type is not exactly `headshot`, it posts the saved Gallery image to `/api/enhancement/enhance`, not `/api/headshots/generate` (`photo-enhancement.component.ts:1994-1997`, `2134-2150`).
7. The controller classifies only `headshot_linkedin`, `headshot_creator`, `headshot_office`, and `headshot_studio` as package refinements (`EnhancementController.cs:527-531`). It checks for **any** active refinement allowance, bypasses legacy credits, invokes OpenAI, decrements **any** eligible entitlement, then stores the result (`EnhancementController.cs:217-255`, `315-330`, `477-505`).
8. OpenAI edits the selected Gallery asset using a fixed LinkedIn-network prompt (`OpenAIImageGenerationService.cs:68-114`, `469-472`).
9. The saved result is `GenerationMode="photo_refinement"`, has a random correlation ID, and has no `ReplacesProcessedImageId` (`EnhancementController.cs:485-514`). It therefore appears as a separate Gallery image and is not restored into a package candidate slot by `GetResumablePreviewAsync`.
10. The UI displays the returned image but leaves `selectedCandidateId` and the source path pointing to the original Gallery image. Repeating the action edits the original again, not the first refinement.

**Retry behavior:** this route has no client request ID/idempotency. A response loss after server success leaves a persisted result and spent allowance, while a user retry spends another allowance. The interrupted-generation draft is only created inside the headshot branch.

### Entitlements, credits, and observability

- Purchase fulfillment grants both legacy credits and an outcome-package entitlement (`CreditPackageService.cs:70-122,250-307`).
- Both refinement paths bypass legacy credit consumption when `IOutcomePackageService` is present.
- The headshot path supplies `packageCode`; the Gallery enhancement path does not. `ConsumeRefinementAsync` without a code chooses the earliest-expiring/oldest active entitlement with allowance (`OutcomePackageService.cs:572-590`).
- Admin surfaces expose remaining refinement totals and per-entitlement counts, but there is no dedicated refinement usage/audit record beyond the image's generation mode/correlation and the decremented counter (`AdminService.cs:1030-1080`).

## What is working correctly

1. **Allowance separation:** normal candidate generation and refinements use distinct counters; package refinements do not use legacy credits.
2. **Ownership controls:** the normal path validates the candidate owner and original source; Gallery source lookup verifies image ownership and storage existence; enhancement storage paths are restricted to the current user's prefixes.
3. **Normal-path replacement persistence:** `ReplacesProcessedImageId` and resume reconstruction preserve candidate count and replace the selected slot.
4. **Normal-path idempotency:** a stable client request ID prevents double-generation/double-spend after a dropped response.
5. **Provider-failure ordering:** both paths call the provider before decrementing refinement allowance, so direct provider failures do not consume the allowance.
6. **Free Preview gate:** free-preview regeneration is denied; paid entitlements can retain refinements after candidate allowance reaches zero (`PackageEntitlementPolicy.cs:19-50`).
7. **Package definitions:** runtime API/DB values consistently grant Starter 2 and Pro 5 refinements.

## Findings

### High — refinement allowance consumption is not concurrency-safe

`ConsumeRefinementAsync` performs a read, decrements an EF entity in memory, then saves. There is no row-version, atomic conditional update, reservation, or serializable transaction (`OutcomePackageService.cs:572-590`; `UserPackageEntitlement.cs`). Two concurrent requests that both read `RemainingRefinements=1` can both return success while the stored count ends at zero, producing two paid/provider results for one allowance. Both API paths pre-check and generate before this non-atomic consume, widening the race window.

**Classification:** confirmed accounting defect.  
**Recommendation:** make decrement/reservation atomic (`UPDATE ... WHERE RemainingRefinements > 0`, checking affected rows) and pair it with request idempotency. Reserve before provider work, finalize on persistence, and release on failure—or perform equivalent transactionally safe compensation.

### High — Gallery refinement cannot reliably use a refinements-only entitlement

`getBestActivePaidPackageCode()` only recognizes entitlements with `RemainingPackageUses > 0` **and** `RemainingCandidates > 0` (`photo-enhancement.component.ts:711-728`). Once package candidates are fulfilled, `ConsumeCandidatesAsync` sets those values to zero while leaving the entitlement active if refinements remain (`OutcomePackageService.cs:550-570,630-648`). A fresh Gallery-to-Studio navigation then remains `free_preview`, hiding/disabling the refinement action despite paid refinements being available.

This is not covered by the Gallery unit fixture, which uses a Pro entitlement with seven candidates remaining (`photo-enhancement.component.spec.ts:283-337`).

**Classification:** confirmed user-facing defect.  
**Recommendation:** Gallery selection must choose an active paid entitlement with `RemainingRefinements > 0`; package generation selection should remain a separate rule.

### High — Gallery allowance can be lost and retries can double-spend

`EnhancementController` consumes the refinement after OpenAI succeeds but **before** `SaveEnhancementResultAsync`. Its exception handling refunds only legacy credits, which are zero for package refinements (`EnhancementController.cs:315-330,360-465,485-514,625-645`). Storage/DB failure therefore spends a refinement without delivering a persisted image, contrary to the documented free-retry rule. The route also lacks idempotency, so a lost HTTP response followed by retry can spend twice.

**Classification:** confirmed recovery/accounting defect.  
**Recommendation:** add an idempotent request key and atomic reserve/finalize/release semantics; never finalize the allowance before durable image persistence.

### High — package progress can falsely expose refinement

`getGeneratedCandidateCount()` takes the maximum of loaded candidates and `included - remaining allowance` (`photo-enhancement.component.ts:818-829`). In an inconsistent/temporarily unavailable allowance state—one restored candidate and zero remaining candidates for a nine-candidate package—it reports `9 of 9`, marks fulfillment complete, and exposes finishing/refinement controls instead of the required fulfillment error.

The existing mocked Playwright test expects `1 of 9` plus the allowance-mismatch blocker, but currently fails because the rendered page says `9 of 9` and “Candidate set complete” (`profile-workflow-flags-and-download.spec.ts:782-1002`; verification below).

**Classification:** confirmed state-derivation defect.  
**Recommendation:** derive fulfilled slots from durable candidate records; use allowance only as a consistency check. When they disagree, show the documented mismatch/recovery state and do not expose refinement.

### Medium — one label hides two different products

Normal regeneration uses the original upload and creates a fresh style/use-case generation. Gallery refinement edits the selected image and always uses `headshot_linkedin`. Both are presented as **Regenerate selected proof**. Neither UI copy explains which properties should remain stable, and Gallery gives no direction choice despite plans describing guided refinements.

**Classification:** unclear product requirement plus UX/documentation issue.  
**Recommendation:** choose one canonical definition. Recommended default: “refinement” is one idempotent replacement attempt for a selected owned candidate, with an explicit guided direction. If “try another generation from the original” remains desired, call it **Try another version**, not an image edit. Keep premium augmentations separate.

### Medium — Gallery results are not candidate replacements

Gallery results are saved as independent `photo_refinement` rows without replacement metadata, while normal refinements use `instant_headshot` plus `ReplacesProcessedImageId`. Workspace resume ignores Gallery refinements. The same UI therefore sometimes replaces a proof and sometimes creates a detached image.

**Classification:** confirmed implementation inconsistency; desired history behavior requires product confirmation.  
**Recommendation:** record source/replacement linkage and make resume/gallery presentation explicit: replacement in the package proof set, with older versions retained as history if desired.

### Medium — direct Gallery refinement can be a consent/style-loading dead end

`canStartEnhancement()` requires biometric consent and a selected portrait style for every headshot-MVP request (`photo-enhancement.component.ts:929-951`). Gallery context hides the upload/generation form containing the biometric checkbox, and the refinement section only renders Turnstile (`photo-enhancement.component.html:483-498,823-846`). A user entering from Gallery without stored consent sees a disabled action with no consent control. Gallery refinement also does not need the portrait style selected by the style API, but failure to load that catalog still disables it.

**Classification:** confirmed UX gate defect by static flow analysis.  
**Recommendation:** render required consent and blocker text in Gallery refinement context; apply portrait-style gating only to the headshot-generation path that uses it.

### Medium — displayed and consumed entitlement can differ

The UI displays the first active entitlement returned newest-first, while `ConsumeRefinementAsync` chooses earliest expiry/oldest. The headshot path scopes only by package code; Gallery scopes by neither entitlement ID nor package code. With multiple purchases, the count shown may not decrement, the button may show `0` while another entitlement enables it, or Gallery may spend a different package than the one displayed.

**Classification:** confirmed attribution/UX defect.  
**Recommendation:** select and send an entitlement ID (or at minimum package code) and return the consumed entitlement plus remaining count in the response.

### Medium — refinement visibility is coupled to the premium-augmentation flag

The entire refinement container is guarded by `arePremiumAugmentationsVisible` (`photo-enhancement.component.html:818`), although product rules define refinements and premium augmentations as independent allowances. Production IaC currently sets the flag true (`infrastructure/simple-deploy.bicep:501-528`), so this is latent under current declarative deployment configuration, but disabling premium augmentations also removes paid refinements.

**Classification:** confirmed rollout/configuration defect.  
**Recommendation:** gate the refinement action on outcome-package/refinement availability; gate only add-on controls on `PremiumAugmentationsVisible`.

### Medium — refinement result is already persisted but UI offers “Save to Workspace”

Both API implementations persist the generated image before returning. The headshot branch sets `isSaved=true`; the generic enhancement/Gallery branch does not. Gallery refinement can therefore show **Save to Workspace**, whose endpoint creates a second image record and copy if given valid base64 (`photo-enhancement.component.ts:2120-2234,3030-3075`; `ImageController.cs:1235-1325`). Resumed headshot candidates can expose the same stale action.

**Classification:** confirmed adjacent persistence/UX defect.  
**Recommendation:** treat every successful/refetched server-persisted image as saved and remove the redundant action from these paths.

### Low — package/test language has drifted

Current seed and product profitability evaluation say Starter 2 / Pro 5. Several unit/E2E fixtures still use Starter 1 / Pro 3, and the broad glossary/overhaul plan uses ranges (1–2 / 3–5). One E2E refinement response also claims `creditsCost: 1` although package refinement responses now cost zero legacy credits.

**Classification:** test/documentation drift.  
**Recommendation:** make API package definitions the exact user-facing source, keep glossary qualitative, and centralize fixture builders around 2/5 plus zero legacy-credit cost.

### Low — important server behavior lacks direct coverage

Existing tests cover sequential allowance consumption, headshot replacement linkage, resume substitution, UI slot replacement, and Gallery routing. No direct `EnhancementController` tests cover package refinement classification, no-credit behavior, persistence failure compensation, idempotency, concurrent consumption, or package attribution. The Gallery fixture also omits the completed-package/refinements-only case.

**Classification:** test gap.  
**Recommendation:** add the smallest regression set around the high-priority findings before changing implementation.

## Product decisions still needed

1. Should a refinement be a fresh variation generated from the original upload, a directed image-to-image edit of the selected proof, or two explicitly named actions?
2. Should Gallery refinement replace a candidate in its package proof set or create a separate image with visible version history?
3. When a user owns multiple active packages, should refinement spend the explicitly selected entitlement, the oldest-expiring entitlement, or a pooled allowance?

Recommended defaults: use explicit guided directions on the selected proof; retain older versions as history while replacing the active package slot; spend the entitlement shown to the user.

## Prioritized action plan

| Priority | Action | Impact | Estimated effort |
| --- | --- | --- | --- |
| 1 | Add atomic allowance reservation/decrement and idempotency to both paths | Critical: protects paid allowance and provider cost | Medium–high |
| 2 | Select refinements-only entitlements for Gallery and send explicit entitlement/package attribution | High: restores access after candidate completion and makes counters truthful | Low–medium |
| 3 | Derive completion from persisted candidates and block on allowance mismatch | High: prevents premature refinement and false completion | Medium |
| 4 | Resolve and name the canonical refinement behavior | High: removes the product/UX ambiguity driving divergent code | Product decision, then low–medium implementation |
| 5 | Unify replacement linkage, durable persistence, safe retry, and saved state | High: makes Gallery and workspace recovery predictable | Medium–high |
| 6 | Separate refinement from the premium flag and show consent/blocker UI in Gallery context | Medium: removes rollout and direct-entry dead ends | Low |
| 7 | Align 2/5 fixtures and add controller/concurrency/failure regressions | Medium: prevents recurrence and test drift | Medium |

## Verification evidence

### Commands run

```text
dotnet test AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~OutcomePackageServiceTests|FullyQualifiedName~HeadshotGenerationServiceTests|FullyQualifiedName~PackageEntitlementPolicyTests' \
  --no-restore
# Passed: 37, Failed: 0

cd AI.ProfilePhotoMaker.UI
npm run test -- --watch=false \
  --include='src/app/components/photo-enhancement/photo-enhancement.component.spec.ts' \
  --include='src/app/pages/gallery/gallery.component.spec.ts' \
  --include='src/app/components/photo-gallery/gallery-image-actions/gallery-image-actions.component.spec.ts'
# TOTAL: 61 SUCCESS

cd AI.ProfilePhotoMaker.API/tests/playwright
npx playwright test tests/profile-workflow-flags-and-download.spec.ts \
  --project=chromium \
  --grep 'Pro upgrade return makes remaining candidate fulfillment the primary action'
# Failed: expected “1 of 9 generated”; rendered “9 of 9 generated” and “Candidate set complete”.
# Playwright then kept its HTML report server open; the command wrapper timed out at 300 seconds.
```

The failed Playwright snapshot is direct evidence of the package-progress finding, not a provider failure. Unit suites passing show the existing tested happy paths remain internally consistent, but they do not cover the identified edge cases.

### Existing production evidence boundary

`docs/verification/purchased-photo-completion.md:66` records a non-destructive production check that Gallery **Refine** navigated to Studio, fetched one authorized source, reconciled a partial `2 of 9 / 7 remaining` state, and did not invoke generation. It validates routing/source loading only. It does **not** verify OpenAI refinement output, allowance decrement, failure refund, replacement persistence, idempotency, or completed-package access.

### Not verified by design

- No paid or external OpenAI/Replicate call was made.
- No production allowance was consumed.
- Concurrency and persistence-failure findings were proven from current control flow/data access semantics, not exercised destructively against production.
- Exact desired product semantics for “refinement” remain a product decision; implementation differences are verified facts.

## Bottom line

Today, **Regenerate selected proof** works reasonably for a fully restored package candidate set, including in-slot replacement and retry recovery. The Gallery version is a separate fixed-LinkedIn enhancement workflow and is not reliable once only refinements remain. Across both paths, allowance consumption needs atomicity; the Gallery path additionally needs durable failure compensation and idempotency. Define the term once, then make both entry points honor the same entitlement, persistence, and recovery contract.
