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
- Package selection should move into the sticky action bar as a compact selector, with Free Preview selected by default.
- Post-result upsell copy should be redesigned around visible value: more candidates, best shot selector, export kit, and refinements.
- Fun styles should stay hidden unless active API styles exist for them; do not create fake fun styles client-side.
- Empty style groups should be hidden.
- Role selection should remain out of the main path; style cards carry professional intent.
- If active styles fail to load, show a compact unavailable state instead of a stale hardcoded catalog.
- Generation errors for unavailable styles should ask the user to choose another style.
- Full paid-package generation and export verification remain separate follow-up testing after the free-preview vertical slice.
