# Tech-Spec: Professional Style Prompt Differentiation and Realism Guardrails

**Created:** 2025-12-28
**Status:** Completed

## Overview

### Problem Statement

Professional styles (Academic, Tech Professional, Startup, LinkedIn, Entrepreneur, Executive) are too similar in output, especially in pose and background. Academic sometimes lands on a blank background, and LinkedIn backgrounds trend to plain white/gray. We also see unrealistic accessories (e.g., two watches) and occasional unnatural facial expressions/skin. We previously tuned skin realism and must preserve or improve it without regression.

### Solution

Update database prompt templates via a new EF Core migration to differentiate these styles using distinct background cues, wardrobe hints, and subtle pose guidance. Extend negative prompts to preserve natural facial expressions/skin realism and prevent unrealistic accessories/poses. Keep existing skin realism tuning intact and only add constraints that improve fidelity.

### Scope (In/Out)

In scope:
- Update prompt/negative prompt text for `academic`, `linkedin`, `startup`, `tech-professional`, `entrepreneur`, `executive`.
- Introduce stronger background differentiation and subtle pose guidance.
- Add accessory and pose realism negatives (e.g., no multiple watches).
- Preserve existing skin realism negatives and quality negatives.

Out of scope:
- UI changes.
- Model changes or training.
- Schema changes.

## Context for Development

### Codebase Patterns

- Style prompt updates are implemented via EF Core migrations with SQL updates against `dbo.Styles` (e.g., `AI.ProfilePhotoMaker.API/Migrations/20251224150249_UpdateProfessionalClusterPromptsV2.cs`).
- Seed defaults live in `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs` (SeedStyles). These may be updated to match, but the migration is the authoritative production change.

### Files to Reference

- `AI.ProfilePhotoMaker.API/Migrations/20251224150249_UpdateProfessionalClusterPromptsV2.cs`
- `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs`
- `AI.ProfilePhotoMaker.API/Controllers/DiagnosticController.cs`

### Technical Decisions

- Use a new migration that updates `dbo.Styles` by `Name` for the targeted styles.
- Reuse the existing `SkinRealismNegativePrompt` and `DefaultQualityNegativePrompt` patterns from prior migrations; append new constraints rather than replacing.
- Do not modify schema. Only text updates.

## Implementation Plan

### Tasks

- [x] Draft updated PromptTemplate text per style with distinct background and wardrobe cues:
  - **Academic**: campus, library stacks, lecture hall, museum gallery; no blank background.
- **LinkedIn**: clean professional background with subtle variety such as soft neutral gradients (warm gray/ivory/muted taupe/soft slate), clean off-white or warm gray, or a minimal modern office interior with gentle bokeh; keep it clean and professional.
  - **Startup**: young energetic vibe; modern co-working or open office; casual-professional wardrobe (hoodie/crewneck or casual jacket), bright natural light.
  - **Tech Professional**: modern tech office or product lab; subtle monitors/whiteboards/server racks; calm, focused; smart casual (no hoodie).
  - **Entrepreneur**: personal brand leader; boutique office, studio, or upscale cafe; premium smart casual; warm, confident.
  - **Executive**: formal corporate boardroom or high-rise office; classic formal suit; composed, authoritative.
- [x] Add subtle pose guidance:
  - Professional styles (academic/linkedin/tech-professional/executive): relaxed shoulders, slight 3/4 angle or gentle head tilt, natural smile, no dramatic gestures.
  - Startup/entrepreneur: slightly wider variety but still natural (no exaggerated action).
- [x] Add or extend negative prompts to prevent unnatural looks/accessories:
  - Maintain current skin realism list (waxy/plastic/airbrushed, etc.).
  - Add: forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt.
  - Add accessory sanity: multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hats, visible logos.
  - Add pose constraints: full-body action, dramatic gestures, arms flailing, unnatural hand positions.
- [x] Implement a new migration (SQL updates) for the six styles using shared negative prompt constants.
- [x] (Optional) Align seed defaults in `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs` and the dev-only `stylesToAdd` list in `AI.ProfilePhotoMaker.API/Controllers/DiagnosticController.cs` so dev resets match production prompts.
- [x] Validate in dev DB by querying updated style rows and generating a small set of sample outputs.

### Acceptance Criteria

- [x] Each of the six styles has a distinct background cue and wardrobe/pose guidance.
- [x] Academic never suggests blank/neutral background; LinkedIn allows clean white/gray or neutral gradients without repeating the same few colors.
- [x] Tech Professional no longer resembles Startup (no coworking or hoodie cues; calmer, more polished).
- [x] Entrepreneur and Executive are clearly distinct (entrepreneur: personal brand, boutique/cafe; executive: formal corporate boardroom/high-rise).
- [x] Negative prompts include accessory sanity and natural expression constraints while preserving prior skin realism tuning.
- [x] Migration updates only text fields and does not alter schema.

## Additional Context

### Dependencies

- Existing style prompt tuning in `20251224150249_UpdateProfessionalClusterPromptsV2.cs` should be reused as a base to avoid regressions.

### Testing Strategy

- Run a SQL check in dev: `SELECT Name, PromptTemplate, NegativePromptTemplate FROM Styles WHERE Name IN (...)`.
- Generate a small sample per style and visually verify background and pose differentiation.

### Notes

- Keep prompts grounded and professional; prefer subtle pose variation for conservative styles.
- Avoid adding constraints that could reduce output quality or reintroduce waxy skin; only append to existing realism negatives.

## Review Notes

- Adversarial review completed
- Findings: 10 total, 10 fixed, 0 skipped
- Resolution approach: auto-fix
