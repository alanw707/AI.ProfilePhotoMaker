# AI.ProfilePhotoMaker - Epic Breakdown

**Author:** Alan
**Date:** 2025-12-22T11:00:23-08:00
**Project Level:** MVP
**Target Scale:** single tenant

---

## Overview

This document provides the complete epic and story breakdown for AI.ProfilePhotoMaker, decomposing the requirements from the [PRD](./PRD.md) into implementable stories.

**Living Document Notice:** This is the initial version. It will be updated after UX Design and Architecture workflows add interaction and technical details to stories.

Epic plan: 6 epics delivering incremental user value, starting with foundational setup and core account access, then image workflows (upload, training, generation), enhancements, credits/payments, and retention/privacy/export. This structure aligns with PRD core flows and leverages the layered API architecture with background services and webhooks.

---

## Context Validation

- ✅ PRD loaded: `docs/product/PRD.md` (version 1.2, 2025-12-19)
- ✅ Architecture loaded: `docs/architecture/ARCHITECTURE_OVERVIEW.md`
- ✅ Architecture details loaded: `docs/architecture-api.md`, `docs/architecture-ui.md`, `docs/integration-architecture.md`
- ✅ Cloud architecture loaded: `docs/architecture/cloud-architecture.md` (infra posture and deployment context)
- ⚠️ UX design not found (recommended for UI-heavy product; will proceed without UX-specific patterns)

---

## Functional Requirements Inventory

FR1: Register/login with email/password and JWT issuance.  
FR2: Google OAuth login and callback flow with session state.  
FR3: Auto-create user profile on first OAuth login with default weekly credits.  
FR4: Profile CRUD and stats via `/api/profile` endpoints.  
FR5: User data export, data deletion, and account deletion flows.  
FR6: Upload images with validation (types, size, magic bytes), max 20 per request, storage paths.  
FR7: Create training ZIP (auto or explicit endpoint), min 10 images, list/get/delete ZIPs.  
FR8: Gallery list with absolute URLs; delete images from DB and filesystem.  
FR9: Style catalog endpoints, style template lookup, and user style selection persistence.  
FR10: Start model training with purchased credits; block retrain if READY model exists.  
FR11: Training status polling endpoint and background completion updates.  
FR12: Styled image generation (single and batch) with credits; status endpoints.  
FR13: Photo enhancement via Replicate (1 credit) and OpenAI (2 credits) with direct output.  
FR14: Credit status, packages, purchase, history, costs, and payment-config endpoints; PaymentIntent creation.  
FR15: Retention policy endpoints (expired-images, delete-expired, initialize retention, policy info) and background cleanup.  
FR16: Replicate prediction-complete webhook validation, image download/persistence, retention scheduling.

---

## FR Coverage Map

FR1-FR5: Epic 2 (Auth + Profile + Account/Data Management)  
FR6-FR8: Epic 3 (Upload, Training Data, Gallery)  
FR9-FR12: Epic 4 (Styles, Training, Generation)  
FR13: Epic 5 (Enhancement)  
FR14: Epic 6 (Credits + Payments)  
FR15-FR16: Epic 6 (Retention + Webhooks) with supporting stories in Epic 3/4 for data lifecycle hooks

---

## Epic Structure Plan

Epic 1: Foundation & Core Platform Setup  
User value: Reliable, secure platform foundations that enable all user-facing features.  
PRD coverage: Foundational requirements supporting FR1-FR16.  
Technical context: .NET 8 API skeleton, EF Core setup, storage paths, background services, webhook plumbing, logging.  
Dependencies: None (first epic).

Epic 2: Account Access & Profile Management  
User value: Users can register/login and manage profiles and account data.  
PRD coverage: FR1-FR5.  
Technical context: JWT auth + Google OAuth, profile CRUD, data export/deletion endpoints.  
Dependencies: Epic 1.

Epic 3: Uploads, Training Data, and Gallery  
User value: Users can upload selfies, manage training data, and view/delete images.  
PRD coverage: FR6-FR8.  
Technical context: upload validation, storage paths, training ZIP lifecycle, gallery APIs.  
Dependencies: Epics 1-2.

