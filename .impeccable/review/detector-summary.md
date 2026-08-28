# Impeccable detector — `/app/enhance`

- **Target:** `AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/photo-enhancement.component.html`
- **Exit:** 2 (findings)
- **Finding count:** 4
- **Parser mode:** degraded regex fallback because `htmlparser2`, `css-select`, `css-tree`, and `domutils` are unavailable.

## Findings reviewed

All four `broken-image` warnings are false positives caused by Angular property binding syntax, which the fallback regex parser treats as a missing static `src`:

| Line | Binding | Runtime guard/evidence | Disposition |
|---:|---|---|---|
| 275 | `[src]="imagePreview"` | Rendered only after file selection; pre-generation Playwright screenshots show the source image. | False positive |
| 466 | `[src]="option.image"` | Legacy creative option values are concrete asset paths. | False positive |
| 603 | `[src]="imagePreview"` | Wrapped in `*ngIf="imagePreview && !beforeImageLoadFailed"` with an explicit fallback state. | False positive |
| 640 | `[src]="candidate.imageUrl"` | Candidate DTO requires `imageUrl`; Chromium/Mobile candidate-review tests render 1–9 proofs. | False positive |

## Complementary checks

The degraded detector cannot evaluate custom properties, selector matching, or computed contrast. Those gaps are covered by:

- `docs/design/app-enhance-accessibility-audit.md`;
- `scripts/check-app-enhance-contrast.mjs`;
- desktop/mobile Playwright screenshots under `.impeccable/review/`; and
- responsive touch-target, overflow, and reflow assertions in `profile-workflow-flags-and-download.spec.ts`.
