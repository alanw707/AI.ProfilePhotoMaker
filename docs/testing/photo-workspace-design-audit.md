# Photo workspace, package, and add-on audit

Status: **implementation and local verification complete; not a universal “bug-free” claim**

Branch: `feature/photo-workspace-design-audit`

## Test boundary

- Product source: `CONTEXT.md`. Current product is a single-photo outcome-package flow; README training/credit language is legacy.
- UI: `localhost:4200`. API: `127.0.0.1:5032`. SQL: isolated `PhotoWorkspaceDesignAudit` database.
- Payments: **real Stripe test mode**, test cards, signed webhooks, payment simulation disabled.
- Images: deterministic local fixed-JPEG provider fixture. This validates transport, storage, state, retries, and allowance accounting—not AI quality or identity preservation.
- Local filesystem storage; paid AI credentials, outbound email, and unrelated messaging jobs disabled.
- No deployment, push, merge, production inspection/change, customer data operation, real purchase, or paid provider call.
- Existing user edits were preserved. Private credentials/session/listener files remain under `/tmp/photo-workspace-audit/` and are not evidence artifacts.

## Final journey and state matrix

| Journey/state | Exact exercised path | Result | Inspectable evidence |
|---|---|---|---|
| Anonymous access | Open `/app/enhance` without session → `/auth/login` | **Pass** | `account-anonymous-redirect.png`, `account-access-trace.zip` |
| Registration validation | Focus/blur every field at 390px; eight required-field messages; no unnamed visible controls/overflow | **Pass** | `account-registration-validation-390.png`, `browser-verification-final.txt` |
| Password login | Seeded verified test account → real `POST /api/auth/login` HTTP 200 → `/app/enhance` | **Pass** | `account-login-workspace-1440.png`, `authenticated-workspace-trace.zip` |
| Package discovery | Live Free/Starter/Pro API; direct + refreshed `/pricing` and `/packages` at 1440/390 | **Pass** | `pricing-final-1440.png`, `pricing-final-390.png`, `playwright-final.txt` |
| Loading states | Delay—not replace—real package and score responses by 1.2s; inspect package loader and live score overlay | **Pass** | `pricing-loading-390.png`, `workspace-score-loading.png`, authenticated trace |
| Empty states | Real empty photo workspace; explicitly mocked empty package response, then Retry passed through to live API and recovered 3 cards | **Pass** | `workspace-empty-*.png`, `pricing-empty-mocked.png`, browser final text |
| Error/retry states | Inject package HTTP 503 then live Retry; inject generation failure and preserve source/retry; export renderer IOException preserves allowance | **Pass** | `pricing-error-injected.png`, `pricing-error-retry-*.txt`, `playwright-final.txt`, API tests |
| Upload/score | Invalid MIME, >7 MB, dismiss, valid image retry, live local score; quality-warning override | **Pass** | `upload-validation.json`, `workspace-score-loading.png` |
| Free Preview | One local-fixture preview; watermark/promotion metadata; draft resume; preview-image failure fallback | **Pass** | `free-preview-before-checkout.png`, `preview-checkout.json`, focused tests |
| Preview → Starter | Real browser Stripe card checkout; zero paid generation before post-payment consent; preview + 2 new candidates; candidate balance 0 | **Pass** | `checkout-*.png`, `starter-after-preview-upgrade.png`, `preview-checkout.json` |
| Payment cancel/decline/3DS | Cancel grants 0; declined card shows alert/grants 0; 3DS challenge completes and grants exactly one Starter | **Pass** | `checkout-resilience.json`, `checkout-declined.png`, `checkout-3ds-*.png` |
| Webhook/idempotency/security | Real signed success/replay; repeated confirmation; decline/cancel; user/package/amount/currency mismatch | **Pass** | `stripe-sandbox-payments.json`, `stripe-upgrade-webhooks.json`, API tests |
| Pro generation | Browser/local API/SQL path returned 9 candidates and consumed allowance once | **Pass** | `preview-result.json`, `preview-*.png` |
| Refinement | Candidates exhausted; refinement 5→4; candidate balance remains 0 | **Pass** | `refinement.json`, API/Karma tests |
| Premium augmentations | Browser relighting 3→2; all 8 supported types integration-tested; exhausted/expired rejected | **Pass** | `paid-finishing.json`, `api-final-rerun.txt` |
| Standalone add-on purchase | No standalone SKU/checkout exists; current feature is Pro-included premium augmentation allowance | **N/A** | package definitions + implementation; misleading API copy removed |
| Gallery/result recovery | Reopen owned paid result; restore four supported image modes; cross-user ID null; no implicit generation/spend | **Pass** | `paid-resume-defect.json`, paid-final screenshots, API/Karma tests |
| Export | Browser ZIP: 4 selected JPEGs + README; failure/retry; all 12 names/dimensions; crop/zoom pixel checks | **Pass** | `paid-finishing.json`, `api-final-rerun.txt`, `PlatformExportServiceTests.cs` |
| Responsive/accessibility basics | 1440/390, light/dark, headings, controls, labels, alerts, focus order, mobile menu/theme keyboard activation, overflow/contrast scan | **Pass** | browser final text, both trace ZIPs, `ui-accessibility.json`, mobile screenshots |

