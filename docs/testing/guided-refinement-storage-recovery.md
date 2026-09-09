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

No paid AI call or production mutation occurred during diagnosis. An already saved request from the older UI must be explicitly resumed or discarded after updating; unknown requests are never silently erased.

## Requested verification and predeployment review

User subsequently authorized verification, review and deployment of `4932756` against production baseline `eda11f3`.

### Standards

No blocking findings. The additional storage formats remain restricted to a selected database-owned proof and the authenticated user's folder; exact-path, unpaid-preview and traversal checks remain intact. No dependencies or migrations were added. The light-workspace color override leaves dark fulfillment-panel text unchanged.

### Spec

No missing requirements found for this fix. Real writer-generated paths now work; definitive validation failures release the saved request, ambiguous outcomes remain protected, and recovery guidance is readable at both tested widths. The test fixture's path-contract mismatch has been corrected rather than bypassing validation.

### Fresh release gates

- Full API suite: **490 passed**, including the previously flaky performance checks.
- Angular suite, production build, and focused 11-test browser suite passed again.
- Rebuilt Docker API tested against real local SQL and Azurite: an unprefixed saved photo was refined successfully, exactly one refinement was consumed, candidate/premium/credit balances were unchanged, the saved image was retrieved, and replay returned the same receipt without another provider call.
- The Docker provider was explicitly checked to be the local deterministic fixture, not a paid AI endpoint. The first harness run reached a successful API response but failed to read a host-mounted counter; the complete rerun read the counter inside its container and passed.

Deployment is authorized through the existing main-push workflow. Its result and live health are reported separately after completion.
