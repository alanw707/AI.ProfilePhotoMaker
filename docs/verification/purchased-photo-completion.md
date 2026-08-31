# Purchased-photo completion verification

Functional changes: `534c917` (`Restore paid candidates after preview expiry`); `22dc862` (`Serve owned paid candidate images`); `b84de8e` (`Block generation from expired photo sources`).

## Deterministic coverage map

| Workflow path | Executable proof |
| --- | --- |
| Upload quality gate / score | `ProfilePhotoScoreServiceTests.ScoreAsync_*`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` |
| Free Preview | `HeadshotGenerationEndpointIntegrationTests.GenerateHeadshot_FreePreview_GeneratesOneStoredImageWithoutConsumingCredits`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` |
| Paid continuation and allowance | `GenerateHeadshot_StarterPackage_RequiresEntitlementAndConsumesCandidateAllowance`; `HeadshotGenerationServiceTests.GenerateHeadshotAsync_CompletesPaidCandidatesWithoutLegacyCredits`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` |
| Package progress / partial retry | `GenerateHeadshot_OneCandidateBatchesResumeWithoutDuplicatesOrAllowanceLoss`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource`; `photo-enhancement.component.spec.ts` partial-fulfilment cases |
| Expired display/source recovery and stale-request prevention | `OutcomePackageServiceTests.GetResumablePreview_RestoresPromotedAndPaidCandidatesAfterInterruption` availability matrix; `HeadshotGenerationServiceTests.GenerateHeadshotAsync_RejectsExpiredSourceBeforeCallingProviderOrConsumingPackageAllowance`; expired-source UI resume spec |
| Failed-generation recovery | `HeadshotGenerationServiceTests.GenerateHeadshotAsync_RefundsCreditsWhenProviderFails`; enhancement error/retry cases |
| Original styled-generation 400 root cause | `ReplicateControllerAuthAndOwnershipTests.GenerateImages_WithoutFiveCredits_ReplaysTheSavedStyledGeneration400WithoutCallingProvider`; `docs/deployment/evidence/generation-insufficient-credits.json` |
| Saved candidate viewing/refinement | `StudioSource_ReturnsOnlyAnOwnedImage`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` (owned restored-candidate URL returns 200; another user gets 404); Gallery route and Studio-load specs |

The expiry case is deliberately red-capable: before `534c917`, its `previewDisplayExists: false` row returned `null`; it now restores the owned paid candidates while the preview display copy and source are unavailable.

The legacy-credit regression is deliberately red-capable: `GenerateHeadshotAsync_CompletesPaidCandidatesWithoutLegacyCredits` performs two one-candidate paid requests with one legacy credit. Before the package allowance bypass, the first consumed that credit and the second threw `InsufficientCredits` (the API maps this to `Instant headshot generation requires 1 credit.`). Current behavior completes both with `CreditsCost == 0` and consumes only package candidates.

`GenerateHeadshotAsync_RejectsExpiredSourceBeforeCallingProviderOrConsumingPackageAllowance` is deliberately red-capable: before the source guard, an expired source reached the provider and consumed package allowance. It now returns `ImageSourceExpired` before either action. The resumable-preview theory independently exercises every display/source availability pair while the paid preview raw asset is available; saved paid candidates restore in every availability combination.

## Original production 400: root cause and regression signal

The saved artifact at `docs/deployment/evidence/generation-insufficient-credits.json` is a complete redacted response: HTTP `400`, `InsufficientCredits`, and `Styled image generation requires 5 credits. You have 0 credits.` Its exact message maps to `GenerationController.GenerateImages`, whose pre-provider credit guard multiplies one styled output by `CreditCostConfig.StyledGeneration` (5). The root cause was therefore an attempted **legacy styled-image generation with zero legacy credits**, not the package-backed instant-headshot workflow.

`ReplicateControllerAuthAndOwnershipTests.GenerateImages_WithoutFiveCredits_ReplaysTheSavedStyledGeneration400WithoutCallingProvider` is the deterministic replay: it reproduces the exact status/code/message from the saved artifact, verifies the 5-credit gate, and proves no provider request is sent. The current `/enhance` purchased-photo path uses the package-backed headshot endpoint, covered separately by `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` and `GenerateHeadshotAsync_CompletesPaidCandidatesWithoutLegacyCredits`; it does not treat a package allowance as legacy styled-generation credit. This distinction is the root-cause fix: purchased-photo continuation remains on the package entitlement path, while the legacy styled-image endpoint gives its accurate, actionable insufficient-credit response.

## Commands and results

```text
dotnet test AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~OutcomePackageServiceTests|FullyQualifiedName~HeadshotGenerationEndpointIntegrationTests|FullyQualifiedName~HeadshotGenerationServiceTests|FullyQualifiedName~ReplicateControllerAuthAndOwnershipTests' \
  --no-restore
# Passed: 53

cd AI.ProfilePhotoMaker.UI && npm run test -- --watch=false \
  --include='src/app/components/photo-enhancement/photo-enhancement.component.spec.ts' \
  --include='src/app/pages/gallery/gallery.component.spec.ts' \
  --include='src/app/components/photo-gallery/gallery-image-actions/gallery-image-actions.component.spec.ts'
# Passed: 59

npm run build:mvp-v1
# Passed (existing lint warnings only)
```

## Production verification

GitHub Actions deployments for the functional commits succeeded:

- Expiry/recovery: run `33386974558`, commit `534c917b6cc4e3aa4f86ad0305d310c0c6e7996c`, <https://github.com/alanw707/AI.ProfilePhotoMaker/actions/runs/33386974558>
- Saved paid-candidate viewing: run `33390397106`, commit `22dc862f31476bf5f2a84ff99ae5f330de32390f`, <https://github.com/alanw707/AI.ProfilePhotoMaker/actions/runs/33390397106>
- Expired-source request blocking: run `33392748832`, commit `b84de8ea7ef1777c7e1c0442a40602c85df7277c`, <https://github.com/alanw707/AI.ProfilePhotoMaker/actions/runs/33392748832>

The latter two deployments completed their test, security scan, and production deployment jobs successfully. The saved-candidate integration test proves owner `200` and different-user `404`; the expired-source generation test proves no provider request or package-allowance consumption.

Authenticated inspection was non-destructive. After the `b84de8e` deployment, Gallery displayed 12 visible images with no failed image load and no desktop horizontal overflow. Selecting **Refine** for an owned Gallery image navigated to `/app/enhance?refineImageId=…`; one authorized `studio-source` request occurred, the Studio loaded its visible images without failure, showed `2 of 9 generated` and `7 remaining` (counts reconcile), and showed no alert. A prior authenticated 390 px inspection of the same Gallery-to-Studio layout made one authorized source request and had no horizontal overflow. No generation control was invoked and no allowance was consumed.
