# Processing visibility and posture comparison investigation

Baseline: `47a2580` (production release 587). Goal: `mtqdm8rw-8c6q3t`.

## Reproduction before implementation

Command (Angular development server, no paid provider):

```sh
cd AI.ProfilePhotoMaker.UI
npx playwright test --config /tmp/aipm-production-playwright.config.cjs --workers=1 --grep 'processing modal|posture comparison'
```

Four processing cases (generation/refinement at 390px/1440px) fail because no loading dialog exists. The request is deliberately held pending after the user clicks a scrolled action. Each test releases the request in cleanup.

The minimized comparison command uses `--grep 'posture comparison'`. Both widths fail the image-within-panel geometry assertion. The before and after `src` assertions pass with distinct valid fixture images. An initial invalid fixture reached image fallback rather than the intended geometry assertion; it was replaced with a valid, portrait-shaped SVG before accepting the reproduction.

Private logs: `/tmp/aipm-modal-comparison-red.txt`, `/tmp/aipm-comparison-minimal-red.txt`.

## Trace and evidence boundaries

- Frontend generation submits the selected proof storage path and replacement image ID with `upright_posture`, one output, and the stable client request ID.
- `HeadshotGenerationService` validates the owned selected database row and exact storage path. It passes that path and the fixed refinement prompt to the provider, bypassing candidate recipe variation.
- `OpenAIHeadshotGenerationProvider` transfers that path and custom prompt into `OpenAIImageGenerationService`.
- The image service reads the selected storage object, prepares image bytes, submits an `images/edits` multipart request, and returns the provider's response image. It does not substitute the input as a successful fallback.
- The generation service saves that returned output as a new processed image with the replacement link and original source lineage. Existing receipt/accounting safeguards remain unchanged.
- Frontend captures the selected proof before replacing the displayed result; the comparison uses its prior display URL and the returned image. The distinct-fixture browser check does not reproduce accidental frontend source/result reuse.

These checks do not establish what the real provider returned in the user's screenshot or whether the posture change was worthwhile. The current edit intentionally requests a gentle shoulder change while preserving other details; an already upright subject may show little change. No paid quality evaluation or production account mutation has been performed. Do not claim confirmed visual improvement, infer identical bytes from the screenshot, or strengthen the prompt speculatively.

## Implementation direction

- Use a native modal dialog for viewport visibility and browser focus containment; retain the existing visual design.
- Describe the actual operation, show indeterminate activity rather than invented percentages, and preserve saved-request safety.
- Allow hiding progress without implying cancellation; keep a viewport-visible way to reopen it. Hiding must not start, abort, or retry a provider request.
- Close cleanly on success/error and restore meaningful focus. Cover Escape, keyboard navigation, reduced motion, interruption, and duplicate submission.
- Correct comparison sizing so both image frames remain within their panel, with consistent uncropped framing on mobile and desktop.
- Describe a generated refinement as a result to inspect, not a verified improvement. Explain before purchase/submit that an already upright pose may change minimally.

## Implemented checks

- Native loading dialog covers generation, guided refinement and premium edits. It uses indeterminate activity, a keyboard-contained action, truthful hide-without-cancel behavior, a fixed reopen control and reduced-motion styling. Success/error removes the modal; errors receive focus. Workspace capture before teardown avoids losing the focus-restoration target when Angular removes the component.
- Comparison images now have an explicit automatic height instead of inheriting the full grid-cell height in addition to their labels. Both columns permit shrinking and preserve a consistent contained aspect ratio.
- Result headings say `result`, not an unverified `applied` claim. The posture choice explains that an already upright pose may change very little. No speculative provider prompt or accounting change was made.
- Ten browser regressions pass together (390px and 1440px): generation/refinement pending states, Escape/hide/reopen, focus, reduced motion, no duplicate submission, definitive rejection versus unknown-result recovery, distinct comparison URLs and panel containment. Log: `/tmp/aipm-modal-comparison-green.txt`.
- Sixteen backend refinement regressions pass: `/tmp/aipm-posture-goal-api.txt`.
- Real local API/SQL/Azurite check passes with a deterministic local provider: one refinement consumed; candidate/premium/credit balances unchanged; saved image retrieved; identical receipt replay without a second provider invocation. Log: `/tmp/aipm-posture-goal-docker.txt`. An expired local verification login was refreshed before the successful check.

The visual effectiveness of the user's particular real posture edit remains unverified. Local fixtures establish data flow and accounting, not generative quality.

## Release review

**Standards:** Direct review against `47a2580` found an unsafe reset exposed by hiding progress: Enhance Another Photo could clear an active request. Three failing unit cases now pass with active/premium/saved-request reset guards and a disabled button. Explicit discard is also blocked during active work. No dependencies, backend accounting changes, or migrations were introduced. Source, credentials, and private screenshots remain outside the commit. No blocking findings remain. This was a direct review, not a claimed parallel sub-agent review.

**Spec:** A native viewport modal replaces inline generation progress and also covers premium edits. Eight lifecycle cases exercise generation, refinement, definitive rejection, and unknown provider outcomes at both widths. Modal closure focuses the result or error instead of leaving focus on the document body. Hiding keeps a visible reopen action and truthful in-progress guidance rather than telling users to resolve a supposedly interrupted request. Comparison results are distinct and geometrically contained. The posture limitation is explicitly disclosed before spending an allowance; unverified improvement is not claimed.

**Visual verification:** One batched mobile/desktop inspection and one confirmation pass checked the actual modal and contained comparison layout. The comparison screenshots use committed marketing fixtures, not real posture outputs. Private screenshots: `/tmp/aipm-modal-review-{390,1440}.png` and `/tmp/aipm-comparison-review-{390,1440}.png`. Automated CSS design scan returned no findings. Later focus/reset corrections were verified functionally without further cosmetic iteration.

**Final gates:** 490 API tests, 510 Angular tests (two existing skips), 21 browser tests, and production build/lint passed. Durable summary: `docs/testing/evidence/processing-modal-posture-release-gates.txt`. Raw local logs: `/tmp/aipm-modal-reviewed-{api,ui,build,browser}.txt`.

User-authorized deployment through the existing main-push workflow is next; live release identifiers and health will be recorded after completion.
