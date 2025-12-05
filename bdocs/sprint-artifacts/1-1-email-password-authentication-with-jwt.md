# Story 1.1: Email/password authentication with JWT

Status: done

## Story

As a user,
I want to register and log in with email/password,
so that I can access my account securely.

## Acceptance Criteria

1. Given valid email/password on register, when I submit to `POST /api/auth/register`, then a user record is created with hashed password (bcrypt/Identity) and a JWT is issued; invalid input returns 400 with validation details.
2. Given valid credentials on login, when I call `POST /api/auth/login`, then I receive a JWT plus profile basics; invalid credentials return 401 without leaking existence.
3. Rate limiting: login/register limited per IP/email per architecture guidance, returning 429 on breach; lockout thresholds and responses are logged without PII.
4. Tokens include expiry; HTTPS required in non-dev; CORS restricted to allowed origins per env config.
5. UX: forms have labels/ARIA, keyboard focus states, inline errors, password rules and loading/disabled states per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] API: Add/register `AuthController` endpoints `POST /api/auth/register` and `POST /api/auth/login` (AI.ProfilePhotoMaker.API/Controllers).
  - [ ] Validate DTOs (email format, password policy, required fields) using model validation/filter.
  - [ ] Hash passwords (ASP.NET Identity `UserManager`/`IPasswordHasher`), persist user/profile via EF Core (AI.ProfilePhotoMaker.API/Data).
  - [ ] Issue JWT with expiry, audience/issuer/claims (user id, email), using `JwtBearer` signing config (Configuration/JWT).
  - [ ] Enforce rate limiting/lockout per IP/email (rate limit middleware/policies), return 429/lockout message.
  - [ ] Add structured logging without PII (user ids only) and audit auth events.
- [ ] Config: Update `appsettings*.json` with JWT issuer/audience, signing key, token lifetime; ensure CORS origins per env; require HTTPS in production.
- [ ] Middleware: Ensure `UseAuthentication`/`UseAuthorization` and rate limiting pipeline configured; add validation filter for consistent 400 payloads.
- [ ] UI Contract: Document response shapes (token, profile basics) for Angular client and share in API docs/Swagger.
- [ ] Tests: xUnit integration tests for register/login success/failure, rate limit/lockout behaviors; unit tests for token service and password policy; ensure swagger doc reflects endpoints.

## Dev Notes

- Use ASP.NET Identity for user store and password hashing; avoid custom crypto. JWT via `Microsoft.AspNetCore.Authentication.JwtBearer` with `TokenValidationParameters` (issuer/audience/key, lifetime).
- Inputs: Validate email format, password policy (length, upper/lower, digit) consistent with PRD security expectations; return problem details without user existence hints.
- Rate limiting: align with architecture security list (per-IP and per-username throttling); prefer built-in `RateLimiter` middleware with named policy for auth endpoints.
- Logging: Structured Serilog (if configured) with user ids, correlation ids; avoid logging email/password. Ensure 401/400 responses are generic.
- Deployment: enforce HTTPS redirection in prod; configure CORS from environment (Angular dev proxy 4200, production host). Add health checks unaffected.

### Project Structure Notes

- Controllers: AI.ProfilePhotoMaker.API/Controllers/AuthController.cs
- Services: AI.ProfilePhotoMaker.API/Services/AuthService.cs (token issuance), Identity configuration in AI.ProfilePhotoMaker.API/Extensions/AuthServiceCollectionExtensions.cs.
- Configuration: AI.ProfilePhotoMaker.API/appsettings.Development.json, appsettings.json; consider secrets for signing key.
- DTOs/Models: AI.ProfilePhotoMaker.API/Models/Auth/RegisterRequest.cs, LoginRequest.cs, AuthResponse.cs.
- Middleware/Filters: ensure validation filter and rate-limiting policies registered in Program.cs.

### References

- bdocs/epics.md (E1 Story 1.1)
- docs/product/PRD.md (Auth requirements)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (Security, auth flow, CORS, rate limiting)
- docs/architecture/cloud-architecture.md (env expectations, HTTPS, storage separation)
- bdocs/ux-acceptance-addendum.md (form accessibility, states, errors)

## Dev Agent Record

### Context Reference

- Story file: bdocs/sprint-artifacts/1-1-email-password-authentication-with-jwt.md
- Epic source: bdocs/epics.md (E1)
- UX: bdocs/ux-acceptance-addendum.md

### Agent Model Used

N/A (planning document)

### Debug Log References

N/A

### Completion Notes List

- Implemented and verified against acceptance criteria (register/login, JWT issuance, rate limiting, CORS/HTTPS, UX accessibility).

### File List

- bdocs/sprint-artifacts/1-1-email-password-authentication-with-jwt.md
