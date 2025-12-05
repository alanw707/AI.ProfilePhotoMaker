# Story 1.3: Profile management & credit status

Status: done

## Story

As a user,
I want to view and manage my profile and see my current credits,
so that I know my account state.

## Acceptance Criteria

1. `GET/POST/PUT/DELETE /api/profile` manages profile data; auth required; validates fields and ownership; returns 400 on validation errors and 404 on missing/unauthorized resources.
2. `GET /api/credit/status` returns weekly and purchased credits, last reset timestamp, and whether weekly reset is available; responses are consistent and cache-safe.
3. Data export and account delete endpoints are discoverable (link to retention/export flows) but primary CRUD works; destructive actions require ownership and confirmation semantics.
4. All responses avoid leaking other users’ data; enforce authorization via user context and Identity user id.
5. UX: forms accessible with labels/ARIA, keyboard/focus states; inline validation errors; clear display of credit balances and empty/loading states per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] API: Implement `ProfileController` (AI.ProfilePhotoMaker.API/Controllers) with `GET/POST/PUT/DELETE /api/profile` using DTOs and model validation.
  - [ ] Enforce ownership via user context; map profile to Identity user; return 404 for missing/not-owned resources.
  - [ ] Validate required fields (name, optional photo metadata) with model validation filter; return problem details 400.
- [ ] API: Implement `GET /api/credit/status` returning weekly credits remaining, purchased credits balance, last reset, and eligibility flag.
- [ ] Services/Data: Add profile persistence (EF Core) and credit status projection from credit tables + weekly credits tracker; ensure concurrency-safe reads.
- [ ] Authorization/Config: Ensure `[Authorize]` on endpoints; CORS/HTTPS per env; reuse JWT/Identity setup from Stories 1.1/1.2.
- [ ] UX Contract: Document response shapes for Angular client; include credit status fields; add Swagger annotations.
- [ ] Tests: xUnit integration tests for profile CRUD (happy/validation/ownership), credit status response shape, and auth-required behaviors.

## Dev Notes

- Reuse Identity user id from claims for all profile/credit lookups; never trust client-provided ids.
- Credit status should align with PRD: weekly free credits + purchased credits; include last reset and reset eligibility (basic tier weekly refresh). Avoid time estimates.
- Validation: consistent problem-details payloads via validation filter; log with user id only (no PII values).
- Authorization: require JWT; ensure CORS origins and HTTPS enforced outside dev; keep responses generic for unauthorized/forbidden cases.
- Observability: structured logging (Serilog) with correlation id; avoid logging profile contents; audit updates/deletes.

### Project Structure Notes

- Controllers: AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs; Credits status endpoint co-located or in CreditController.
- Services: AI.ProfilePhotoMaker.API/Services/ProfileService.cs, CreditStatusService.cs (or equivalent) for aggregation.
- Data: AI.ProfilePhotoMaker.API/Data (profile entity, credit/purchase entities), EF Core context; migrations if schema changes needed.
- Configuration: appsettings*.json for CORS/HTTPS; no secrets in repo; weekly credit defaults from config.
- Middleware/Filters: validation filter already present; authorization via JWT middleware configured in Program.cs.

### References

- bdocs/epics.md (E1 Story 1.3)
- docs/product/PRD.md (Profile/Credit requirements)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (security, CORS/HTTPS, rate limiting)
- docs/architecture/cloud-architecture.md (env expectations)
- bdocs/ux-acceptance-addendum.md (accessibility, states, errors)

## Previous Story Intelligence

- Story 1.1 established JWT/Identity, validation filter, rate-limiting, and structured logging; reuse these for profile/credit endpoints.
- Story 1.2 set external login/profile auto-creation; ensure profile CRUD and credit status operate on the same profile records and do not duplicate user data.

## Git Intelligence Summary

- Recent commits (awareness of patterns):
  - 8609ec4 Stabilize pricing scroll Playwright spec
  - ac10c43 Add pricing scroll UI-only Playwright spec
  - a725fe5 Fix pricing purchase scroll to billing form (#205)
  - d01733f fix: harden auth navigation + logging (#204)
  - 450f8ae launch/mvp ready (#203)

## Dev Agent Record

### Context Reference

- Story file: bdocs/sprint-artifacts/1-3-profile-management-credit-status.md
- Epic source: bdocs/epics.md (E1)
- UX: bdocs/ux-acceptance-addendum.md

### Agent Model Used

N/A (planning document)

### Debug Log References

N/A

### Completion Notes List

- Story context created via create-story workflow; status set to ready-for-dev.

### File List

- bdocs/sprint-artifacts/1-3-profile-management-credit-status.md
