# AI.ProfilePhotoMaker - Epic Breakdown

**Author:** Alanw
**Date:** 2025-12-04T19:51:51-08:00
**Project Level:** MVP
**Target Scale:** 1–10K users

---

## Overview

This document provides the epic and story breakdown for AI.ProfilePhotoMaker, decomposing the requirements from the PRD into implementable stories. Initial draft created with PRD and Architecture context loaded; no UX design doc available.

**Epics Summary:** 5 epics aligned to user value and architecture constraints:
- E1 Foundation & Access: accounts, profiles, baseline credits, storage/auth scaffolding.
- E2 Upload & Training Prep: validated uploads, training ZIP creation, gallery basics.
- E3 Styles & Model Training: style catalog/selection, training flows, training webhooks.
- E4 Styled Generation & Gallery: batch generation, gallery viewing/downloading/deleting.
- E5 Enhancement, Credits & Retention: photo enhancement, credit purchase/consumption, retention & privacy enforcement.

---

## Context Validation

- PRD: `docs/product/PRD.md` (loaded)
- Architecture: `docs/architecture/ARCHITECTURE_OVERVIEW.md` (loaded)
- Cloud Architecture: `docs/architecture/cloud-architecture.md` (loaded)
- UX Design: _Not provided_
- Prerequisite check: PRD + Architecture present; proceeding without UX design context.

---

## Functional Requirements Inventory

1. **FR1 – Authentication:** Email/password registration and login with JWT; Google OAuth login/callback with session handling.
2. **FR2 – Profile & Account Lifecycle:** Auto-create profile on first OAuth login; profile CRUD; account/data export and deletion flows.
3. **FR3 – Image Upload & Validation:** Upload up to 20 images/request (.jpg/.jpeg/.png/.webp), ≤10MB, magic-byte validation; save to user-scoped folders (uploads/enhanced/training).
4. **FR4 – Training ZIP Management:** Auto/create training ZIP when `ForTraining=true` or via explicit endpoint; enforce ≥10 images; list/get/delete ZIPs per user.
5. **FR5 – Gallery Management:** List images with normalized absolute URLs; delete image (file + DB); include repair/debug endpoints.
6. **FR6 – Style Catalog & Selection:** Retrieve style catalog/details/templates; allow multiple style selections and persist per user.
7. **FR7 – Custom Model Training:** Start training with purchased credits; block retrain when READY model exists; handle training-complete webhook; support auto-generation kickoff; status endpoints.
8. **FR8 – Styled Image Generation:** Generate 1–4 outputs per request; batch across styles; consume purchased credits; status endpoints.
9. **FR9 – Photo Enhancement:** Enhance a single uploaded image using weekly credits.
10. **FR10 – Credits & Payments:** Weekly free credits reset; view status/packages; purchase packages; history; create PaymentIntents (Stripe) with simulation flag in dev/test.
11. **FR11 – Retention & Privacy:** Schedule deletions (originals 7 days, generated 30 days); background/repair endpoints; user export/delete support.
12. **FR12 – Webhooks & File Downloading:** Validate replicate webhooks, download generated images, persist DB records, set retention metadata.

---

## FR Coverage Map

FR→Epic/Story mapping:
- FR1 Authentication → E1: Stories 1.1, 1.4
- FR2 Profile & Account Lifecycle → E1: Story 1.3; E5: Story 5.4
- FR3 Image Upload & Validation → E2: Story 2.1
- FR4 Training ZIP Management → E2: Story 2.3
- FR5 Gallery Management → E2: Story 2.2; E4: Story 4.3 (generated ingestion)
- FR6 Style Catalog & Selection → E3: Story 3.1
- FR7 Custom Model Training → E3: Stories 3.2, 3.3
- FR8 Styled Image Generation → E4: Stories 4.1, 4.2, 4.3
- FR9 Photo Enhancement → E5: Story 5.1
- FR10 Credits & Payments → E1: Story 1.3 (status surfacing); E5: Story 5.2
- FR11 Retention & Privacy → E5: Stories 5.3, 5.4
- FR12 Webhooks & File Downloading → E3: Story 3.3; E4: Story 4.3; E5: Story 5.2

---

## Epic Structure Plan

