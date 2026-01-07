## Product Requirements Document (PRD) — AI.ProfilePhotoMaker

Version: 1.2  
Last updated: 2025-12-19

### 1) Product Summary
- AI-powered profile/headshot photo maker with: user auth (email/password + Google OAuth), photo upload, custom model training on Replicate, styled image generation, photo enhancement (Replicate + OpenAI styles), credits/credit packages, and automated data retention.
- Tech: ASP.NET Core Web API + Angular frontend, EF Core + SQL Server, optional Azure Blob Storage.

### 2) Goals
- Enable users to generate professional profile photos from their selfies using AI styles.
- Provide a basic tier with a weekly top-up to 5 when below, and credit packages (Stripe PaymentIntents + webhooks in production, with simulation mode for development).
- Ensure reliability via hybrid DB/filesystem syncing and webhook-driven flows.
- Enforce privacy via retention: input photos deleted after 30 days, generated photos after 30 days (target behavior; automated cleanup is being phased in via background jobs).

### 3) Non‑Goals
- No full subscription billing lifecycle in MVP (credit packages are primary; Stripe webhook flow drives credit awards, but not all UI flows are strictly payment-gated yet).
- No admin analytics dashboard in MVP.
- No enterprise SSO or multi-tenant roles.

### 4) Users & Personas
- New user: wants quick professional headshots; may try basic tier first.
- Returning user: trained a model and returns to generate more styled photos.
- Guest visitor: can view credit packages and style previews.

### 5) User Stories (Core)
- As a user, I can register/login (email/password or Google) to manage my photos and credits.
- As a user, I can upload up to 20 selfies at a time for training zip creation.
- As a user, I can select styles, train a custom model, and generate styled photos using credits.
- As a basic user, I can enhance photos using credits with weekly top-ups.
- As a user, I can view, download, and delete my images.
- As a user, I can purchase credit packages and see my credit status/history.
- As a user, I can export my data and delete photos/model/account.

### 6) Functional Requirements

Auth & Profile
- Email/password registration and login (JWT), Google OAuth login and callback with session for state.
- Profile auto-creation on first OAuth login with default credits (5) and last reset timestamp.
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
- Training requires 15 credits. Prevent retrain when a READY model already exists.
- Training completion is detected via background polling; status endpoints reflect progress and final readiness.
- Endpoints: `POST /api/replicate/train`, `GET /api/replicate/train/status/{trainingId}`.

Styled Image Generation (Replicate)
- Requires 5 credits per image output. Generates 1–4 images per request; batch generation supported across multiple styles.
- Endpoints: `POST /api/replicate/generate`, `POST /api/replicate/generate/batch`, `GET /api/replicate/generate/status/{predictionId}`.

Photo Enhancement (Replicate + OpenAI)
- Basic tier feature using credits. Standard enhancements use Replicate Kontext Pro (1 credit).
- Stylized enhancements (OpenAI gpt-image-1) use 2 credits and return direct image output instead of a Replicate prediction wrapper.
- Endpoints: `POST /api/replicate/enhance` (Replicate), `POST /api/enhancement/enhance` (OpenAI).

Credits & Payments
- Unified credit balance with weekly top-ups to 5 when below.
- Credits added via credit packages; status, packages (public), purchase, history provided; PaymentIntents created via Stripe in all environments, with a configuration switch for simulation in development/test.
- Endpoints: `GET /api/credit/status`, `GET /api/credit/packages` (public), `POST /api/credit/purchase`, `GET /api/credit/history`, `POST /api/credit/create-payment-intent` (Stripe PaymentIntent), `GET /api/credit/costs`, `GET /api/credit/payment-config`.

Retention & Privacy
- Retention policy: input photos (original uploads) deleted after 30 days; AI generated photos deleted after 30 days. Background services and tooling implement this over time; manual endpoints exist to inspect/delete expired images in early MVP deployments.
- Endpoints: `GET /api/retentionpolicy/expired-images`, `POST /api/retentionpolicy/delete-expired`, `POST /api/retentionpolicy/initialize-retention-dates`, `GET /api/retentionpolicy/policy-info`.

