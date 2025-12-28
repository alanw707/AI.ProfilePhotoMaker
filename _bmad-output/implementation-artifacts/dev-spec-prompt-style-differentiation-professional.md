# Dev-Spec: Professional Style Prompt Differentiation and Realism Guardrails

**Source Tech-Spec:** `_bmad-output/implementation-artifacts/tech-spec-prompt-style-differentiation-professional.md`
**Created:** 2025-12-28
**Status:** Ready for Implementation

## Goal

Differentiate professional styles (academic, linkedin, startup, tech-professional, entrepreneur, executive) with distinct backgrounds, wardrobe hints, and subtle pose guidance while preserving and extending existing skin realism and quality negatives.

## Non-Goals

- UI or model changes.
- Schema changes.
- Prompt updates for styles outside the six listed.

## References

- `AI.ProfilePhotoMaker.API/Migrations/20251224150249_UpdateProfessionalClusterPromptsV2.cs`
- `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs`
- `AI.ProfilePhotoMaker.API/Controllers/DiagnosticController.cs`

## Implementation Plan

### 1) Define shared negative prompt fragments

In the new migration, keep the existing constants and add one more shared fragment for expression/accessory/pose realism.

- `SkinRealismNegativePrompt` (reuse existing string)
- `DefaultQualityNegativePrompt` (reuse existing string)
- `ExpressionAccessoryPoseNegativePrompt` (new)

Proposed `ExpressionAccessoryPoseNegativePrompt` value:

```
forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions
```

### 2) Create a new migration

Add a migration (e.g., `UpdateProfessionalClusterPromptsV3`) that updates only text fields in `dbo.Styles` for the six styles. Follow the SQL pattern from `20251224150249_UpdateProfessionalClusterPromptsV2.cs` with `DECLARE @skin`, `DECLARE @quality`, and `DECLARE @realism` (for the new fragment).

### 3) Prompt templates and negative prompts

Use the following prompt and negative prompt templates. Keep `{subject}`, `{gender}`, and `{ethnicity}` tokens.

#### Academic

PromptTemplate:
```
{subject}, academic professional portrait of {gender} {ethnicity}, scholarly wardrobe (tweed blazer or cardigan with button-down), university library stacks or lecture hall background, subtle campus ambiance, thoughtful expression, relaxed shoulders, slight 3/4 angle, soft natural window light, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution
```

NegativePromptTemplate (style-specific segment to append between @quality and @realism):
```
hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup
```

#### LinkedIn

PromptTemplate:
```
{subject}, LinkedIn-ready headshot of {gender} {ethnicity}, business-casual wardrobe (blazer or crisp button-down, no tie), muted professional backdrop with soft gradient (light blue, warm beige, or soft teal), direct eye contact, warm confident smile, relaxed shoulders, soft diffused daylight, natural skin texture, minimal retouching, head-and-shoulders framing, sharp focus
```

NegativePromptTemplate (style-specific segment):
```
hoodie, t-shirt, tank top, athletic wear, coworking space, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, plain white background, plain gray background, full body shot, watermark, text
```

#### Startup

PromptTemplate:
```
{subject}, startup founder portrait of {gender} {ethnicity}, casual-professional wardrobe (hoodie, crewneck, or casual jacket), modern coworking or open office background, bright natural window light, approachable energetic expression, relaxed posture, slight 3/4 angle, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field
```

NegativePromptTemplate (style-specific segment):
```
formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text
```

#### Tech Professional

PromptTemplate:
```
{subject}, modern tech professional headshot of {gender} {ethnicity}, smart-casual tech attire (open-collar shirt or fine knit sweater, no hoodie, no tie), contemporary tech office or product lab background with subtle monitors or whiteboards, calm focused expression, relaxed shoulders, gentle head tilt, clean cool-neutral palette, soft diffused lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution
```

NegativePromptTemplate (style-specific segment):
```
suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text
```

#### Entrepreneur

PromptTemplate:
```
{subject}, entrepreneur personal-brand portrait of {gender} {ethnicity}, premium smart-casual wardrobe (tailored blazer without tie or premium knit), boutique office, studio, or upscale cafe background, warm confident expression, relaxed shoulders, slight 3/4 angle, cinematic but natural lighting, natural skin texture, minimal retouching, medium close-up portrait, shallow depth of field
```

NegativePromptTemplate (style-specific segment):
```
formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text
```

#### Executive

PromptTemplate:
```
{subject}, executive leadership portrait of {gender} {ethnicity}, formal suit with crisp shirt and tie, corporate boardroom or high-rise office background, composed authoritative expression, relaxed shoulders, subtle 3/4 angle, polished professional lighting, natural skin texture, minimal retouching, head-and-shoulders framing, high-resolution
```

NegativePromptTemplate (style-specific segment):
```
hoodie, t-shirt, casual streetwear, coworking space, cafe, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text
```

### 4) SQL update pattern

For each style, set:

```
PromptTemplate = '<prompt template>',
NegativePromptTemplate = CONCAT(@quality, ', <style-specific segment>, ', @realism, ', ', @skin),
UpdatedAt = GETUTCDATE()
```

Keep `WHERE IsActive = 1 AND Name = '<style-name>'`.

### 5) Optional: Align seed defaults and dev-only lists

If desired, update the style entries for the six styles in:

- `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs` (SeedStyles)
- `AI.ProfilePhotoMaker.API/Controllers/DiagnosticController.cs` (stylesToAdd)

This is optional but keeps dev resets in sync with production.

## Validation

- SQL sanity check:
  ```
  SELECT Name, PromptTemplate, NegativePromptTemplate
  FROM Styles
  WHERE Name IN ('academic', 'linkedin', 'startup', 'tech-professional', 'entrepreneur', 'executive');
  ```
- Generate sample outputs for each style and visually confirm:
  - Distinct backgrounds and wardrobe cues.
  - Academic has a non-blank background.
  - LinkedIn avoids a plain white/gray bias and stays professional.
  - Tech Professional does not resemble Startup.
  - Entrepreneur and Executive are clearly distinct.
  - No regression in skin realism or expression naturalness.

## Risks and Notes

- Keep prompts grounded and avoid overly restrictive negatives that could reduce output quality.
- Avoid apostrophes in SQL prompt strings to prevent escaping issues.