**E1 Foundation & Access**
- User value: Users can sign up/login and manage profiles with baseline credits available.
- FR coverage: FR1, FR2, partial FR10 (credit status visibility), foundation for FR11.
- Technical context: JWT + Identity, Google OAuth callback flow, profile CRUD, initial credit reset job hooks.
- Dependencies: none.

**E2 Upload & Training Prep**
- User value: Users can safely upload/validate photos, manage gallery, and produce a training ZIP.
- FR coverage: FR3, FR4, FR5; sets up assets for FR7/FR8.
- Technical context: multipart upload limits (20 files, ≤10MB, magic-byte validation), user-scoped storage paths, ZIP creation pipeline, list/delete endpoints.
- Dependencies: E1 (auth/profile).

**E3 Styles & Model Training**
- User value: Users choose styles and kick off model training with guardrails.
- FR coverage: FR6, FR7, FR12 (training webhook path).
- Technical context: style catalog endpoints, selection persistence, Replicate training submission, block retrain when READY, training-complete webhook ingestion.
- Dependencies: E2 (training data available), credits from E5 for paid training.

**E4 Styled Generation & Gallery**
- User value: Users generate styled images in batches and manage the gallery outputs.
- FR coverage: FR8, FR5 (generated items), FR12 (prediction-complete webhook path).
- Technical context: Replicate generation endpoints (single/batch), credit consumption, model availability checks, gallery listing with normalized URLs, delete/download flows.
- Dependencies: E3 (trained model), E5 (credits), E2 (styles selected).

**E5 Enhancement, Credits & Retention**
- User value: Users enhance photos, purchase credits, and have data protected via retention/privacy controls.
- FR coverage: FR9, FR10, FR11, remaining FR12 (credit/payment webhooks, retention ops).
- Technical context: Enhancement endpoint, weekly credit reset, PaymentIntent creation, webhook-driven credit award, retention scheduling (7/30 days) + cleanup endpoints, export/delete flows.
- Dependencies: E1 (auth/profile), supports E2–E4 via credits/retention.

---

## Epic Technical Context

- Stack: Angular 19 frontend, .NET 8 API, EF Core (SQLite dev, SQL Server prod), file storage (local/Azure), JWT auth with Google OAuth.
- Integrations: Replicate (training/generation/enhancement), Stripe PaymentIntents + webhooks, Azure/FS storage, background services for credits/reset/retention.
- API patterns: multipart uploads with validation, user-scoped storage paths, webhook signature validation (5-minute window), rate limiting on auth, REST endpoints per PRD.
- Data: users/profiles, processed images (original/enhanced/generated) with retention metadata, model requests, style catalog/user selections, credit packages/purchases, audit/usage logs.
- Security & privacy: JWT + OAuth, HMAC-validated webhooks, file signature checks, CORS per env, no PII logging, retention 7/30 days.

---

## Epic 1: Foundation & Access

Goal: Users can sign up/login, manage profiles, and see credit status with secure auth foundations in place.

### Story 1.1: Email/Password Authentication with JWT
As a user, I want to register and log in with email/password, so that I can access my account securely.

**Acceptance Criteria:**
- Given valid email/password on register, when I submit to `POST /api/auth/register`, then a user record is created with hashed password (bcrypt) and JWT issued; invalid input returns 400 with validation details.
- Given valid credentials on login, when I call `POST /api/auth/login`, then I receive JWT + profile basics; invalid credentials return 401 without leaking existence.
- Rate limiting: login/register limited per IP/email (per architecture guidance) with proper 429 response.
- Tokens include expiry; CORS and HTTPS enforced per environment settings.
- UX addendum: Forms have labels/ARIA, keyboard nav/focus states; show password rules + inline errors; loading/empty/error states per addendum.

**Technical Notes:** Use ASP.NET Identity, bcrypt hashing, JWT issuance, structured logging without PII.

**Prerequisites:** None.

### Story 1.2: Google OAuth Login & Profile Auto-Creation
As a user, I want to sign in with Google, so that I can onboard quickly without a password.

