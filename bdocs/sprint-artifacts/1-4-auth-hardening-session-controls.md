# Story 1.4: Auth hardening & session controls

Status: done

## Story

As a user,
I want secure sessions and protected auth endpoints,
so that my account is safe from misuse.

## Acceptance Criteria

1. JWT validation middleware enforces expiry, signature, issuer/audience; rejects tampered or expired tokens with 401.
2. CORS policies are environment-scoped; HTTPS is required outside dev; HSTS enabled in prod.
3. Rate limiting/lockout applied to auth endpoints (register/login/external) with 429/lockout responses; brute-force attempts logged without PII.
4. Audit logging for auth events (login success/fail, lockout) captures user id and correlation id only; no secrets/PII in logs.
5. UX: user-facing errors are generic, maintain focus/keyboard states, and disable buttons during submit per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Middleware: Ensure `UseAuthentication`/`UseAuthorization` precede endpoint mapping; add `UseHsts` + HTTPS redirection in prod; configure CORS per env.
- [ ] JWT validation: Configure `JwtBearerOptions` with issuer/audience/key, lifetime validation, clock skew minimal; add event handlers to reject tampered tokens.
- [ ] Rate limiting/lockout: Apply named policies to auth endpoints (register/login/external); integrate lockout thresholds with Identity; return 429/lockout responses.
- [ ] Logging/Audit: Add structured audit logs for auth events (success/fail, lockout) with user id only; ensure exception middleware hides internals.
- [ ] Tests: Integration tests for expired/invalid token rejection, CORS/HTTPS enforcement (env-based), rate-limit and lockout behavior, and audit log emission (shape/fields, no PII).

## Dev Notes

- Reuse Identity + JWT setup from 1.1/1.2; centralize auth policies in Program.cs or extension.
- Lockout thresholds from config; ensure per-user + per-IP throttling on auth endpoints.
- HSTS/HTTPS only in non-dev; CORS origins set from config (Angular dev proxy 4200, prod host).
- Logging via Serilog; scrub headers/secrets; include correlation id.

### Project Structure Notes

- Middleware/config: Program.cs; extensions in AI.ProfilePhotoMaker.API/Extensions/AuthServiceCollectionExtensions.cs or similar.
- Policies: Rate limiting configuration in appsettings*.json or code.
- Logging: Serilog config in appsettings*.json.

### References

- bdocs/epics.md (E1 Story 1.4)
- docs/product/PRD.md (Auth security expectations)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (CORS/HTTPS, rate limiting)
- docs/architecture/cloud-architecture.md (env security posture)
- bdocs/ux-acceptance-addendum.md (errors/states/accessibility)

## Previous Story Intelligence

- 1.1 and 1.2 established JWT issuance, Identity, external login, and rate-limit scaffolding; extend same policies to cover session controls.

## Git Intelligence Summary

- Recent commits (awareness): 8609ec4, ac10c43, a725fe5, d01733f, 450f8ae.

## Dev Agent Record

### Context Reference

- Story file: bdocs/sprint-artifacts/1-4-auth-hardening-session-controls.md
- Epic source: bdocs/epics.md (E1)
- UX: bdocs/ux-acceptance-addendum.md

### Agent Model Used

N/A (planning document)

### Debug Log References

N/A

### Completion Notes List

- Story context created via create-story workflow; status set to ready-for-dev.

### File List

- bdocs/sprint-artifacts/1-4-auth-hardening-session-controls.md
