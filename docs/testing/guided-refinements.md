# Guided refinements and exhausted premium controls

## Changes

- Refinements require an explicit choice: subtle smile, relaxed expression, or straighter posture.
- Each choice sends a validated preset code, not user-supplied prompt text.
- The backend edits the owned, selected saved photo, including a premium-edited photo or a Gallery photo. It does not regenerate the original upload or apply a new portrait recipe.
- Fixed server instructions request the smallest specified change and preservation of other details. The existing photo style is retained even if that style is no longer offered.
- This improves control, not pixel determinism. No unsupported seed/fidelity parameters or guaranteed identity/quality claims were added.
- One successful edit consumes one refinement, not candidate or premium allowance. Existing operation fencing/replay handling remains in use; the preset is included in new operation identities without changing legacy identities.
- Previous photos remain saved. The before/after comparison is available for the new result.
- Crop/brightness/rotation controls remain the non-generative option for predictable adjustments.

## Exhausted picker defect

The deployed template rendered its direction picker solely from the selected premium type. The Apply button had no disabled binding, despite the handler checking allowance. A stale selection could therefore leave an apparently usable picker when the count reached zero.

Actual browser regression against the deployed template:

```
expect(locator).not.toBeVisible() failed
Expected: not visible
Received: visible
Choose the relighting direction
```

Premium edits now have a separate expandable section. At zero allowance the cards and direction picker are removed, and an explicit exhausted message replaces them. The Apply button and direction inputs also reflect eligibility/busy state.

## Verification

- Final reviewed API suite: 487 passed, zero failed (including performance checks).
- New guided-refinement regressions: 13 cases covering fixed prompts, selected source, supported image modes, expired original upload, replay, different-choice identity, exact allowance consumption, invalid choices/modes, cross-user references, unpaid previews, path mismatches and traversal.
- Final reviewed Angular suite: 502 passed, two existing skips.
- Focused Playwright: 11 passed. Mobile (390px) and desktop (1440px) regressions simulate an entitlement refresh with a stale premium selection, verify disappearance, then apply a refinement and verify 4→3 refinements with premium allowance still zero and no premium request.
- Production UI build and lint passed.
- Layout screenshots inspected at both widths; no page overflow.
- Static design detector reported existing Angular-bound-image warnings; browser image-fallback coverage passed. Existing design-sidecar drift was not changed as part of this task.

Timing caveat: the full backend run intermittently failed the unchanged repository concurrent-user performance threshold (220ms against a 200ms limit). That test passed in isolation; all functional checks passed. No performance threshold was weakened. A parallel browser run also hit the existing pricing retry timeout; the complete sequential run passed.

## Runnable checks

From repository root:

```sh
dotnet test AI.ProfilePhotoMaker.API.Tests --filter 'FullyQualifiedName~GuidedRefinement'
dotnet test AI.ProfilePhotoMaker.API.Tests --filter 'FullyQualifiedName!~Performance'
```

From `AI.ProfilePhotoMaker.UI`:

```sh
npx ng test --watch=false --browsers=ChromeHeadless
npm run build
npx playwright test tests/premium-augmentation-generate.spec.ts --workers=1
```

The entitlement-transition browser test requires the Angular development server (the default Playwright webServer configuration), not a reused production/Docker listener. It uses Angular's debug API to simulate refreshed account state; all image-generation requests are mocked. No real-money purchase or paid AI generation was used.

## Release status

User requested review and deployment. Review baseline: deployed `0959574`; scope includes the pending guided-refinement and premium-picker changes described above.

### Standards review

No blocking standards/security findings remain. Existing component/service patterns were reused; no dependency or schema migration was added. Ownership, path validation, allowance separation, consent, bot checks and operation fencing remain enforced. The pre-existing large workspace component remains a maintainability limitation, not a reason for an unrelated rewrite.

### Spec review

One recovery gap was found and fixed before release: a saved refinement request can now retrieve its existing receipt even after the last allowance is consumed. Only the matching saved preset/source/image/request identity gets this client-side replay path; the server still authorizes fulfillment. A regression verifies that changing the requested preset does not qualify for that exception.

Explicit choices, selected-photo input, fixed server instructions, premium/refinement separation, exhausted-picker removal and mobile/desktop behavior passed review. Generative visual quality and identity fidelity have not been established by paid AI testing; the UI does not promise pixel determinism.

Final full backend, frontend, production build and focused browser gates passed after the review fix. Authorized release proceeds through the existing main-push workflow; deployment outcome is reported separately. Backend and frontend must be released together for the new preset contract.
