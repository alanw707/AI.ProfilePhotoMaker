# Guided-refinement storage rejection and recovery

Baseline: `eda11f3`, deployed in workflow 34051665287.

## Observed failures

The user saw `InvalidImageSource` when applying a guided refinement, followed by disabled choices and Apply. The saved-request explanation was white on a light surface.

Read-only production logs locate the invalid-source rejection in the storage-prefix validation branch. No production generation was replayed or account state modified during diagnosis.

## Root cause

`BaseStorageService.GenerateUserStoragePath`, used by the Azure and local writers, produces `generated/<user>/...`, `generated-private/<user>/...`, or `enhanced/<user>/...` without an environment prefix. The new guided-refinement validator accepted only paths built by the separate environment-aware `StoragePathResolver`.

The original regression fixtures used environment-prefixed fake paths, concealing this mismatch. The updated regression obtains paths through the real `LocalStorageService.SaveImageAsync` with mocked file I/O, exercising the shared writer contract.

Minimized command:

```sh
dotnet test AI.ProfilePhotoMaker.API.Tests --filter 'FullyQualifiedName~GuidedRefinement_EditsSelectedProof'
```

Before fix: four cases failed with `InvalidImageSource`. After fix: writer-produced paths and historical prefixed paths pass. The broader 16-case guided suite also rejects wrong owners, unpaid preview records, mismatched paths and traversal in both layouts. Matching receipts still replay without a second provider call or charge.

The validator accepts unprefixed paths only for a selected proof whose database ownership and exact path have already been verified. It does not broaden arbitrary generation inputs.

## Recovery and readability

The UI retained every rejected request as potentially in-flight. It now releases drafts only for explicitly identified HTTP 400 validation/bot-check rejections. Network errors, provider-unknown outcomes and in-progress responses retain receipt-recovery protection.

Unit regression: two definite-rejection cases failed before the fix; ambiguous cases remain protected. Browser regressions now submit a rejected edit and verify that Return to your work restores enabled choices/Apply and removes the rejected draft, without consuming another allowance.

The global white blocker text was intended for a dark fulfillment panel. A light-workspace-specific text-color rule restores readable recovery guidance without changing dark-panel text. The browser contrast assertion failed before this rule and now passes WCAG AA (at least 4.5:1) at 390px and 1440px.

## Final local checks

- API functional suite: 476 passed.
- Guided-refinement suite: 16 passed.
- Angular suite: 507 passed, two existing skips.
- Focused browser suite: 11 passed, including recovery and contrast at both widths.
- Production UI build passed.
- Full API run: 489 passed, one unchanged performance-suite threshold failed (85% achieved; assertion requires greater than 85%). No threshold was relaxed.

No paid AI call, production mutation, or deployment performed for this fix. An already saved request from the older UI must be explicitly resumed or discarded after updating; unknown requests are never silently erased.
