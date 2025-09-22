## Product Requirements Document (PRD) — AI.ProfilePhotoMaker

Version: 1.0
Last updated: 2025-07-28

### 1) Product Summary
- AI-powered profile/headshot photo maker with: user auth (email/password + Google OAuth), photo upload, custom model training on Replicate, styled image generation, photo enhancement, credits/credit packages, and automated data retention.
- Tech: ASP.NET Core Web API + Angular frontend, EF Core + SQL Server, optional Azure Blob Storage.

### 2) Goals
- Enable users to generate professional profile photos from their selfies using AI styles.
- Provide a basic tier with weekly free credits, and paid credits via credit packages (Stripe integration WIP).
- Ensure reliability via hybrid DB/filesystem syncing and webhook-driven flows.
- Enforce privacy via retention: input photos deleted after 7 days, generated photos after 30 days.

### 3) Non‑Goals
- No full subscription billing lifecycle in MVP (credit packages are primary; Stripe webhook flow present but not fully enforced in UI).
- No admin analytics dashboard in MVP.
- No enterprise SSO or multi-tenant roles.

### 4) Users & Personas
- New user: wants quick professional headshots; may try basic tier first.
- Returning user: trained a model and returns to generate more styled photos.
- Guest visitor: can view credit packages and style previews.

### 5) User Stories (Core)
- As a user, I can register/login (email/password or Google) to manage my photos and credits.
- As a user, I can upload up to 20 selfies at a time for training zip creation.
- As a user, I can select styles, train a custom model (with purchased credits), and generate styled photos (with purchased credits).
- As a basic user, I can enhance photos using weekly credits without training a model.
- As a user, I can view, download, and delete my images.
- As a user, I can purchase credit packages and see my credit status/history.
- As a user, I can export my data and delete photos/model/account.

### 6) Functional Requirements

Auth & Profile
- Email/password registration and login (JWT), Google OAuth login and callback with session for state.
- Profile auto-creation on first OAuth login with default weekly credits.
- Endpoints: `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/google-oauth-url`, `GET /api/auth/external-login/{provider}`, `GET /api/auth/external-login-callback`.
- Profile CRUD and stats: `GET/POST/PUT/DELETE /api/profile`, data export, data deletion, account deletion.

Image Upload & Management
- Upload: multipart `POST /api/image/upload` with constraints: max 20 files per request; per-file validation (jpg/jpeg/png/webp), magic-bytes verification, size ≤ 10MB; saves to `/uploads/{userId}` or `/enhanced/{userId}`.
- Training ZIP: auto when `ForTraining=true` on upload or via `POST /api/image/create-training-zip`; min 10 images required; ZIP path `/training-zips/{userId}.zip`; list/get/delete endpoints provided.
- Gallery: `GET /api/image/images` returns normalized absolute URLs; `DELETE /api/image/images/{imageId}` hard deletes files and DB record; additional debug/repair endpoints available in development.

Styles & Selection
- Style catalog from DB (name, description, prompt templates, negative prompts).
- User can select multiple styles; selections persisted.
- Endpoints: `GET /api/style`, `GET /api/style/{id}`, `GET /api/style/name/{name}/template`, `POST /api/style/select`, `GET /api/style/user-selected`.

Custom Model Training (Replicate)
- Training requires purchased credits (15 credits). Prevent retrain when a READY model already exists.
- Webhook `POST /api/webhooks/replicate/training-complete` updates model record and can auto-kick off generation for selected styles.
- Endpoints: `POST /api/replicate/train`, `GET /api/replicate/train/status/{trainingId}`.

Styled Image Generation (Replicate)
- Requires purchased credits (5 credits per image output). Generates 1–4 images per request; batch generation supported across multiple styles.
- Endpoints: `POST /api/replicate/generate`, `POST /api/replicate/generate/batch`, `GET /api/replicate/generate/status/{predictionId}`.

Photo Enhancement (Kontext Pro)
- Basic tier feature using weekly credits (1 per enhancement). Enhances a single uploaded image.
- Endpoint: `POST /api/replicate/enhance`.

Credits & Payments
- Weekly free credits for Basic tier: 5, reset every 7 days.
- Purchased credits added via credit packages; status, packages (public), purchase, history provided; PaymentIntent mocked in dev.
- Endpoints: `GET /api/credit/status`, `GET /api/credit/packages` (public), `POST /api/credit/purchase`, `GET /api/credit/history`, `POST /api/credit/create-payment-intent` (mock), `GET /api/credit/costs`, `GET /api/credit/payment-config`.

Retention & Privacy
- Retention policy: input photos (original uploads) deleted after 7 days; AI generated photos deleted after 30 days. Background service runs periodic cleanup; manual endpoints to inspect/delete expired images.
- Endpoints: `GET /api/retentionpolicy/expired-images`, `POST /api/retentionpolicy/delete-expired`, `POST /api/retentionpolicy/initialize-retention-dates`, `GET /api/retentionpolicy/policy-info`.

Webhooks & File Downloading
- `POST /api/webhooks/replicate/prediction-complete` validates signature; downloads generated images to `/generated/{userId}`, creates DB records, sets retention.