**Acceptance Criteria:**
- `GET /api/auth/google-oauth-url` returns provider URL with state/nonce; CORS allowed origins only.
- `GET /api/auth/external-login/{provider}` and callback validate tokens (5-minute window), then issue JWT.
- On first login, profile is auto-created with default weekly credits; subsequent logins reuse profile.
- Errors (invalid state, expired token) return 400/401 with safe messaging.
- UX addendum: Clear button labels, safe error messaging, loading while redirecting, keyboard/focus support.

**Technical Notes:** Honor session/state flow per architecture; log attempts without PII.

**Prerequisites:** Story 1.1.

### Story 1.3: Profile Management & Credit Status
As a user, I want to view and manage my profile and see my current credits, so that I know my account state.

**Acceptance Criteria:**
- `GET/POST/PUT/DELETE /api/profile` manages profile data with auth required; validates fields and ownership.
- `GET /api/credit/status` returns weekly and purchased credits, last reset, and eligibility for weekly reset.
- Data export and account delete endpoints are discoverable (link to retention stories), but primary CRUD works.
- UX addendum: Forms accessible; show credit balances clearly with empty/loading states.

**Technical Notes:** Ensure authorization via user context; map profile to Identity user; surface credit counts for UI.

**Prerequisites:** Stories 1.1–1.2.

### Story 1.4: Auth Hardening & Session Controls
As a user, I want secure sessions, so that my account is protected from misuse.

**Acceptance Criteria:**
- JWT validation middleware enforces expiry, signature, and audience; rejects tampered tokens.
- CORS policies per environment; HTTPS required in production.
- Brute-force/rate limits applied to auth endpoints; lockout on repeated failures per architecture guidance.
- Audit logs for auth events recorded without PII (user IDs only).
- UX addendum: User-facing errors are generic and non-PII; focus states maintained; disable buttons during submit.

**Technical Notes:** Align with architecture security list; ensure config-driven CORS/origins.

**Prerequisites:** Stories 1.1–1.3.

---

## Epic 2: Upload & Training Prep

Goal: Users can safely upload/validate photos, manage gallery basics, and produce a training ZIP for model training.

### Story 2.1: Validated Image Upload
As a user, I want to upload photos with validation, so that only acceptable images are stored.

**Acceptance Criteria:**
- `POST /api/image/upload` accepts up to 20 files/request, each ≤10MB, types jpg/jpeg/png/webp with magic-byte validation; invalid files rejected with per-file reasons.
- Files are stored under user-scoped paths (`/uploads/{userId}` or `/enhanced/{userId}` based on flag).
- Returns normalized absolute URLs for accepted files; rejects over-limit requests with 400.
- UX addendum: Drag/drop + picker; show per-file errors inline; progress/complete indicators; responsive layout and accessible controls.

**Technical Notes:** Enforce size/type/limit; structured errors; logging without paths that reveal PII.

**Prerequisites:** Epic 1 auth.

### Story 2.2: Gallery Listing & Deletion
As a user, I want to view and delete my images, so that I control my uploads.

**Acceptance Criteria:**
- `GET /api/image/images` returns list with absolute URLs, type (original/enhanced/generated), createdAt, retention dates.
- `DELETE /api/image/images/{imageId}` removes file + DB record; path traversal blocked; returns 404 for missing/not-owned items.
- Optional repair/debug endpoints protected to non-production or admin scopes.
- UX addendum: Thumbnails lazy-load; delete confirms; empty state guidance; keyboard/focusable actions; no horizontal scroll on mobile.

**Technical Notes:** Use user context to filter; ensure file and DB consistency.

**Prerequisites:** Story 2.1.

### Story 2.3: Training ZIP Creation & Management
As a user, I want a training ZIP built from my uploads, so that I can train a model.

**Acceptance Criteria:**
- Auto-create ZIP when `ForTraining=true` on upload or via `POST /api/image/create-training-zip`; enforces ≥10 images.
- ZIP path `/training-zips/{userId}.zip`; `GET/DELETE` endpoints list and remove training ZIP.
- Handles insufficient images with clear 400 message; respects storage paths per architecture.
- UX addendum: Show progress/status for ZIP creation; surface validation errors clearly; responsive messaging.

**Technical Notes:** Ensure ZIP rebuild is idempotent; handle concurrent requests safely.

**Prerequisites:** Stories 2.1–2.2.

---

## Epic 3: Styles & Model Training

Goal: Users select styles and train a model with guardrails and webhook-driven status updates.