Mocked/injected states are labeled above. Stripe fulfillment and normal/3DS/declined browser payments were not mocked. Package/score loading used delayed pass-through to the real local API.

## Severity-ranked findings, reproductions, fixes

### F01 — P0: direct pricing navigation served inert placeholder HTML — fixed

- **Reproduce before fix:** run the dev UI; open or refresh `/pricing`; observe only obsolete training copy, with no Angular package cards/actions.
- **Cause:** copied `public/pricing/index.html` shadowed the Angular route and contained no bootstrap scripts.
- **Fix:** exclude `public/**/index.html` from Angular assets; retain release SEO generation.
- **Verify:** `pricing-entry.spec.ts`; before/after images are explicitly named `pricing-before-fix-*.png` and `pricing-final-*.png`; release build generated 26 pages.

### F02 — P1: failed export stranded the button and changed requested format — fixed

- **Reproduce before fix:** restore/select a paid image; make the ZIP endpoint fail; click Download package; observe busy state never clears and fallback initiates a single-image download rather than preserving ZIP intent. Click again while pending to observe duplicate action.
- **Fix:** reset with RxJS `finalize`, ignore re-entry, and show a retryable ZIP error without format substitution.
- **Verify:** focused action tests cover error reset, no fallback, and duplicate guard; browser export succeeds afterward.

### F03 — P1: failed ZIP rendering consumed export allowance — fixed

- **Reproduce before fix:** grant one export kit; configure `IPlatformExportService` to throw `IOException`; POST `/api/profilephotoworkflow/export-package`; query entitlement and observe export unavailable despite no ZIP.
- **Fix:** render ZIP before consuming entitlement.
- **Verify:** `ProfilePhotoWorkflowExportTests.cs` checks failed render preserves allowance and successful render consumes it.

### F04 — P1: premium action error/re-entry handling was unsafe — fixed

- **Reproduce before fix:** invoke premium augmentation twice while first request is pending; or return HTTP 200 with `success:false`; observe duplicate requests or a thrown subscriber error without useful workspace message.
- **Fix:** enforce supported type + busy/availability guard; surface application error; preserve selected result.
- **Verify:** focused Karma action regressions and browser relighting path.

### F05 — P1 security: another user's completed purchase could be returned — fixed

- **Reproduce before fix:** create completed transaction for user A; as user B call purchase confirmation with A's transaction ID; service returns existing purchase before checking owner.
- **Fix:** existing purchase must match current user and requested package.
- **Verify:** real-service regression checks cross-user and cross-package requests grant/return nothing.

### F06 — P1: manual-review payment could fulfill — fixed

- **Reproduce before fix:** persist a `PendingReview` transaction, then call purchase confirmation; refresh could overwrite review and grant credits/package.
- **Fix:** reject manual-review and refund states before Stripe refresh/fulfillment.
- **Verify:** service test confirms no purchase, credits, or entitlement.

### F07 — P1 integration: sandbox webhooks rejected Stripe API version — fixed

- **Reproduce before fix:** deliver a real signed sandbox `payment_intent.succeeded`; endpoint returns 400 because Stripe.NET 48.5.0 expects Basil while event is `2025-10-29.clover`.
- **Fix:** with explicit approval, upgrade Stripe.NET 48.5.0 → **49.1.0**; keep strict signature/version validation.
- **Verify:** real listener/replay HTTP 200; realistic Clover event accepted; tampered signature rejected.

### F08 — P2: unavailable portrait preview rendered broken image — fixed

- **Reproduce before fix:** block a portrait-style image URL; open style selector; browser shows a broken image with no recovery.
- **Fix:** switch once to existing local placeholder on error; guard second failure to avoid loops.
- **Verify:** Playwright `unavailable portrait previews...` passes.

### F09 — P1: dismissing generation error discarded source/result — fixed