Epic 4: Styles, Model Training, and Generation  
User value: Users can select styles, train models, and generate styled photos.  
PRD coverage: FR9-FR12, FR16 (webhook processing for generated images).  
Technical context: Replicate integration, training polling, generation queues, webhook validation and persistence.  
Dependencies: Epics 1-3.

Epic 5: Photo Enhancement (Weekly Credits)  
User value: Basic users can enhance photos without model training.  
PRD coverage: FR13.  
Technical context: Replicate/OpenAI enhancement endpoints, credit consumption/refunds.  
Dependencies: Epics 1-3.

Epic 6: Credits, Payments, and Data Retention  
User value: Users can purchase/manage credits and maintain privacy via retention and account controls.  
PRD coverage: FR14-FR16, FR5, FR15.  
Technical context: Stripe PaymentIntents + webhooks, credit ledgering, retention cleanup services.  
Dependencies: Epics 1-3 (and 4 for webhook integration).

---

## Epic Technical Context

- API stack: ASP.NET Core Web API (.NET 8) with layered controllers/services/data and EF Core (SQL Server prod, SQLite dev).  
- Auth: JWT + Google OAuth with callback endpoints; Cloudflare Turnstile in auth flow.  
- Storage: Filesystem paths for uploads/training-zips/generated/enhanced; optional Azure Blob Storage integration.  
- Background services: training polling, retention cleanup, weekly credit reset, model expiration.  
- External integrations: Replicate (train/generate/enhance) + OpenAI enhancement, Stripe payments and webhooks.  
- Webhooks: Replicate prediction-complete endpoint with HMAC validation; Stripe payment events.  
- UI: Angular 19 routes for auth, dashboard, upload/generate, enhancements, gallery, credits, settings.  
- Non-functional expectations: validation, error handling, logging, and retention enforcement.

---

## Epic 1: Foundation & Core Platform Setup

**Epic Goal:** Establish the foundational platform services (API, data, storage, background jobs, webhook plumbing) needed to support all user-facing features reliably.

### Story 1.1: Core API configuration, error handling, and health checks

As a platform maintainer,  
I want consistent API configuration, logging, and health checks,  
So that the system is observable and safe to operate.

**Acceptance Criteria:**

**Given** the API is running with environment configuration loaded  
**When** I call `GET /health`  
**Then** the API returns `200 OK` with a basic health response.

**And** **Given** the database is reachable  
**When** I call `GET /health/db`  
**Then** the API returns `200 OK`, and returns a non-200 status if the DB is unreachable.

**And** **Given** an unhandled exception occurs in a request  
**When** the API returns the error  
**Then** the response is standardized and does not expose stack traces in production.

**Prerequisites:** None.

**Technical Notes:** Use ASP.NET Core health checks (`/health`, `/health/db`), centralized exception middleware, structured logging (Serilog + Application Insights when configured), response compression, and environment-specific configuration via appsettings and env vars.

### Story 1.2: Database schema and migrations for core entities

As a platform maintainer,  
I want core database schemas and migrations in place,  
So that all features can store and query data reliably.

**Acceptance Criteria:**

**Given** the database is configured for the environment (SQLite dev, SQL Server prod)  
**When** the application starts  
**Then** EF Core migrations are applied and core tables exist.

**And** **Given** the core entities are defined  
**When** I inspect the schema  
**Then** tables exist for users/profiles, processed images, styles, style selections, model creation requests, credit packages/purchases, and usage logs.

**Prerequisites:** Story 1.1.

**Technical Notes:** Use EF Core migrations; ensure `ApplicationUser`, `UserProfile`, `ProcessedImage`, `Style`, `UserStyleSelection`, `ModelCreationRequest`, `CreditPackage`, `CreditPurchase`, and `UsageLog` are mapped.

### Story 1.3: Storage paths and storage abstraction

As a platform maintainer,  
I want consistent storage paths and a storage abstraction,  
So that uploads, generated assets, and enhanced images are stored safely.

**Acceptance Criteria:**

**Given** a user uploads or generates images  
**When** the system stores files  
**Then** files are written to the correct paths (`/uploads/{userId}`, `/training-zips/{userId}.zip`, `/generated/{userId}`, `/enhanced/{userId}`).

**And** **Given** Azure Blob Storage is configured  
**When** file storage is used  
**Then** the storage abstraction stores and retrieves files using the configured provider.

**Prerequisites:** Story 1.2.

