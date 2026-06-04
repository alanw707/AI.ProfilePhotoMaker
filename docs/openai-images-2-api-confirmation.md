# OpenAI Image Edit API Confirmation

Date: 2026-05-15
Status: implementation evidence for rollout-safe OpenAI headshot provider

## Source checked

- Source: OpenAI public OpenAPI spec, `openai/openai-openapi` `master`, fetched from `https://raw.githubusercontent.com/openai/openai-openapi/master/openapi.yaml` during implementation verification.
- Endpoint checked: `POST /images/edits`.

## Confirmed request shape for the one-photo MVP

For the multipart image edit endpoint, the spec describes binary uploads via `image` and prompt/model fields in `multipart/form-data`.

Required/used fields for this project:

- `image`: one source image file for the MVP one-photo headshot flow.
- `model`: configured by `OpenAI:ImageModel`.
- `prompt`: generated server-side from the selected headshot intent.
- `size`: `1024x1024` for the MVP output.

The spec and SDK examples also show `image[]` for multiple-image edit examples. The MVP sends exactly one image, so the implementation now uses the single-image field name `image` and has a unit test that rejects `image[]` for this path.

## Confirmed model identifier for production-safe default

The implementation plan and rollout configuration identify the Images 2 model identifier as `gpt-image-2`. The public OpenAPI schema also contains `gpt-image-2`/`gpt-image-2-2026-04-21` parameter guidance for GPT image models, including supported arbitrary `WIDTHxHEIGHT` sizing rules. The project default is therefore `gpt-image-2` for the Images 2 pivot.

`OpenAI:ImageModel` remains configurable, so rollout can switch to a dated model snapshot or a fallback GPT image edit model without code changes if account access or API availability requires it.

## Verification hooks

- `OpenAIImageGenerationService` reads `OpenAI:ImageModel` and `OpenAI:ImageEditEndpoint` from configuration.
- `OpenAIImageGenerationServiceTests.EnhancePhotoQualityAsync_SendsConfiguredModelParameter` asserts multipart fields include `image`, `model`, `prompt`, and `size`, and do not include `image[]` for the one-photo MVP.