- **Reproduce before fix:** upload/score a photo; force generation failure; click “Try again”; component reset removes source and work.
- **Fix:** relabel action “Dismiss” and clear error only.
- **Verify:** Playwright injects generation failure and checks photo + retry remain after dismissal.

### F10 — P1 security: first fulfillment did not bind Stripe intent to purchase — fixed

- **Reproduce before fix:** fake a succeeded intent whose package metadata, user metadata, amount, or currency differs from request/local row; first confirmation fulfills it.
- **Fix:** fetch and verify intent identity/status, package/user metadata, currency, expected amount, and amount received before first fulfillment.
- **Verify:** four mismatch regressions grant nothing; matching and idempotent cases pass; real sandbox success passes.

### F11 — P1: gallery paid-result continuation opened empty workspace — fixed

- **Reproduce before fix:** open My photos; choose a paid `instant_headshot`; click Continue in Photo Workspace; `/resumable-preview?previewId=<paid>` returns null and upload empty state appears.
- **Fix:** restore explicitly requested owned headshots, promoted previews, augmentations, and refinements; add `isPaidCandidate` so restored work cannot be repurchased/promoted.
- **Verify:** four-mode, ownership, no-generation, no-spend API tests; available/exhausted UI tests; real browser finishing/export.

### F12 — P1: refinement incorrectly checked spent candidate slots — fixed

- **Reproduce before fix:** consume package candidates while leaving refinements; select result; refinement action remains disabled.
- **Fix:** generation eligibility accepts explicit refinement intent and checks refinement allowance; premium-processing re-entry blocked.
- **Verify:** browser refinement 5→4 while candidates stay 0; focused available/exhausted tests.

### F13 — P1 accessibility: dark workspace placed light text on light panels — fixed

- **Reproduce before fix:** set dark theme; reopen paid result; compute foreground/background on visible text; about 50 candidates are near 1:1 and inspector stretches into blank space.
- **Fix:** scoped dark surface/status tokens, selected/upload backgrounds, stronger focus styles, muted-text correction, non-stretching inspector.
- **Verify:** sampled dark 390px paid view has 0 computed candidates, 0 broken visible images, no overflow. Light 1440px leaves only 3.68:1 brand logotype (logotype exemption). Not a WCAG certification.

### F14 — P1: promoted preview did not consume its Starter slot — fixed

- **Reproduce before fix:** generate Free Preview; buy Starter; confirm 2 remaining generations; receive 3 results but entitlement says 1 candidate remains.
- **Fix:** allowance preflight includes intended promotion; successful continuation consumes actual returned candidate count; idempotent retry consumes nothing twice.
- **Verify:** real browser ends candidates 0; tests cover valid promotion, oversized request rejected before provider, retry idempotency.

### F15 — P1: export zoom did not reliably alter framing — fixed

- **Reproduce before fix:** export a red/white edge fixture at zoom 100 and 140; final platform crop shows same sampled edge. Export `original_high_res` at 140 and output dimensions change 100→140.
- **Fix:** affine-transform inside fixed source frame before platform crop/resize; white background for zoom-out.
- **Verify:** image tests check zoom pixel movement, white zoom-out padding, unchanged dimensions, left/right crop anchors, all 12 formats/names/dimensions.

### F16 — P1: expired entitlement was reported active — fixed

- **Reproduce before fix:** store status Active with `ExpiresAt` in past and remaining allowance; GET entitlements returns `active`; premium preflight can proceed before consumption later rejects.
- **Fix:** DTO reports `expired` when due; active queries already enforce expiry.
- **Verify:** candidates/refinements/augmentations/export all reject expired row; premium endpoint rejects before credit/provider work.

### F17 — P2 accessibility: 390px app header hid theme control — fixed

- **Reproduce before fix:** authenticated workspace at width ≤460px; inspect/tab header; `.theme-toggle { display:none }` makes theme unavailable.
- **Fix:** retain theme control at mobile width.
- **Verify:** at 390px it is visible, focused and activated with Enter, changes theme, and does not add overflow.

### F18 — P2 copy: API implied nonexistent standalone add-on checkout — fixed

- **Reproduce before fix:** exhaust Pro augmentation allowance and submit; response says “Unlock a Pro Package or add-on,” but package catalog has no add-on SKU/path.
- **Fix:** message now says “Unlock a Pro Package.” Pro-included augmentations remain unchanged.
- **Verify:** exhaustion assertions for all 8 supported augmentation types.

### F19 — P1: package request error left permanent loading state — fixed