**Technical Notes:** Centralize path creation, validate file names, prevent path traversal, and support local filesystem with optional Azure Blob Storage services.

### Story 1.4: Background services wiring and scheduling

As a platform maintainer,  
I want background services registered and running,  
So that scheduled operations execute reliably.

**Acceptance Criteria:**

**Given** the API starts  
**When** hosted services are registered  
**Then** background workers are active for weekly credit reset, training polling, retention cleanup, and model expiration checks.

**And** **Given** a background job fails  
**When** the service logs the error  
**Then** the error is recorded with context for troubleshooting.

**Prerequisites:** Story 1.1.

**Technical Notes:** Register hosted services, ensure safe retry/backoff for external API polling, and log failures without crashing the process.

---

## Epic 2: Account Access & Profile Management

**Epic Goal:** Users can register/login and manage profiles and account data.

### Story 2.1: Email/password registration and login

As a new user,  
I want to register and log in with email/password,  
So that I can access the product.

**Acceptance Criteria:**

**Given** I submit valid registration data  
**When** `POST /api/auth/register` is called  
**Then** my account is created and I receive a JWT for authenticated access.

**And** **Given** I submit valid login credentials  
**When** `POST /api/auth/login` is called  
**Then** I receive a JWT and can access authenticated endpoints.

**Prerequisites:** Story 1.1, Story 1.2.

**Technical Notes:** JWT issuance, password hashing, validation, and non-PII logging.

### Story 2.2: Google OAuth login and profile auto-creation

As a user,  
I want to sign in with Google,  
So that I can access the app without creating a local password.

**Acceptance Criteria:**

**Given** I initiate OAuth  
**When** I call `GET /api/auth/google-oauth-url` and complete the provider flow  
**Then** `GET /api/auth/external-login-callback` completes authentication and returns a JWT.

**And** **Given** this is my first OAuth login  
**When** the callback completes  
**Then** a `UserProfile` is created with default weekly credits.

**Prerequisites:** Story 1.1, Story 1.2.

**Technical Notes:** Maintain OAuth state, validate provider tokens, and create profile atomically.

### Story 2.3: Profile CRUD and stats

As a logged-in user,  
I want to view and update my profile and stats,  
So that I can manage my account.

**Acceptance Criteria:**

**Given** I am authenticated  
**When** I call `GET/POST/PUT/DELETE /api/profile`  
**Then** I can view, create, update, or delete my profile.

**And** **Given** I request profile stats  
**When** I call `GET /api/profile`  
**Then** I see credits, usage, and counts relevant to my account.

**Prerequisites:** Story 2.1 or Story 2.2.

**Technical Notes:** Enforce ownership checks and consistent DTOs.

### Story 2.4: Data export and account deletion

As a user,  
I want to export and delete my data,  
So that I can control my privacy.

**Acceptance Criteria:**

**Given** I am authenticated  
**When** I request a data export  
**Then** the API returns an export payload or downloadable archive.

**And** **Given** I request account/data deletion  
**When** I call the delete endpoints  
**Then** my account data, photos, and model data are deleted per policy.

**Prerequisites:** Story 2.3.

**Technical Notes:** Ensure deletion includes filesystem cleanup and DB records, with safe confirmation flows.

---

## Epic 3: Uploads, Training Data, and Gallery

**Epic Goal:** Users can upload selfies, manage training data, and view/delete images.

### Story 3.1: Image upload with validation and storage

As a user,  
I want to upload selfies with validation,  
So that my training data is accepted and stored correctly.

**Acceptance Criteria:**

**Given** I upload images  
**When** `POST /api/image/upload` is called  
**Then** the API validates file types (jpg/jpeg/png/webp), magic bytes, size <= 10MB, and max 20 files.

**And** **Given** validation passes  
**When** files are saved  
**Then** images are stored under `/uploads/{userId}` (or `/enhanced/{userId}` for enhancement uploads) with DB records.

**Prerequisites:** Story 1.3, Story 2.1 or Story 2.2.

**Technical Notes:** Multipart upload handling, validation errors, and normalized absolute URLs on response.

### Story 3.2: Training ZIP creation and management

As a user,  
I want a training ZIP generated from my uploads,  
So that I can train a custom model.

**Acceptance Criteria:**

