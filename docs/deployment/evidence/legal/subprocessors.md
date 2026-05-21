# Subprocessors (Third-Party Processors)

Last updated: 2025-12-20  
Status: Signed off (legal).

## Evidence (production)
- URL: https://aiprofilephotomaker.com/legal/subprocessors
- Screenshot: docs/deployment/evidence/legal/subprocessors-production.png
- Captured: 2025-12-23T20:35:36Z

This list covers third-party service providers that process user data on our behalf. Update this list when providers change.

| Provider | Service | Purpose | Data Categories | Notes / Links |
| --- | --- | --- | --- | --- |
| OpenAI | Instant headshot generation and photo enhancement | Generate professional headshots and enhanced images using configured GPT Image model | Uploaded photos, prompts, output images | https://openai.com/policies/privacy-policy |
| Replicate | Optional/advanced model training and image generation | Train custom models and generate larger styled photo packs | Uploaded photos, prompts, model metadata | https://replicate.com/privacy ; https://replicate.com/docs/topics/predictions/data-retention |
| Stripe | Payments | Credit package payments and billing | Payment metadata, email, transaction IDs | https://stripe.com/privacy |
| Google | OAuth login (optional) | Authenticate users who choose Google sign-in | Email, name, OAuth profile ID | https://policies.google.com/privacy |
| Microsoft Azure (optional) | Hosting and blob storage | API hosting and image storage if configured | Account data, images, logs | https://azure.microsoft.com/legal/privacy |

Notes:
- If local storage is used (no cloud storage), the Azure entry is not applicable.
- If Google sign-in is not enabled, the Google entry is not applicable.
 - Each provider may retain data as needed to provide services or comply with legal and security obligations; see their policies for details.
