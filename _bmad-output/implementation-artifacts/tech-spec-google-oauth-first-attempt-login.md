# Tech-Spec: Google OAuth first-attempt login regression

**Created:** 2026-01-04
**Status:** Completed

## Overview

### Problem Statement
After a Google OAuth redirect, the session is valid (cookie set, `/profile` returns 200 with data) but the UI remains on the login screen. The first attempt consistently fails, while a second attempt immediately lands on the dashboard. `/auth/validate-session` returns 204 for both attempts. This impacts all users in production and local.

### Solution
Remove the race between session validation and guard navigation by ensuring the app waits for session validation before redirecting to `/auth/login`. Add a small auth-state hydration path so that once `validate-session` succeeds, `isAuthenticated$` flips true and the login screen redirects automatically.

### Scope (In/Out)
**In scope**
- Update Angular auth guard to perform a first-time session validation when auth state is unknown.
- Add a reusable `ensureSession()` helper in `AuthService` to validate and hydrate auth state.
- Make the login page react to `isAuthenticated$` changes and redirect when a valid session is detected.

**Out of scope**
- Changes to Google OAuth provider settings.
- Changing `/auth/validate-session` response semantics (204 stays as-is).
- Backend auth/session model changes.

## Context for Development

### Codebase Patterns
- Auth state is managed via `BehaviorSubject` in `AuthService`.
- Session validation uses `/auth/validate-session` with `withCredentials: true` and returns 204 on success.
- Guards use `isAuthenticated$` with `take(1)` (current value only), causing a race on first load after OAuth redirect.

### Files to Reference
- `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs` (ValidateSession returns 204)
- `AI.ProfilePhotoMaker.UI/src/app/services/auth.service.ts` (auth state, validateSession, probeSession)
- `AI.ProfilePhotoMaker.UI/src/app/guards/app.guard.ts` (primary protected route guard)
- `AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.ts` (login flow, OAuth redirect)
- `AI.ProfilePhotoMaker.UI/src/app/app.component.ts` (session probing on navigation)

### Technical Decisions
- Keep `/auth/validate-session` as 204 on success; treat the HTTP 2xx as authenticated.
- Centralize session validation in `AuthService.ensureSession()` so guards and login can use the same logic.
- Do not store JWT in localStorage; rely on HttpOnly cookie and profile hydration as already implemented.

## Implementation Plan

### Tasks
- [x] Add `ensureSession()` in `AuthService` that:
  - Returns `true` if already authenticated.
  - Otherwise calls `validateSession()` and on success sets `_isAuthenticatedSubject` to `true` and triggers `hydrateUserFromProfile()`.
  - On failure returns `false` (no navigation side effects).
- [x] Update `AppGuard`:
  - If `isAuthenticated$` is false and the session has not been verified yet, call `ensureSession()` and only redirect if it returns false.
  - Preserve existing email verification and profile completion checks.
- [x] Update `LoginComponent` to reactively redirect:
  - Subscribe once to `isAuthenticated$` (or `ensureSession()` in `ngOnInit`) and navigate to `returnUrl` when it becomes `true`.
  - Avoid double-navigation if already on dashboard.
- [x] Add minimal test coverage where feasible:
  - Unit test for `ensureSession()` (mock validateSession success/fail).
  - Guard behavior test: when validate succeeds, guard allows; when validate fails, redirects.

### Acceptance Criteria
- [x] Given a valid session cookie after OAuth redirect, when navigating to `/app/dashboard`, then the user lands on the dashboard without an extra login click.
- [x] Given a valid session cookie while on `/auth/login`, when session validation succeeds, then the login page redirects to the return URL.
- [x] Given an invalid session, when navigating to protected routes, then the user is redirected to `/auth/login` with the existing message/returnUrl behavior.
- [x] `/auth/validate-session` remains 204 on success, 401 on failure; no backend contract changes.

## Additional Context

### Dependencies
- Angular, RxJS (`BehaviorSubject`, `switchMap`, `take`, `catchError`)

### Testing Strategy
- Manual: clear localStorage, perform OAuth login, confirm first redirect lands on dashboard without a second click.
- Unit tests: mock `validateSession` to validate guard branching and `ensureSession` state updates.

### Notes
- The first-attempt failure is a race between guard evaluation and async session validation; this fix prioritizes correctness over optimistic redirects.

## Review Notes
- Adversarial review completed
- Findings: 10 total, 10 fixed, 0 skipped
- Resolution approach: walk-through