**Given** I have at least 10 valid images  
**When** I mark uploads `ForTraining=true` or call `POST /api/image/create-training-zip`  
**Then** a ZIP is created at `/training-zips/{userId}.zip`.

**And** **Given** I call ZIP endpoints  
**When** I list/get/delete ZIPs  
**Then** the API returns or removes the ZIP appropriately.

**Prerequisites:** Story 3.1.

**Technical Notes:** Validate minimum image count, return public URL, and support delete.

### Story 3.3: Gallery list with absolute URLs

As a user,  
I want to view my uploaded and generated images,  
So that I can manage my gallery.

**Acceptance Criteria:**

**Given** I call `GET /api/image/images`  
**When** images are returned  
**Then** URLs are normalized to absolute paths and include original/generated/enhanced records.

**Prerequisites:** Story 3.1.

**Technical Notes:** Include metadata and ordering; ensure user scoping.

### Story 3.4: Image deletion and cleanup

As a user,  
I want to delete images,  
So that unwanted photos are removed from my account.

**Acceptance Criteria:**

**Given** I call `DELETE /api/image/images/{imageId}`  
**When** deletion succeeds  
**Then** the DB record and filesystem file are removed.

**Prerequisites:** Story 3.3.

**Technical Notes:** Path traversal protection and ownership checks.

---

## Epic 4: Styles, Model Training, and Generation

**Epic Goal:** Users can select styles, train models, and generate styled photos.

### Story 4.1: Style catalog and template lookup

As a user,  
I want to browse available styles and templates,  
So that I can choose how my photos are generated.

**Acceptance Criteria:**

**Given** I call `GET /api/style` or `GET /api/style/{id}`  
**When** styles are returned  
**Then** each style includes name, description, prompt, and negative prompt template.

**And** **Given** I request a template  
**When** I call `GET /api/style/name/{name}/template`  
**Then** I receive the template for that style.

**Prerequisites:** Story 1.2.

**Technical Notes:** Style data stored in DB; enforce active-only selection.

### Story 4.2: User style selection persistence

As a user,  
I want to select and save styles,  
So that my preferences drive model generation.

**Acceptance Criteria:**

**Given** I submit style selections  
**When** `POST /api/style/select` is called  
**Then** selections are persisted for my profile.

**And** **Given** I fetch selections  
**When** `GET /api/style/user-selected` is called  
**Then** my selections are returned.

**Prerequisites:** Story 4.1.

**Technical Notes:** Store `UserStyleSelection` and validate ownership.

### Story 4.3: Start model training with credit gating

As a user,  
I want to start model training with purchased credits,  
So that I can generate styled photos.

**Acceptance Criteria:**

**Given** I have at least 10 training images and enough purchased credits  
**When** `POST /api/replicate/train` is called  
**Then** 15 credits are consumed and training starts.

**And** **Given** a READY model exists  
**When** I try to train again  
**Then** the request is rejected.

**Prerequisites:** Story 3.2, Story 4.2.

**Technical Notes:** Create `ModelCreationRequest`, call Replicate, and refund credits on failure.

### Story 4.4: Training status and background polling

As a user,  
I want to check training progress,  
So that I know when my model is ready.

**Acceptance Criteria:**

**Given** I call `GET /api/replicate/train/status/{trainingId}`  
**When** training is in progress  
**Then** the API returns the current status.

**And** **Given** training completes  
**When** background polling detects completion  
**Then** the model status is updated and marked READY.

**Prerequisites:** Story 4.3.

**Technical Notes:** Use `TrainingPollingBackgroundService` and persist status transitions.

### Story 4.5: Styled image generation (single request)

As a user,  
I want to generate styled photos,  
So that I can receive AI-generated outputs.

**Acceptance Criteria:**

**Given** I have purchased credits and a READY model  
**When** I call `POST /api/replicate/generate`  
**Then** 5 credits per output are consumed and a prediction is started.

**Prerequisites:** Story 4.4.

**Technical Notes:** Validate credit balance, create prediction record, refund on failure.

### Story 4.6: Batch generation across styles

As a user,  
I want to generate across multiple styles in one request,  
So that I can get diverse results efficiently.

**Acceptance Criteria:**

**Given** I submit a batch request  
**When** `POST /api/replicate/generate/batch` is called  
**Then** predictions are queued per style and credits are charged accordingly.