- **Reproduce before fix:** make `/api/profilephotoworkflow/packages` return HTTP 503; open `/pricing`; “Loading profile photo packages...” remains because Observable `complete` does not run after `error`; Retry never appears.
- **Fix:** move loading cleanup to RxJS `finalize`.
- **Verify:** `pricing-error-retry-red.txt` captures failure; `pricing-error-retry-green.txt` captures fix; final Playwright injects 503, displays Retry, then passes retry through to live API and recovers 3 cards.

### F20 — P2 accessibility/HTML: hidden upload input had no name and was nested inside a button — fixed

- **Reproduce before fix:** inspect authenticated empty workspace accessibility controls; hidden file input is a second interactive descendant of upload button and has no accessible name.
- **Fix:** move file input outside button, keep programmatic trigger, add `aria-label="Choose a source photo"`.
- **Verify:** final browser inventory reports 0 unnamed controls across registration, desktop/mobile workspace, pricing, and scored workspace; upload and score browser checks still pass.

## Real payment and entitlement evidence

- Standard browser flow: Free Preview → Starter card form → successful test payment → workspace → explicit consent → 2 generated candidates + promoted preview. Final Starter: package uses 0, candidates 0, refinements 2, augmentations 0, export available.
- Separate browser flow: Cancel grants 0; declined test card displays “Your card has been declined” and grants 0; Stripe 3DS test page appears, Complete returns to `/app/enhance`, exactly one Starter entitlement with 3 candidates.
- Signed sandbox flow: success webhook grants one Pro; actual replay HTTP 200; repeated confirmations/replay do not duplicate. Credits 289→289 and purchase rows 2→2 during duplicate checks. Decline HTTP 402 and cancellation HTTP 400 grant nothing.
- Exact artifacts: `preview-checkout.json`, `checkout-resilience.json`, `stripe-sandbox-payments.json`, `stripe-upgrade-webhooks.json`, and named checkout screenshots.

## Final reproducible checks and artifacts

| Command/check | Final result | Non-ignored artifact |
|---|---:|---|
| `dotnet test AI.ProfilePhotoMaker.API.Tests --no-restore` | **383 passed, 0 failed** | `api-final-rerun.txt`, `api-final-rerun-results.xml` |
| First final API run | 382 passed; timing aggregate hit exactly 85% vs `>85%`; isolated rerun passed, then full rerun passed | documented in API output/report |
| `npm test -- --watch=false` | **465 passed, 19 skipped** | `angular-karma-final.txt` |
| focused Playwright pricing/recovery | **6 passed** | `playwright-final.txt` |
| `npm run build` | **passed**, 26 SEO pages | `angular-build-final.txt` |
| `npm run lint` | **0 errors, 35 warnings** | `angular-lint-final.txt` |
| browser evidence collector | **8 checks**, real login/API plus labeled injections | `browser-verification-final.txt`, two trace ZIPs |
| `git diff --check` | **passed** | terminal verification |

All paths above are relative to `docs/testing/evidence/photo-workspace-design-audit/`. The collector source is `docs/testing/audit-browser-evidence.cjs`. Trace ZIPs contain Playwright DOM snapshots, screenshots, request timing, and sources. Authenticated tracing begins only after login so credential input is never written to evidence. Baseline inert-pricing artifacts are retained only as explicitly named `pricing-before-fix-*` / `before-fix-browser-baseline.json`; post-fix evidence uses `pricing-final-*`.

## Residual risks and explicit limitations

No known unresolved in-scope defect remains from exercised behavior. These are constraints or pre-existing debt:

1. **AI output quality/identity:** paid provider use was not authorized. Fixture results cannot assess visual quality, identity preservation, moderation, or provider latency.
2. **Production Stripe:** production was not inspected. Before separately authorized deployment, confirm deployed webhook endpoints use Clover-compatible versions.
3. **Response delivery boundary:** export allowance survives renderer failure; a network drop after server byte delivery cannot be made transactionally recoverable by this endpoint alone.
4. **Historical batch model:** paid-result recovery restores the selected owned image and current account allowances. Schema lacks complete historical batch/package association; reconstructing an old comparison set would be a new persistence feature, not claimed here.
5. **Pre-existing debt:** 19 Karma tests remain skipped, legacy workspace suite remains `xdescribe`, and NuGet reports advisories for SSH.NET, SQLitePCLRaw, and System.Security.Cryptography.Xml. Only the explicitly approved Stripe.NET compatibility upgrade was made.
6. **Standalone add-ons:** no separate SKU exists. Audit verifies Pro-included premium augmentations only.

Audit processes are stopped after verification. Generated fixture storage under API `dev/`/`generated-private/` is removed and must not be committed.
