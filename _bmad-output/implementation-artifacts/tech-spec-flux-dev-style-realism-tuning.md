# Tech-Spec: Flux Dev Style Realism Tuning (Reduce Waxy Skin)

**Created:** 2025-12-26
**Status:** Completed

## Overview

### Problem Statement
Style generation (FLUX dev trained-model predictions) is producing overly waxy/plastic, over-sharpened skin. This is most noticeable in casual/relaxed styles, while professional/studio styles still need to remain polished. We need a backend-only adjustment that yields more natural skin texture without UI changes or external LoRAs.

### Solution
Introduce style-aware generation tuning in the backend:
- Adjust `guidance_scale` and `num_inference_steps` based on style group (Pro vs Casual/Relaxed).
- Add a subtle, randomized, human-skin realism modifier to the positive prompt (small set of phrases; exclude "slight skin redness" and "fine facial hair").
- Keep enhancements (Kontext Pro) unchanged.
- Keep all adjustments configurable and safe defaults if a style is not classified.

### Scope (In/Out)
**In scope**
- Style generation pipeline (Replicate API predictions for trained models).
- Style-based parameter selection (guidance/steps) with minimal changes to prompt text.
- Subtle randomized prompt modifiers for realism.
- Configuration-based style grouping with sensible defaults.

**Out of scope**
- UI changes or user-facing controls.
- Enhancement/transform models (Kontext Pro or other non-style generation flows).
- External LoRAs or external URLs.

## Context for Development

### Codebase Patterns
- Replicate prediction payloads are built in `ReplicateApiClient.GenerateImagesAsync`.
- Style prompts/negative prompts are stored in `Styles` table and read at generation time.
- Replicate config values live in `appsettings*.json` under `Replicate`.
- Background jobs also call the same `GenerateImagesAsync` method (shared entry point).

### Files to Reference
- `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs` (prediction payload)
- `AI.ProfilePhotoMaker.API/Services/ImageProcessing/MockReplicateApiClient.cs` (mock parity)
- `AI.ProfilePhotoMaker.API/appsettings.json`
- `AI.ProfilePhotoMaker.API/appsettings.Development.json`
- `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs` (entry points)
- `AI.ProfilePhotoMaker.API/Services/PendingGenerationService.cs` (background generation)
- `AI.ProfilePhotoMaker.API/Models/Style.cs` (style naming)

### Technical Decisions
- **Style grouping:** Use a backend config list of style names for `CasualRelaxedStyles` and `ProStyles`. Names are case-insensitive. If a style is not in either list, use current defaults (guidance 3, steps 28) to avoid surprise regressions.
- **Parameter targets (initial defaults):**
  - Pro styles: `guidance_scale = 2.8`, `num_inference_steps = 34`.
  - Casual/Relaxed styles: `guidance_scale = 2.3`, `num_inference_steps = 40`.
- **Prompt realism modifier:** append 1 random phrase (optionally 0 in some cases) from a short list to the **positive** prompt. Keep it subtle and avoid repeating the same exact phrasing every time.
  - Allowed examples: `natural skin texture`, `subtle skin pores`, `soft natural sheen`, `realistic skin detail`, `unretouched look`, `candid lighting`.
  - Exclude phrases: `slight skin redness`, `fine facial hair`.
- **No change** to enhancement flow (`EnhancePhotoAsync`).

## Implementation Plan

### Tasks
- [x] Add config section for style tuning defaults and style groups (casual/pro). Provide safe defaults in code if config missing.
- [x] Implement a helper in `ReplicateApiClient` to resolve style tuning (guidance/steps) based on style name and config lists.
- [x] Implement prompt realism modifier injection:
  - Build a small list of allowed phrases.
  - Use a randomized selection per generation request (shared across outputs in that request).
  - Keep modifier subtle (0–1 phrases recommended; optionally 1 always for simplicity).
- [x] Update `ReplicateApiClient.GenerateImagesAsync` to use the resolved guidance/steps and modified prompt.
- [x] Mirror changes in `MockReplicateApiClient` so test/mocked behavior stays consistent.
- [x] Add unit tests in `AI.ProfilePhotoMaker.API.Tests`:
  - Style group selection is case-insensitive.
  - Pro styles use pro defaults; casual styles use casual defaults.
  - Unknown styles fall back to current defaults.
  - Prompt modifier uses only allowed phrases and never banned ones.

### Acceptance Criteria
- [ ] Casual/relaxed styles produce visibly less waxy/airbrushed skin (manual verification by Alan).
- [ ] Pro styles remain polished but slightly more realistic (manual verification).
- [ ] No UI changes; no new user controls.
- [ ] Enhancement/transform workflows unchanged.
- [ ] Style generation remains functional for unclassified styles (no regressions).

## Additional Context

### Dependencies
- No new external services or LoRAs.
- Minor config changes under `Replicate` in appsettings.

### Testing Strategy
- Unit tests for tuning logic and prompt modifier selection.
- Manual visual QA by Alan on a small batch of Pro vs Casual styles.

### Notes
- If style grouping needs to change, edit the config lists (preferred) rather than hard-coding in multiple places.
- Keep the modifier list short and subtle to avoid over-steering the prompt.

## Review Notes
- Adversarial review completed.
- Findings: 10 total, 8 fixed, 2 skipped.
- Resolution approach: auto-fix.