**Prerequisites:** Story 4.5.

**Technical Notes:** Store `PendingGenerationRequest` and track status per style.

### Story 4.7: Generation status endpoint

As a user,  
I want to check generation status,  
So that I know when outputs are ready.

**Acceptance Criteria:**

**Given** I call `GET /api/replicate/generate/status/{predictionId}`  
**When** generation is in progress  
**Then** the API returns the latest prediction status and any output URLs when ready.

**Prerequisites:** Story 4.5.

**Technical Notes:** Return normalized URLs and handle failures gracefully.

### Story 4.8: Replicate prediction-complete webhook

As a system,  
I want to handle Replicate completion webhooks,  
So that generated images are saved automatically.

**Acceptance Criteria:**

**Given** Replicate posts to `POST /api/webhooks/replicate/prediction-complete`  
**When** the signature is valid and within time window  
**Then** the API downloads images to `/generated/{userId}`, creates `ProcessedImage` records, and schedules retention.

**Prerequisites:** Story 4.5.

**Technical Notes:** Validate HMAC signature, enforce 5-minute window, and persist outputs.

---

## Epic 5: Photo Enhancement (Weekly Credits)

**Epic Goal:** Basic users can enhance photos without model training.

### Story 5.1: Replicate enhancement endpoint

As a basic user,  
I want to enhance photos using weekly credits,  
So that I can improve images without training a model.

**Acceptance Criteria:**

**Given** I have weekly credits available  
**When** I call `POST /api/replicate/enhance`  
**Then** 1 weekly credit is consumed and a Replicate enhancement is started.

**Prerequisites:** Story 3.1.

**Technical Notes:** Consume and refund weekly credits appropriately on failure.

### Story 5.2: OpenAI enhancement endpoint

As a basic user,  
I want stylized enhancements with OpenAI,  
So that I can get premium-looking results.

**Acceptance Criteria:**

**Given** I have weekly credits available  
**When** I call `POST /api/enhancement/enhance`  
**Then** 2 weekly credits are consumed and the API returns the enhanced image output directly.

**Prerequisites:** Story 3.1.

**Technical Notes:** Use OpenAI `gpt-image-1` and ensure credits are refunded on failure.

---

## Epic 6: Credits, Payments, and Data Retention

**Epic Goal:** Users can purchase/manage credits and maintain privacy via retention and account controls.

### Story 6.1: Credit status and weekly reset

As a user,  
I want to see my credit status,  
So that I know what I can do in the product.

**Acceptance Criteria:**

**Given** I call `GET /api/credit/status`  
**When** the response returns  
**Then** I see weekly and purchased credit balances and next reset timing.

**And** **Given** weekly credits expire  
**When** the weekly reset runs  
**Then** credits reset to the configured basic tier value.

**Prerequisites:** Story 1.4, Story 2.3.

**Technical Notes:** Use `BasicTierBackgroundService` and usage logs.

### Story 6.2: Credit packages, costs, and purchase initiation

As a user,  
I want to view packages and start a purchase,  
So that I can buy credits.

**Acceptance Criteria:**

**Given** I call `GET /api/credit/packages`  
**When** packages are returned  
**Then** I see available packages, pricing, and bonus credits.

**And** **Given** I start a purchase  
**When** I call `POST /api/credit/create-payment-intent`  
**Then** I receive a Stripe PaymentIntent client secret (or simulation response in dev).

**Prerequisites:** Story 2.3.

**Technical Notes:** Use Stripe in all environments with a simulation toggle for dev/test.

### Story 6.3: Stripe webhook credit fulfillment

As a system,  
I want to process payment webhooks,  
So that credits are awarded only after successful payment.

**Acceptance Criteria:**

**Given** Stripe sends a webhook event  
**When** `POST /api/webhooks/stripe` is called  
**Then** the signature is validated and credits are awarded on successful payment events.

**Prerequisites:** Story 6.2.

**Technical Notes:** Signature validation, idempotency, and logging of payment transactions.

### Story 6.4: Credit history and usage logs

As a user,  
I want to see my credit history,  
So that I can understand past usage.

**Acceptance Criteria:**

**Given** I call `GET /api/credit/history`  
**When** the response returns  
**Then** I see a chronological list of credit usage and purchases.

