# OpenAI Enhancement and Instant Headshot Generation (gpt-image-2)

This is the provider-specific reference for our OpenAI image editing integration used by the photo enhancement feature and the OpenAI-first instant headshot MVP. The overall flow is documented in `docs/product/PRD.md`, `docs/operations/PHOTO_PROCESSING.md`, and `docs/openai-images-2-pivot-implementation-plan.md`.

OpenAI is the default provider for the feature-flagged instant headshot path. These requests route to `POST /api/headshots/generate`, use a server-side stored upload path, create a generated gallery record, and consume `instant_headshot_generation` credits. OpenAI also remains available for select enhancement styles through `POST /api/enhancement/enhance` during the transition.

Key points:
- Endpoint: `OpenAI:BaseUrl` + `OpenAI:ImageEditEndpoint` (default `https://api.openai.com/v1/images/edits`)
- Required fields (multipart/form-data):
  - `model` from `OpenAI:ImageModel` (default: `gpt-image-2`, selected for the Images 2 pivot and configurable for rollout safety)
  - `image` (PNG we generate server-side; square up to 1024)
  - `prompt` (style-specific text)
  - `size=1024x1024`
- Mask: optional; currently omitted for reliability.
- Response handling: accept either `url` or `b64_json` and return a Replicate-compatible payload to the UI.

Implementation files:
- Low-level API service: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/OpenAIImageGenerationService.cs`
- Headshot provider: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/OpenAIHeadshotGenerationProvider.cs`
- Headshot orchestration: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/HeadshotGenerationService.cs`
- Headshot API endpoint: `AI.ProfilePhotoMaker.API/Controllers/HeadshotsController.cs`
- Legacy enhancement endpoint: `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`
- UI flow: `AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/`

More detail: see `AI.ProfilePhotoMaker.UI/docs/openai-implementation.md`.
