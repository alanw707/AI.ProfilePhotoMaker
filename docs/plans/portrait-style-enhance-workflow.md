# Portrait Style Enhance Workflow Plan

## Resolved decisions

- `/enhance` should make **Portrait styles** the primary post-upload choice.
- Portrait styles use the existing active `Style` records and `Style.PromptTemplate` prompts.
- Generation remains instant through OpenAI Images 2; no Replicate custom model training in this path.
- Existing style preview assets should be reused through the current preview service.
- Avoid style schema/data changes for now; derive grouping/order/badges in frontend from existing style names.
- Show professional styles first; keep playful/fun styles visually separate only when active styles exist for them.
- Use `Generate headshot` before generation; use `candidate` language after results exist.
- Free Preview stays default before generation; Starter/Pro are upsold after first preview.

## UX target

After upload, `/enhance` becomes a style-first chooser:

1. Left sticky panel: uploaded photo, score summary, choose different photo.
2. Right panel: `Choose your portrait style`.
3. Tabs: `Recommended`, `More styles`, and `Fun` only when those groups contain active styles.
4. Style cards show preview image, style name, badge/intent, and short copy.
5. Sticky action bar shows selected style, package summary, and `Generate headshot`.
6. Role dropdown is removed from the main path; style choice carries professional intent.

## Backend plan

- Accept selected style name/code through existing headshot generation request.
- Validate selected style is active.
- Resolve existing `Style.PromptTemplate` server-side.
- Pass resolved prompt to OpenAI image generation.
- Return clear user error if style is unavailable.
- Keep existing generic headshot prompt as fallback only where safe.

## Frontend plan

- Load active styles through existing `StyleService`.
- Resolve preview URLs through existing `StylePreviewService`.
- Derive temporary UI metadata from normalized style name:
  - group: recommended / more / fun
  - display order
  - badge label
- If style has no mapping, place it in `More styles` after mapped styles.
- Hide empty groups.
- Send selected active style name to `/headshots/generate`.

## Upgrade workflow plan

V1 uses a preview-first paid upgrade path.

1. Hide the pre-generation paid package selector for users without an active Starter or Pro entitlement.
2. Generate Free Preview first by default.
3. Render the Free Preview at the same underlying generation quality as paid candidates, but gate user value with watermarking and export restrictions.
4. After preview, show clear Starter and Pro upgrade cards instead of a locked package dropdown.
5. Use the existing CreditPackage payment flow as the temporary checkout bridge through `OutcomePackageDefinition.InternalCreditPackageId`.
6. Return from payment to `/app/enhance` with the outcome package context and reload entitlements.
7. Show a confirmation CTA before spending the paid entitlement: “Generate 2 more Starter candidates” or “Generate 8 more Pro candidates.”
8. If the user keeps the same source photo and portrait style, promote the Free Preview candidate to candidate #1 and generate only the remaining paid candidate slots.
9. If the user changes portrait style before paid generation, start a new paid candidate set and clearly state that the preview no longer counts toward the paid package.
10. Show regeneration cost before action: user-requested regenerate consumes one refinement; provider/storage failures get a free retry.

V1.1 should add stricter cost controls: same image hash + style preview reuse, daily free preview caps, entitlement reservation/finalization, and hardened watermark generation.

## Verification plan

- API build.
- UI lint/build.
- Local container rebuild for API and frontend.
- API smoke: `/api/style` returns active styles.
- Authenticated Playwright smoke:
  - login
  - upload image
  - style chooser appears
  - recommended styles render
  - more styles render
  - empty fun tab hidden
  - selected style reflected in action bar
  - no browser console/page errors
- End-to-end generation smoke when provider credentials and cost budget allow.

## Resolved defaults for remaining design questions

- Style card copy should use curated frontend copy for mapped styles and fall back to `Style.Description` only for unmapped styles.
- Existing preview images are acceptable for the vertical slice, but the premium chooser should later get consistent portrait-preview crops and lighting.
- Package selection should not be the default pre-generation decision for users without an active entitlement; Free Preview should be generated first, then Starter/Pro should be offered as explicit upgrade cards after preview.
- Post-result upsell copy should be redesigned around visible value: more candidates, best shot selector, export kit, and refinements.
- Fun styles should stay hidden unless active API styles exist for them; do not create fake fun styles client-side.
- Empty style groups should be hidden.
- Role selection should remain out of the main path; style cards carry professional intent.
- If active styles fail to load, show a compact unavailable state instead of a stale hardcoded catalog.
- Generation errors for unavailable styles should ask the user to choose another style.
- Full paid-package generation and export verification remain separate follow-up testing after the free-preview vertical slice.
