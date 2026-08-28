# `/app/enhance` Asset Manifest

## produce

None. The code-led Studio Proof Desk direction requires no new raster material.

## direct

| id | source_crop | output_path | strategy | dimensions | format | transparency | deviations | qa_status |
|---|---|---|---|---|---|---|---|---|
| existing-professional-proofs | Existing repository photography | `AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/` | Reuse existing production portrait assets; runtime candidates remain user/provider images | Existing source dimensions | Existing JPEG/PNG | Opaque | No files modified or duplicated | accepted |
| existing-brand-mark | Existing repository logo | `AI.ProfilePhotoMaker.UI/src/assets/Logo.PNG` | Reuse incumbent product mark in shared navigation | Existing source dimensions | PNG | Existing alpha | Outside route-local redesign | accepted |

## semantic

| id | implementation | notes | qa_status |
|---|---|---|---|
| fulfillment-ticket | Semantic `section`, `dl`, and text progressbar; CSS owns paper, rule, stamp, and progress treatment | Candidate/refinement/add-on/export truth remains live text | accepted |
| proof-desk | Semantic source/selected figure regions plus runtime `<img>` elements; CSS owns framing, crop, scale, and selection mark | No UI text baked into imagery | accepted |
| contact-sheet | Native buttons with runtime candidate images, `aria-pressed`, numeric labels, and CSS proof marks | Scrollable at narrow viewports | accepted |
| adjustment-controls | Native range inputs and labels; CSS owns production-ticket layout | Values remain live and keyboard-operable | accepted |
| platform-export-list | Native checkboxes and dimensions; CSS owns two-column/one-column adaptation | No raster output | accepted |
| icons | Existing inline SVG for upload/download/share/save | Semantic labels remain text; no new icon raster | accepted |

## execution_order

1. No produced assets required.
2. Compose existing/runtime portraits inside semantic proof frames.
3. Keep all controls, status notation, and marks in HTML/CSS/SVG.

## blockers

None.

## assumptions

- Provider-generated and user-uploaded photos remain runtime content, not design-system assets.
- Existing repository rasters were not created or replaced by this redesign, so no new generation provenance was embedded.
