# OpenAI Enhancement (gpt-image-1)

This is the provider-specific reference for our OpenAI image editing integration used by the photo enhancement feature. The overall enhancement flow is documented in `docs/product/PRD.md` and `docs/operations/PHOTO_PROCESSING.md`.

OpenAI is used for select enhancement styles (for example: `chibi`, `pixar_3d`, `studio_ghibli`). These requests route to `POST /api/enhancement/enhance` and consume 2 credits per enhancement.

Key points:
- Endpoint: `POST https://api.openai.com/v1/images/edits`
- Required fields (multipart/form-data):
  - `model=gpt-image-1`
  - `image` (PNG we generate server-side; square up to 1024)
  - `prompt` (style-specific text)
  - `size=1024x1024`
- Mask: optional; currently omitted for reliability.
- Response handling: accept either `url` or `b64_json` and return a Replicate-compatible payload to the UI.

Implementation files:
- API service: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/OpenAIImageGenerationService.cs`
- API endpoint: `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`
- UI flow: `AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/`

More detail: see `AI.ProfilePhotoMaker.UI/docs/openai-implementation.md`.