**Prerequisites:** Story 6.1.

**Technical Notes:** Populate `UsageLog` and `CreditPurchase` with correlation data.

### Story 6.5: Retention policy endpoints and cleanup

As a user,  
I want my photos retained only per policy,  
So that my data is not stored longer than needed.

**Acceptance Criteria:**

**Given** retention is enabled  
**When** the background cleanup runs  
**Then** original images older than 30 days and generated images older than 30 days are deleted.

**And** **Given** I call retention endpoints  
**When** I use `GET /api/retentionpolicy/expired-images` or `POST /api/retentionpolicy/delete-expired`  
**Then** I can inspect and trigger cleanup.

**Prerequisites:** Story 1.4, Story 3.1.

**Technical Notes:** Schedule deletion dates at record creation and enforce via background services.

---

## FR Coverage Matrix

| FR | Description | Coverage |
| --- | --- | --- |
| FR1 | Email/password auth with JWT | Story 2.1 |
| FR2 | Google OAuth login and callback | Story 2.2 |
| FR3 | Auto-create profile on OAuth | Story 2.2 |
| FR4 | Profile CRUD and stats | Story 2.3 |
| FR5 | Data export and account/data deletion | Story 2.4 |
| FR6 | Upload validation and storage | Story 3.1 |
| FR7 | Training ZIP creation and management | Story 3.2 |
| FR8 | Gallery list and image deletion | Story 3.3, Story 3.4 |
| FR9 | Style catalog and selection | Story 4.1, Story 4.2 |
| FR10 | Model training with credits and ready check | Story 4.3 |
| FR11 | Training status and polling | Story 4.4 |
| FR12 | Styled generation and status | Story 4.5, Story 4.6, Story 4.7 |
| FR13 | Photo enhancement (Replicate/OpenAI) | Story 5.1, Story 5.2 |
| FR14 | Credits, packages, purchases, payment intents, history | Story 6.1, Story 6.2, Story 6.3, Story 6.4 |
| FR15 | Retention policy endpoints and cleanup | Story 6.5 |
| FR16 | Replicate webhook handling and persistence | Story 4.8 |

---

## Epic Completion Reviews

- Epic 1 Complete: Foundation & Core Platform Setup  
  FR Coverage: Foundational support for FR1-FR16  
  Technical Context: API configuration, EF Core, storage paths, background services

- Epic 2 Complete: Account Access & Profile Management  
  FR Coverage: FR1-FR5  
  Technical Context: JWT auth, OAuth, profile CRUD, data export/deletion

- Epic 3 Complete: Uploads, Training Data, and Gallery  
  FR Coverage: FR6-FR8  
  Technical Context: upload validation, training ZIP lifecycle, gallery APIs

- Epic 4 Complete: Styles, Model Training, and Generation  
  FR Coverage: FR9-FR12, FR16  
  Technical Context: Replicate integration, polling, prediction status, webhook persistence

- Epic 5 Complete: Photo Enhancement (Weekly Credits)  
  FR Coverage: FR13  
  Technical Context: Replicate/OpenAI enhancement endpoints and credit consumption

- Epic 6 Complete: Credits, Payments, and Data Retention  
  FR Coverage: FR14-FR15  
  Technical Context: Stripe PaymentIntents/webhooks, credit history, retention cleanup

---

## Final Validation

- User Value: Each epic delivers a usable capability (account access, uploads, training/generation, enhancements, credits/retention).  
- Completeness: All PRD functional requirements FR1-FR16 are mapped to specific stories.  
- Technical Soundness: Stories align with architecture decisions (API endpoints, EF Core, background jobs, Replicate/Stripe integrations).  
- UX Integration: UX artifacts are missing; UX-specific implementation details are not included.  
- Implementation Ready: Stories are scoped for single-dev execution with clear acceptance criteria and technical notes.

---

## Summary

Six epics cover all PRD functional requirements with 24 implementation-ready stories. The sequence starts with platform foundations, moves through account access and image pipelines, then addresses style-based training/generation, enhancements, and finally credits/payments and retention. UX-specific story details are intentionally omitted pending UX design artifacts.

---

_For implementation: Use the `create-story` workflow to generate individual story implementation plans from this epic breakdown._

_This document will be updated after UX Design and Architecture workflows to incorporate interaction details and technical decisions._
