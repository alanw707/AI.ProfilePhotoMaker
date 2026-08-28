# `/app/enhance` Package Journey

## Invariants

1. Free Preview contains 1 candidate; Starter contains 3; Pro contains 9.
2. A purchased package promotes the raw Free Preview to candidate 1 and generates only the remaining slots.
3. Candidate generation, refinements, premium augmentations, and export-kit availability are independent allowances.
4. The interface exposes one primary action: the action that advances the active fulfillment milestone.
5. Download is a completion action only after the user has a paid candidate and exports are available. It never implies all candidate slots have been fulfilled.

## Journey model

| Stage | Entry condition | Required information | Primary action | Secondary actions | Exit |
|---|---|---|---|---|---|
| Source | No resumable work and no selected file | Accepted formats, size limit, privacy/retention | Upload one photo | Resume saved preview when available | Valid file selected |
| Score | Source selected | Score progress, quality dimensions, gate result | Continue with this photo | Choose another photo; acknowledge warning | Source passes or warning accepted |
| Style | Source usable | Use case, recommended styles, selected style | Generate Free Preview or paid candidates | Browse more styles | Generation request starts |
| Free Preview generation | Free package active | `0 of 1`, processing status, safe leave/return guidance | None while processing | Cancel only if supported | Watermarked candidate ready or error |
| Free Preview review | One preview ready | `1 of 1`, watermark/export limits, paid package outcomes | Choose Starter or Pro | Download watermarked preview; start over | Checkout or end |
| Upgrade return | Active paid entitlement and promoted preview | Package name, `1 of 3` or `1 of 9`, exact remaining slots, separate allowances | Generate remaining 2/8 photos | Review promoted candidate; start later | Remaining generation starts |
| Paid generation | Candidate slots requested | Generated/total progress and current status | None while processing | Leave safely when persistence permits | Full or partial candidate set |
| Partial fulfillment | Some paid candidates exist and candidate slots remain | Exact generated/total count, recoverable failure context | Generate remaining N photos | Review existing candidates; retry failed batch | Full set or paused work |
| Candidate review | Candidate set available | Selected proof, best-shot marker, score/reason, candidate count | Select this as final photo | Compare another candidate | Final candidate chosen |
| Adjustment | Final candidate selected | Non-generative adjustment preview and reset state | Keep adjustments | Reset; choose another candidate | Adjustments accepted |
| Refinement | Candidate fulfillment complete and refinements remain | `N refinements remaining`; clear distinction from generation | Regenerate selected photo | Back to proofs | New candidate version ready or error |
| Premium augmentation | Pro candidate selected and augmentations remain | `N premium add-ons remaining`; effect description; before/after | Apply selected add-on | Cancel; compare result | Augmented version ready or error |
| Export | Paid candidate selected and kit available | Platform options, dimensions, selected count, adjustment effect | Download package | Share; change selections | ZIP/browser download starts |
| Complete | Download initiated or package fully fulfilled | What was produced, retention, where work is saved | Enhance another photo | Return to workspace | New flow or exit |

## State precedence

Render one dominant state in this order:

1. Account/email gate.
2. Source or resumable-preview choice.
3. Source quality gate.
4. Missing style/consent/bot-check requirement.
5. Active processing.
6. Paid candidate fulfillment incomplete.
7. Candidate review and selection.
8. Adjust/refine/augment.
9. Export and completion.

A lower-priority state may supply context but may not introduce a competing primary action.

## Package-state derivation

| Package state | Display | Primary action |
|---|---|---|
| Free, no preview | `Free Preview · 0 of 1` | Generate Free Preview |
| Free, preview ready | `Free Preview · 1 of 1` | Choose Starter or Pro |
| Starter, promoted preview | `Starter · 1 of 3` | Generate remaining 2 photos |
| Pro, promoted preview | `Pro · 1 of 9` | Generate remaining 8 photos |
| Paid, partial response | `<Package> · X of N` | Generate remaining `N-X` photos |
| Paid, full set | `<Package> · N of N` | Choose your best photo |
| Paid, final selected | `Best photo selected` | Prepare exports |
| Export kit consumed | `Package downloaded` | Enhance another photo |

## Exceptional states

- **Loading entitlement:** preserve source/preview context; skeleton the ticket; disable package-consuming actions with `Checking package…`.
- **Expired preview:** explain that promotion is unavailable; primary action `Start with a new photo`; never offer checkout continuation.
- **Exhausted candidate allowance with missing candidates:** show a fulfillment error and support/retry path; do not redirect users toward refinements.
- **No refinements:** keep candidate review and export available; label regeneration unavailable with reason.
- **No premium augmentations:** keep Pro candidate review and export available; label add-ons exhausted without disabling unrelated actions.
- **Export kit consumed:** preserve candidate review and adjustments; hide export options, disable package download, label the kit used, and provide support recovery without falling back to a different download.
- **Recoverable generation failure:** preserve generated candidates and source; primary action retries only unfulfilled candidate slots.
- **Preview/image load failure:** retain candidate metadata and package progress; offer retry preview/download without losing selection.
- **Refresh or mobile interruption:** reconstruct the highest completed stage from resumable preview, candidates, active entitlement, and workspace state. Persist in-flight request ID/source/style/package data for 24 hours so Resume generation reuses the idempotent server request; clear the marker when server candidates are recovered.

## Responsive behavior

- **360–430px:** single column; ticket summary before portrait/task; horizontal snap candidate strip; sticky safe-area-aware primary action; advanced allowance detail disclosed below.
- **768px:** single main flow with two-column candidate/contact regions where space permits; action remains in document order.
- **≥1280px:** dominant proofing canvas plus sticky task/ticket column; no separate desktop-only workflow or terminology.
- Every actionable target is at least 44×44 CSS pixels. Content survives 200% zoom and long labels without horizontal page overflow.

## Accessibility behavior

- Announce scoring, generation progress, candidate arrival, selection, add-on completion, errors, and download preparation through polite/assertive live regions appropriate to urgency.
- Candidate choices use buttons or radios with candidate number, score, recommendation, and selected state in the accessible name/state.
- Progress includes text (`1 of 9 generated`), not color or geometry alone.
- Sticky actions remain last in logical tab order even when visually fixed.
- Reduced-motion mode removes proof-mark travel and stamp transitions while preserving state changes.