### Story 3.1: Style Catalog & Selection Persistence
As a user, I want to browse and select styles, so that training/generation uses my choices.

**Acceptance Criteria:**
- `GET /api/style` and `GET /api/style/{id}` return catalog and details; `GET /api/style/name/{name}/template` returns prompt template.
- `POST /api/style/select` saves selections per user; `GET /api/style/user-selected` returns current selections.
- Validates style IDs and ownership; returns consistent data shape for UI.
- UX addendum: Cards/list responsive; selection controls accessible; loading/empty/error states for catalog and selections.

**Technical Notes:** Persist selections; ready for generation fan-out.

**Prerequisites:** Epic 2.

### Story 3.2: Start Model Training with Credit Guardrails
As a user, I want to start model training using purchased credits, so that I can generate styled photos later.

**Acceptance Criteria:**
- `POST /api/replicate/train` checks purchased credits (15); blocks if insufficient; consumes after starting job.
- Blocks retrain when a READY model exists; returns status endpoint link `GET /api/replicate/train/status/{trainingId}`.
- Associates training with latest training ZIP; validates min image count.
- UX addendum: Show remaining credits and clear errors for insufficient credits/model state; loading/progress messaging.

**Technical Notes:** Follow PRD credit rules; return trainingId; log events.

**Prerequisites:** Story 3.1, credits available (Epic 5).

### Story 3.3: Training Complete Webhook Handling
As a system, I want to process training-complete webhooks, so that model status is updated and optional auto-generation can start.

**Acceptance Criteria:**
- `POST /api/webhooks/replicate/training-complete` validates signature + 5-minute window; rejects invalid payloads.
- Updates model record to READY with model version; optionally kicks off generation for selected styles per config.
- Idempotent processing; logs audit entry; no PII in logs.
- UX addendum: Surface training status transitions in UI; user-facing messages avoid technical leakage.

**Technical Notes:** HMAC validation; ensure retries safe; align with architecture webhook flow.

**Prerequisites:** Story 3.2.

---

## Epic 4: Styled Generation & Gallery

Goal: Users generate styled images in batches and manage generated outputs with webhook-backed ingestion.

### Story 4.1: Styled Image Generation Requests (Single & Batch)
As a user, I want to generate styled images in batches, so that I can get multiple outputs efficiently.

**Acceptance Criteria:**
- `POST /api/replicate/generate` and `/api/replicate/generate/batch` accept 1–4 outputs per style per request; validate model availability and purchased credits (5 per output).
- Rejects requests when model unavailable or insufficient credits with actionable errors.
- Returns predictionId(s) and status endpoints.
- UX addendum: Show credit balance impacts before submit; progress/status feedback; responsive layout for batch selection.

**Technical Notes:** Ensure credit consumption after successful call; enforce max outputs per PRD.

**Prerequisites:** Epic 3 completed; credits from Epic 5.

### Story 4.2: Generation Status & Error Handling
As a user, I want to track generation status, so that I know when images are ready or if something failed.

**Acceptance Criteria:**
- `GET /api/replicate/generate/status/{predictionId}` returns current status, outputs if ready, errors if failed.
- Handles timeouts/retries gracefully; surfaces user-friendly errors without leaking internals.
- UX addendum: Polling/refresh indicators; empty state when pending; accessible status messaging.

**Technical Notes:** Polling endpoint uses stored prediction state; align with Replicate API responses.

**Prerequisites:** Story 4.1.

### Story 4.3: Prediction Complete Webhook & Gallery Ingestion
As a system, I want to ingest generated images from webhooks, so that users see outputs in their gallery.

**Acceptance Criteria:**
- `POST /api/webhooks/replicate/prediction-complete` validates signature/time window; downloads generated images to `/generated/{userId}`; creates DB records with retention dates (30 days).
- Normalizes URLs for gallery; marks failures with retry-safe logs; idempotent on duplicate webhooks.
- UX addendum: Gallery reflects new items with loading placeholders; delete/download accessible; retention window displayed where shown.

**Technical Notes:** Follow retention metadata rules; no public blob access without auth; avoid PII in logs.

**Prerequisites:** Stories 4.1–4.2.

---

## Epic 5: Enhancement, Credits & Retention

