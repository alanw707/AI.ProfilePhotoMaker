# Purchased-photo completion verification

Functional changes: `534c917` (`Restore paid candidates after preview expiry`); `22dc862` (`Serve owned paid candidate images`).

## Deterministic coverage map

| Workflow path | Executable proof |
| --- | --- |
| Upload quality gate / score | `ProfilePhotoScoreServiceTests.ScoreAsync_*`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` |
| Free Preview | `HeadshotGenerationEndpointIntegrationTests.GenerateHeadshot_FreePreview_GeneratesOneStoredImageWithoutConsumingCredits`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` |
| Paid continuation and allowance | `GenerateHeadshot_StarterPackage_RequiresEntitlementAndConsumesCandidateAllowance`; `HeadshotGenerationServiceTests.GenerateHeadshotAsync_CompletesPaidCandidatesWithoutLegacyCredits`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` |
| Package progress / partial retry | `GenerateHeadshot_OneCandidateBatchesResumeWithoutDuplicatesOrAllowanceLoss`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource`; `photo-enhancement.component.spec.ts` partial-fulfilment cases |
| Expired display/source recovery | `OutcomePackageServiceTests.GetResumablePreview_RestoresPromotedAndPaidCandidatesAfterInterruption(... previewDisplayExists: false)` |
| Failed-generation recovery | `HeadshotGenerationServiceTests.GenerateHeadshotAsync_RefundsCreditsWhenProviderFails`; enhancement error/retry cases |
| Saved candidate viewing/refinement | `StudioSource_ReturnsOnlyAnOwnedImage`; `PurchasedPhotoWorkflow_ScoresPreviewContinuesPackageAndLoadsStudioSource` (owned restored-candidate URL returns 200; another user gets 404); Gallery route and Studio-load specs |

The expiry case is deliberately red-capable: before `534c917`, its `previewDisplayExists: false` row returned `null`; it now restores the owned paid candidates while the preview display copy and source are unavailable.

The legacy-credit regression is deliberately red-capable: `GenerateHeadshotAsync_CompletesPaidCandidatesWithoutLegacyCredits` performs two one-candidate paid requests with one legacy credit. Before the package allowance bypass, the first consumed that credit and the second threw `InsufficientCredits` (the API maps this to `Instant headshot generation requires 1 credit.`). Current behavior completes both with `CreditsCost == 0` and consumes only package candidates.

## Commands and results

```text
dotnet test AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~OutcomePackageServiceTests|FullyQualifiedName~HeadshotGenerationEndpointIntegrationTests|FullyQualifiedName~HeadshotGenerationServiceTests' \
  --no-restore
# Passed: 40

cd AI.ProfilePhotoMaker.UI && npm run test -- --watch=false \
  --include='src/app/components/photo-enhancement/photo-enhancement.component.spec.ts' \
  --include='src/app/pages/gallery/gallery.component.spec.ts' \
  --include='src/app/components/photo-gallery/gallery-image-actions/gallery-image-actions.component.spec.ts'
# Passed: 58

npm run build:mvp-v1
# Passed (existing lint warnings only)
```

## Production verification

GitHub Actions deployments for the functional commits succeeded:

- Expiry/recovery: run `33386974558`, commit `534c917b6cc4e3aa4f86ad0305d310c0c6e7996c`, <https://github.com/alanw707/AI.ProfilePhotoMaker/actions/runs/33386974558>
- Saved paid-candidate viewing: run `33390397106`, commit `22dc862f31476bf5f2a84ff99ae5f330de32390f`, <https://github.com/alanw707/AI.ProfilePhotoMaker/actions/runs/33390397106>

The latter deployment completed its test, security scan, and production deployment jobs successfully. Its restored-candidate URL behavior is proven by the deterministic integration test above: the owner receives `200`; a different authenticated user receives `404`.

Authenticated inspection was non-destructive. On desktop, selecting **Refine** for an owned Gallery image navigated to `/app/enhance?refineImageId=…`; one authorized `studio-source` request occurred, the Studio showed the existing package progress (`2 of 9 generated`), and no alert appeared. At a 390 px mobile viewport, the same route made one authorized source request and had no horizontal overflow. No generation control was invoked and no allowance was consumed.