Webhooks & File Downloading
- `POST /api/webhooks/replicate/prediction-complete` validates signature; downloads generated images to `/generated/{userId}`, creates DB records, sets retention.

### 7) Key Business Rules & Limits
- Upload limits: max 20 images per request; file size ≤ 10MB; allowed types: .jpg/.jpeg/.png/.webp with signature validation.
- Training ZIP: requires ≥ 10 original uploads.
- Credits:
  - Unified balance; weekly top-up restores to 5 when below.
  - Costs: enhancement = 1 (Replicate) or 2 (OpenAI styles), model_training = 15, styled_generation = 5 per image.
  - Consumption occurs before external API calls; failures refund credits.
- Generation: 1–4 outputs per style per request; batch generation allowed; model availability checked.
- Retention: originals 30 days; generated 30 days; scheduled at record creation and enforced by background job.

### 8) Data Model (high‑level)
- ApplicationUser (Identity)
- UserProfile: UserId, Credits, LastCreditReset, SubscriptionTier, ProcessedImages, UsageLogs.
- ProcessedImage: IsOriginalUpload, IsGenerated, OriginalImageUrl, ProcessedImageUrl, Style, CreatedAt, ScheduledDeletionDate.
- Style + UserStyleSelection: Active styles and user selections.
- ModelCreationRequest: UserId, ReplicateModelId, TrainedModelVersion, Status, CompletedAt, TrainingImageZipUrl.
- CreditPackage, CreditPurchase.
- UsageLog: action, creditsCost, creditsRemaining.

### 9) External Services & Config
- Replicate API: training, prediction, enhancement; prediction-complete webhook uses signature validation with 5-minute timestamp window; training completion uses polling.
- OpenAI API: gpt-image-1 photo enhancement for select styles; requires `OPENAI_API_KEY`.
- Stripe: library wired; dev uses simulation flags; payment webhook handler exists but full flow is not enforced in UI yet.
- Storage: Local or Azure Blob Storage chosen by connection string.

### 10) Security & Privacy Requirements
- JWT auth for API; Google OAuth supported when configured.
- No logging of PII or secrets; avoid returning stack traces.
- Webhook HMAC validation, 5‑minute window; rejection on invalid signatures.
- File validation: size, extension, and magic bytes; path traversal guarded on delete.
- CORS policies per environment; static file responses include content type, caching, and limited headers.
- Data retention strictly enforced (30/30 days); user-initiated deletion/export flows available.

### 11) Performance & Reliability
- Background jobs: weekly credit top-up (service code), retention cleanup, model expiration checks.
- Gallery self-healing and filesystem/DB reconciliation utilities in development endpoints.
- Response compression enabled; static file caching for images and previews.

### 12) UX Overview (Frontend)
- Onboarding: login/register → dashboard with steps: upload → create training zip → train model → select styles → generate → view gallery.
- Enhancement flow: upload/select photo → enhance → preview/download.
- Credits UI: view status, packages list (public), purchase flow (mock PaymentIntent in dev), history.
- Gallery: shows original/generate counts, download links, delete.
- Settings: data export, delete photos/model/all data, delete account, retention notice (30/30 days).

### 13) Acceptance Criteria (MVP)
- Upload rejects >20 images or invalid types/sizes; success returns absolute URLs.
- Training ZIP created when ≥10 images exist; returns public URL.
- Training endpoint blocks when READY model exists; requires 15 credits; consumes after starting.
- Generation requires 5 credits per output; generation fails gracefully when model unavailable.
- Enhancement consumes 1 credit (Replicate) or 2 credits (OpenAI styles); returns prediction or direct output plus remaining credits.
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
- Originals deleted after 30 days; generated images after 30 days; user-initiated deletion and data export supported.
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
