# Third-Party Retention Alignment (Compliance Evidence)

Last updated: 2025-12-20  
Status: Partial - provider confirmation pending.

## 1. Providers in scope
- Replicate (model training and image generation)
- OpenAI (photo enhancement)

## 2. Replicate retention (predictions)
Source: https://replicate.com/docs/topics/predictions/data-retention

- API predictions: input parameters, outputs, output files, and logs are removed after 1 hour by default.
- Web UI predictions: retained indefinitely unless manually deleted.
- Manual deletion available from the Replicate dashboard.

## 2a. Replicate privacy policy (training data + general retention)
Source: https://replicate.com/privacy

- Replicate collects “Training Data” uploaded to train models.
- Retains customer personal information as long as necessary to provide services, or as required by law or legitimate interests.
- Residual copies may exist in backups for a limited period.

## 2b. Replicate terms (customer data definitions)
Source: https://replicate.com/terms

- Customer Data is defined as Inputs and Outputs.
- Customer Derivative Model is defined as a model fine-tuned using Inputs.
- Terms do not specify retention periods for training data or model artifacts.

Open items:
- Replicate training job retention for uploaded training data and model artifacts is not covered in the predictions retention doc.
- Interim disclosure: use privacy policy general retention language + user consent to processing until specifics are confirmed.
- Optional inquiry draft: `docs/deployment/evidence/compliance/replicate-retention-inquiry.md`.

## 3. OpenAI retention (image enhancement)
Source: https://platform.openai.com/docs/guides/your-data

- Default API data retention is generally up to 30 days for abuse monitoring (per OpenAI data controls).
- Image generation endpoints (including gpt-image-1) do not store application state; Zero Data Retention is available for gpt-image-1.
- Image inputs may be retained for manual review if CSAM detection triggers.

Open items:
- Confirm OpenAI data controls for gpt-image-1 within our account tier (ZDR eligibility and configuration).
- Capture an official OpenAI data controls reference for evidence (page access blocked in this environment).

## 4. Alignment actions
- Local deletion: API deletes local uploads and generated images per retention policy.
- Model deletion: API calls Replicate to delete user models on request.
- Training ZIP cleanup: training ZIPs are deleted when models are removed.

## Evidence references
- Replicate retention doc: https://replicate.com/docs/topics/predictions/data-retention
- Replicate delete flow: `AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs`
- Subprocessors list: `docs/deployment/evidence/legal/subprocessors.md`
- OpenAI data controls (reference pending): https://platform.openai.com/docs/guides/your-data