### 7) Key Business Rules & Limits
- Upload limits: max 20 images per request; file size ≤ 10MB; allowed types: .jpg/.jpeg/.png/.webp with signature validation.
- Training ZIP: requires ≥ 10 original uploads.
- Credits:
  - Weekly (Basic): 5; resets every 7 days.
  - Costs: enhancement = 1 (allows weekly), model_training = 15 (purchased only), styled_generation = 5 per image (purchased only).
  - Consumption occurs after successful Replicate API call.
- Generation: 1–4 outputs per style per request; batch generation allowed; model availability checked.
- Retention: originals 7 days; generated 30 days; scheduled at record creation and enforced by background job.

### 8) Data Model (high‑level)
- ApplicationUser (Identity)
- UserProfile: UserId, Credits, PurchasedCredits, LastCreditReset, SubscriptionTier, ProcessedImages, UsageLogs.
- ProcessedImage: IsOriginalUpload, IsGenerated, OriginalImageUrl, ProcessedImageUrl, Style, CreatedAt, ScheduledDeletionDate.
- Style + UserStyleSelection: Active styles and user selections.
- ModelCreationRequest: UserId, ReplicateModelId, TrainedModelVersion, Status, CompletedAt, TrainingImageZipUrl.
- CreditPackage, CreditPurchase.
- UsageLog: action, creditsCost, creditsRemaining.

### 9) External Services & Config
- Replicate API: training, prediction, enhancement; webhooks for training/prediction complete; signature validation with 5-minute timestamp window.
- Stripe: library wired; dev uses simulation flags; payment webhook handler exists but full flow is not enforced in UI yet.
- Storage: Local or Azure Blob Storage chosen by connection string.

### 10) Security & Privacy Requirements
- JWT auth for API; Google OAuth supported when configured.
- No logging of PII or secrets; avoid returning stack traces.
- Webhook HMAC validation, 5‑minute window; rejection on invalid signatures.
- File validation: size, extension, and magic bytes; path traversal guarded on delete.
- CORS policies per environment; static file responses include content type, caching, and limited headers.
- Data retention strictly enforced (7/30 days); user-initiated deletion/export flows available.

### 11) Performance & Reliability
- Background jobs: weekly credit reset (service code), retention cleanup, model expiration checks.
- Gallery self-healing and filesystem/DB reconciliation utilities in development endpoints.
- Response compression enabled; static file caching for images and previews.

### 12) UX Overview (Frontend)
- Onboarding: login/register → dashboard with steps: upload → create training zip → train model → select styles → generate → view gallery.
- Enhancement flow: upload/select photo → enhance → preview/download.
- Credits UI: view status, packages list (public), purchase flow (mock PaymentIntent in dev), history.
- Gallery: shows original/generate counts, download links, delete.
- Settings: data export, delete photos/model/all data, delete account, retention notice (7/30 days).

### 13) Acceptance Criteria (MVP)
- Upload rejects >20 images or invalid types/sizes; success returns absolute URLs.
- Training ZIP created when ≥10 images exist; returns public URL.
- Training endpoint blocks when READY model exists; requires 15 purchased credits; consumes after starting.
- Generation requires purchased credits (5 per output); generation fails gracefully when model unavailable.
- Enhancement consumes 1 weekly credit; returns prediction and remaining credits.
- Retention background job sets and deletes data per policy; manual endpoints work.
- Credit status/packaging endpoints return typed data; purchase adds credits to account.
- Webhook ingestion persists generated images and sets retention; downloads images locally.

### 14) Telemetry/Logging (non‑PII)
- Use structured logs for: uploads, credit consumption, training/generation start/fail, webhook processing, retention actions.
- Do not log user emails, tokens, or image contents.

### 15) Risks & Mitigations
- Replicate failures/rate limits → retry/backoff and clear error messages; credit only consumed on success.
- Webhook misses → dashboard repair endpoints + filesystem reconciliation tools.
- Payment incomplete → simulation mode for dev; production requires Stripe webhook completion before credit award.

### 16) Open Questions
- Should downloads require credits for premium tier? (Currently no; generation is gated instead.)
- Maximum total uploads per account/week? (Only per‑request max=20 is enforced now.)
- Admin moderation of styles or generated content?

### 17) Rollout & Dependencies
- Backend: dotnet build/run; EF migrations applied at startup; configure Replicate token; optional Azure Storage.
- Frontend: Angular app with auth interceptors; environment `AppBaseUrl` set for API base.
- Production: enable Stripe keys, webhook secret; disable payment simulation; confirm CORS origins.

### 18) Compliance & Data Retention
- Originals deleted after 7 days; generated images after 30 days; user-initiated deletion and data export supported.
- SQL backup retention per infra templates; Blob Storage soft delete (7 days) if enabled.

### 19) Appendix — Representative API Map
- Auth: `/api/auth/*`
- Profile: `/api/profile/*`
- Image: `/api/image/*`
- Style: `/api/style/*`
- Replicate: `/api/replicate/*`
- Credit: `/api/credit/*`
- Retention: `/api/retentionpolicy/*`
- Webhooks: `/api/webhooks/replicate/*`


