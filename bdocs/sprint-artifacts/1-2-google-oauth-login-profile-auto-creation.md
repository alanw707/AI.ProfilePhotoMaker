# Story 1.2: Google OAuth login & profile auto-creation

Status: done

## Story

As a user,
I want to sign in with Google,
so that I can onboard quickly without a password and have my profile created automatically.

## Acceptance Criteria

1. `GET /api/auth/google-oauth-url` returns the provider URL with state/nonce; only allowed origins per env can call it; failures return 400.
2. `GET /api/auth/external-login/{provider}` (Google) validates the returned tokens within a 5-minute window, enforces state/nonce/correlation, and issues a JWT on success.
3. On first successful external login, a profile is auto-created and initialized with default weekly credits; subsequent logins reuse the same profile and do not duplicate users.
4. Invalid state/nonce, expired tokens, or replay attempts return 400/401 with safe, non-PII messaging; events are logged without leaking user existence.
5. Session/security hardening: HTTPS required outside dev; CORS restricted to allowed origins; rate limiting/lockout applies to external login endpoints similar to email/password flows.
6. UX: buttons/links have labels/ARIA, keyboard/focus states, inline errors, and loading/disabled states during redirect per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] API: Extend `AuthController` with `GET /api/auth/google-oauth-url` and `GET /api/auth/external-login/google`.
  - [ ] Build provider URL using Google options (client id, redirect, scopes), include state/nonce, set CORS to allowed origins.
  - [ ] Handle callback: validate tokens (5-minute window), state/nonce, map external principal to Identity user, create new user/profile on first login with default weekly credits, issue JWT + profile basics.
  - [ ] Apply rate limiting/lockout policy to external login endpoint; return 429 on breach.
  - [ ] Log attempts without PII (user id only once resolved), audit failures separately.
- [ ] Configuration: Add Google OAuth settings (client id/secret, callback URL) to `appsettings*.json`/user secrets; ensure HTTPS redirect in non-dev; confirm CORS origins include UI host.
- [ ] Services: Reuse token issuance from Story 1.1; add external login handler/service for Google sign-in and profile provisioning.
- [ ] Data/Credits: On first login, initialize weekly credits per PRD; ensure idempotent profile creation and no duplicate accounts.
- [ ] Tests: xUnit integration tests for success path, invalid state/nonce, expired token, replay; verify profile auto-creation and idempotency; confirm rate limit behavior.
- [ ] Docs: Update Swagger/OpenAPI for new endpoints; document response shapes for Angular client.

## Dev Notes

- Use ASP.NET Core Identity external login flow; Google handler with state/nonce and correlation cookie. Validate token lifetime (<=5 minutes) and issuer/audience.
- JWT issuance, logging, validation filter, and rate-limiting middleware are already in place from Story 1.1—reuse the same services and policies.
- Default weekly credits: set on first profile creation; ensure credit fields initialized consistently with PRD (weekly free credits + purchased credits = 0).
- Security: enforce HTTPS redirection in prod; CORS per env; avoid leaking existence on failed external logins; store only provider subject id and email as needed.
- Observability: structured logs (user id, provider, correlation id), no tokens/PII. Add audit event for external login success/failure.

### Project Structure Notes

- Controllers: AI.ProfilePhotoMaker.API/Controllers/AuthController.cs (external login endpoints).
- Services: AI.ProfilePhotoMaker.API/Services/ExternalAuthService.cs (Google handler), reuse AuthService for JWT issuance.
- Configuration: AI.ProfilePhotoMaker.API/appsettings.Development.json (Google credentials placeholders), production secrets via env/user-secrets.
- Data/Profiles: AI.ProfilePhotoMaker.API/Data (profile creation + weekly credits initialization).
- Middleware/Filters: existing validation filter and rate-limiting policies applied to external login routes.

### References

- bdocs/epics.md (E1 Story 1.2)
- docs/product/PRD.md (Auth/OAuth requirements)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (Auth flow, CORS/HTTPS, rate limiting)
- docs/architecture/cloud-architecture.md (environment expectations)
- bdocs/ux-acceptance-addendum.md (accessibility, states, errors)

## Previous Story Intelligence

- Story 1.1 established JWT issuance, Identity password flows, rate-limiting policies, validation filters, and structured logging without PII; reuse those services/policies for external login.
- CORS/HTTPS already configured; keep the same origins and HTTPS enforcement for Google callback endpoints.

## Git Intelligence Summary

- Recent commits (for awareness of current patterns):
  - 8609ec4 Stabilize pricing scroll Playwright spec
  - ac10c43 Add pricing scroll UI-only Playwright spec
  - a725fe5 Fix pricing purchase scroll to billing form (#205)
  - d01733f fix: harden auth navigation + logging (#204)
  - 450f8ae launch/mvp ready (#203)

## Dev Agent Record

### Context Reference

- Story file: bdocs/sprint-artifacts/1-2-google-oauth-login-profile-auto-creation.md
- Epic source: bdocs/epics.md (E1)
- UX: bdocs/ux-acceptance-addendum.md

### Agent Model Used

N/A (planning document)

### Debug Log References

N/A

### Completion Notes List

- Story context created and marked done per user confirmation; external login implemented/verified against AC.

### File List

- bdocs/sprint-artifacts/1-2-google-oauth-login-profile-auto-creation.md