Goal: Users can enhance photos, manage/purchase credits, and have data retained/deleted per policy.

### Story 5.1: Photo Enhancement with Weekly Credits
As a basic-tier user, I want to enhance a photo, so that I can improve it using my weekly credits.

**Acceptance Criteria:**
- `POST /api/replicate/enhance` consumes 1 weekly credit; blocks when none remain; returns prediction and remaining weekly credits.
- Validates input image ownership; uses enhancement model per architecture.
- Returns normalized URLs for enhanced image.
- UX addendum: Show credit balance before action; loading/progress; inline errors for invalid selection; responsive layout.

**Technical Notes:** Weekly credits separate from purchased credits; ensure proper decrement and reset markers.

**Prerequisites:** Epic 2 upload; Epic 1 auth.

### Story 5.2: Credit Purchase, Status, and Simulation Mode
As a user, I want to purchase credits and see my balances, so that I can fund training/generation.

**Acceptance Criteria:**
- `GET /api/credit/packages`, `GET /api/credit/costs`, `GET /api/credit/payment-config` provide catalog and config.
- `POST /api/credit/create-payment-intent` creates Stripe PaymentIntent (simulation flag in dev/test).
- Stripe webhook updates credits on successful payment; history via `GET /api/credit/history`.
- Weekly reset job runs per schedule to restore free credits; status reflects last reset.
- UX addendum: Payment flows indicate simulation vs live; show credit deltas and errors clearly; accessible forms and buttons; loading states.

**Technical Notes:** Secure webhook validation; ensure idempotent credit grants; no time estimates.

**Prerequisites:** Epic 1 auth; supports Epics 3–4.

### Story 5.3: Retention & Privacy Enforcement
As a user, I want my data retained or deleted per policy, so that privacy is respected.

**Acceptance Criteria:**
- Retention job schedules deletions: originals after 7 days, generated after 30 days; manual endpoints exist: `GET /api/retentionpolicy/expired-images`, `POST /api/retentionpolicy/delete-expired`, `POST /api/retentionpolicy/initialize-retention-dates`.
- Export and delete flows for photos/model/account are available; deletions remove files + DB records; audit logged.
- Operations avoid PII in logs and enforce authorization.
- UX addendum: Show retention windows; warn before destructive actions; accessible confirmations.

**Technical Notes:** Align with PRD retention; ensure background service configured; surface retention dates via gallery/status.

**Prerequisites:** Epics 1–4 dependencies for data presence.

### Story 5.4: Data Export & Account Deletion (User-Controlled Privacy)
As a user, I want to export and delete my data/account, so that I control my presence.

**Acceptance Criteria:**
- Provide endpoints/flows to export data (images metadata, profile) and delete account with cascading removal (images, models, credits records) and revocation of tokens.
- Confirm destructive actions; return completion confirmation; ensure retention queues cleared.
- UX addendum: Clear warnings/confirmations; accessible dialogs; progress/complete messaging.

**Technical Notes:** Tie into profile lifecycle; coordinate with retention job to avoid resurrecting deleted data.

**Prerequisites:** Epic 1 auth; complements Story 5.3.

---
---

<!-- Epic sections will be added as they are created during the workflow. -->

## Summary

17 stories across 5 epics cover all 12 FRs with architecture alignment (auth, storage, Replicate, Stripe, retention). Credits/retention hardening underpin training/generation. Next step: use `create-story` workflow for implementation planning.

---

## Final Validation

**Quality Checks**
- User value: Each epic delivers user-facing outcomes; foundation epic enables secure access and credit visibility.
- Completeness: All 12 FRs mapped to at least one story; prerequisites noted per epic.
- Technical soundness: Stories include API endpoints, auth, storage paths, webhooks, credit rules, retention, and validation details.
- UX integration: No UX doc provided; stories remain interaction-agnostic but include hooks for validation/error messaging.
- Implementation-ready: Stories sized for single dev sessions; forward dependencies avoided.

**Counts**
- FRs covered: 12/12
- Epics: 5
- Stories: 17

**Output**
- Document: `bdocs/epics.md`
- Coverage: FR coverage map included; webhooks/credits/retention explicitly mapped.

---

_For implementation: Use the `create-story` workflow to generate individual story implementation plans from this epic breakdown._
