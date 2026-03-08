---
title: 'Fix Admin User View And Add Activity History'
slug: 'fix-admin-user-view-and-add-activity-history'
created: '2026-03-08T08:31:18-07:00'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Angular standalone components', 'TypeScript', 'RxJS', 'ASP.NET Core Web API', 'Entity Framework Core', 'ASP.NET Identity', 'xUnit', 'Jasmine']
files_to_modify: ['AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.ts', 'AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.html', 'AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.sass', 'AI.ProfilePhotoMaker.UI/src/app/services/admin.service.ts', 'AI.ProfilePhotoMaker.API/Models/DTOs/AdminDtos.cs', 'AI.ProfilePhotoMaker.API/Services/IAdminService.cs', 'AI.ProfilePhotoMaker.API/Services/AdminService.cs', 'AI.ProfilePhotoMaker.API/Controllers/AdminController.cs', 'AI.ProfilePhotoMaker.API.Tests/Controllers/AdminControllerTests.cs', 'AI.ProfilePhotoMaker.API.Tests/Services/AdminServiceTests.cs', 'AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.spec.ts']
code_patterns: ['Admin UI uses standalone Angular components with RouterModule and shared admin shell styles', 'API responses use BaseController.SuccessResponse with success/data envelope', 'AdminService in UI extends BaseHttpService for response extraction and error handling', 'Admin backend logic is centralized in AdminService with controller thin endpoints', 'Repository/query helpers should be reused for optimized profile stats and recent image retrieval', 'Admin pages should expose explicit loading and error states instead of blank conditional rendering only']
test_patterns: ['Backend controller tests use xUnit and Moq in AI.ProfilePhotoMaker.API.Tests/Controllers', 'Backend service tests use EF Core in-memory context in AI.ProfilePhotoMaker.API.Tests/Services', 'Angular admin component tests use Jasmine spies with direct component instantiation']
---

# Tech-Spec: Fix Admin User View And Add Activity History

**Created:** 2026-03-08T08:31:18-07:00

## Overview

### Problem Statement

The `View` action from `/admin/users` currently leads to an empty admin user detail page in production, which blocks admins from inspecting what a user actually did in the product. This makes it impossible to diagnose states like a user having `0` credits because there is no visibility into purchases, uploads, generations, or other recent activity.

### Solution

Repair the admin user detail flow and evolve the `View` screen into a read-only diagnostics page. The page should show core user profile data, current credit state, product usage signals, recent activity history, and recent uploaded/generated image thumbnails so an admin can quickly understand whether the user used the system and what happened.

### Scope

**In Scope:**
- Fix the blank `/admin/users/:userId` admin detail experience
- Confirm and repair the API/UI data flow behind the `View` action
- Show user summary details and current credit balance context
- Show usage/activity history relevant to diagnosing zero-credit states
- Show recent generated and uploaded image thumbnails
- Include enough user-facing diagnostics for admins to distinguish no-usage vs consumed-usage vs operational issue

**Out of Scope:**
- Editing user profile fields from the detail page
- Adding new destructive admin actions beyond existing deactivate/reactivate/delete/credit adjustment flows
- Broad redesign of the full admin dashboard outside the user detail experience
- Building a full analytics platform or cross-user reporting layer

## Context for Development

### Codebase Patterns

- Admin UI uses standalone Angular components under `AI.ProfilePhotoMaker.UI/src/app/admin`, with navigation tabs and shared styles from `admin-shared.sass`
- Admin routes are defined centrally in `AI.ProfilePhotoMaker.UI/src/app/app.routes.ts` and protected by `AdminGuard`
- UI admin API access flows through `AI.ProfilePhotoMaker.UI/src/app/services/admin.service.ts`, which extends `BaseHttpService` and expects `BaseController.SuccessResponse(...)` envelopes
- The current `admin-user-detail` component is a minimal happy-path implementation: it reads the route param once, calls `getUserDetail`, assigns `user`, and only renders when `user` is truthy
- Unlike `admin-dashboard`, the current detail page has no loading state, no retry path, no error surface, and no timeout or watchdog handling, which can produce a visually blank page whenever the request fails or returns nothing usable
- Admin backend logic is intentionally thin in `AdminController` and concentrated in `AdminService`, so the correct extension point for activity aggregation is the service layer plus DTOs
- Existing optimized query patterns already exist in `UserProfileRepository` for profile stats and recent images; these should be reused rather than loading whole entity graphs
- Existing data sources relevant to diagnostics are already present: `UserProfile` credits and profile fields, `UsageLog`, `CreditPurchase`, `ProcessedImage`, `ModelCreationRequest`, `PendingGenerationRequest`, and `AdminAuditLog`
- No `project-context.md` file was found during investigation

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `AI.ProfilePhotoMaker.UI/src/app/admin/admin-users/admin-users.component.html` | Current user list and `View` action entry point |
| `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.ts` | Current detail component logic; needs loading/error and richer data handling |
| `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.html` | Current detail template; currently only static profile fields under `*ngIf="user"` |
| `AI.ProfilePhotoMaker.UI/src/app/app.routes.ts` | Confirms `/admin/users/:userId` route is registered correctly |
| `AI.ProfilePhotoMaker.UI/src/app/services/base-http.service.ts` | Defines wrapped API response extraction and error propagation |
| `AI.ProfilePhotoMaker.UI/src/app/services/admin.service.ts` | Admin HTTP client and DTO definitions to extend for diagnostics payload |
| `AI.ProfilePhotoMaker.UI/src/app/admin/admin-dashboard/admin-dashboard.component.ts` | Existing admin-page loading/error/retry pattern worth following |
| `AI.ProfilePhotoMaker.API/Controllers/BaseController.cs` | Confirms response envelope format used by admin endpoints |
| `AI.ProfilePhotoMaker.API/Controllers/AdminController.cs` | Existing admin user detail endpoint surface; likely to extend or add dedicated activity endpoint |
| `AI.ProfilePhotoMaker.API/Services/IAdminService.cs` | Current service contract; must expand for richer detail/activity payload |
| `AI.ProfilePhotoMaker.API/Services/AdminService.cs` | Core aggregation point for user detail and activity history |
| `AI.ProfilePhotoMaker.API/Models/DTOs/AdminDtos.cs` | Existing admin DTO shapes to extend for activity, metrics, and recent images |
| `AI.ProfilePhotoMaker.API/Data/IUserProfileRepository.cs` | Existing repository contract for stats/recent images |
| `AI.ProfilePhotoMaker.API/Data/UserProfileRepository.cs` | Reusable optimized stats and recent images implementation |
| `AI.ProfilePhotoMaker.API/Services/ICreditPackageService.cs` | Existing purchase history service contract |
| `AI.ProfilePhotoMaker.API/Services/CreditPackageService.cs` | Existing per-user credit purchase history query |
| `AI.ProfilePhotoMaker.API/Models/UsageLog.cs` | User activity log schema including action, details, credit cost, remaining credits |
| `AI.ProfilePhotoMaker.API/Models/CreditPurchase.cs` | Credit purchase history schema |
| `AI.ProfilePhotoMaker.API/Models/ProcessedImage.cs` | Uploaded/generated image schema with flags and timestamps |
| `AI.ProfilePhotoMaker.API/Models/ModelCreationRequest.cs` | Training lifecycle data source |
| `AI.ProfilePhotoMaker.API/Models/PendingGenerationRequest.cs` | Pending generation lifecycle data source |
| `AI.ProfilePhotoMaker.API/Models/AdminAuditLog.cs` | Admin action history to optionally include on detail page |
| `AI.ProfilePhotoMaker.API/Services/BasicTierService.cs` | Confirms where user usage logs are written |
| `AI.ProfilePhotoMaker.API.Tests/Controllers/AdminControllerTests.cs` | Existing controller test conventions and current detail endpoint coverage |
| `AI.ProfilePhotoMaker.API.Tests/Services/AdminServiceTests.cs` | Existing service test conventions with EF in-memory context |

### Technical Decisions

- Treat this as both a bug fix and a UX/data-surface enhancement
- Keep the detail page read-only in the first version
- Prioritize diagnostic value over full account management controls
- Include recent uploaded/generated image thumbnails in the first release, not just text metrics
- The detail experience should help answer why a user has zero credits using observable product history, not guesswork
- Prefer a single admin-focused aggregate detail payload over multiple loosely coordinated UI calls so the page can render deterministically and stay easier to test
- Reuse existing optimized data access patterns from `UserProfileRepository` and `CreditPackageService` rather than hand-rolling broad entity includes inside the controller
- Add explicit UI loading, empty, and error states modeled after `admin-dashboard` so failures do not present as a blank page
- Build the activity timeline from concrete persisted sources already in the app: `UsageLog`, `CreditPurchase`, `ProcessedImage`, `ModelCreationRequest`, `PendingGenerationRequest`, and optional admin audit entries for the viewed user
- Add focused backend and UI tests around the detail page because current test coverage only confirms not-found handling on the controller side and does not cover the richer diagnostics flow

## Implementation Plan

### Tasks

- [x] Task 1: Define an admin user diagnostics response model
  - File: `AI.ProfilePhotoMaker.API/Models/DTOs/AdminDtos.cs`
  - Action: Add DTOs for the richer detail screen, including a top-level admin user diagnostics DTO plus nested summary, metrics, timeline entry, recent image, credit purchase, and optional admin action shapes.
  - Notes: Keep the existing `AdminUserDetailDto` compatible or replace it cleanly in the admin detail endpoint contract; include timestamps and image flags needed by the UI without exposing mutation fields.

- [x] Task 2: Expand the admin service contract for diagnostics retrieval
  - File: `AI.ProfilePhotoMaker.API/Services/IAdminService.cs`
  - Action: Replace or extend `GetUserDetailAsync` so the service returns the aggregate admin diagnostics payload needed by the detail page.
  - Notes: Keep the API focused on read-only diagnostics; if a new method name is clearer than overloading `GetUserDetailAsync`, align the controller and UI accordingly.

- [x] Task 3: Aggregate user diagnostics in the admin service
  - File: `AI.ProfilePhotoMaker.API/Services/AdminService.cs`
  - Action: Build a single diagnostics payload by composing user/profile data, credits, purchase history, profile stats, recent images, usage logs, model creation requests, pending generation requests, and user-targeted admin audit entries.
  - Notes: Reuse `UserProfileRepository` query patterns and existing purchase-history logic instead of broad eager loading. Normalize the output into a timeline that can explain usage and zero-credit states.

- [x] Task 4: Add any missing backend dependencies required for efficient aggregation
  - File: `AI.ProfilePhotoMaker.API/Services/AdminService.cs`
  - File: `AI.ProfilePhotoMaker.API/Program.cs` or equivalent DI registration file if needed
  - Action: Inject any repository/service dependencies needed to fetch recent images and purchase history efficiently.
  - Notes: Prefer existing services/repositories over duplicating queries; keep DI changes minimal and consistent with current registration patterns.

- [x] Task 5: Extend the admin detail endpoint to return diagnostics data
  - File: `AI.ProfilePhotoMaker.API/Controllers/AdminController.cs`
  - Action: Update `GET /api/admin/users/{userId}` to return the new diagnostics payload and preserve standardized success/error envelopes.
  - Notes: Keep not-found behavior intact; if a dedicated route such as `/activity` is added instead, document and wire the UI to that route explicitly.

- [x] Task 6: Add backend coverage for diagnostics aggregation and controller behavior
  - File: `AI.ProfilePhotoMaker.API.Tests/Controllers/AdminControllerTests.cs`
  - File: `AI.ProfilePhotoMaker.API.Tests/Services/AdminServiceTests.cs`
  - Action: Add tests for successful diagnostics retrieval, not-found behavior, recent image inclusion, and timeline/purchase aggregation for representative zero-credit and active-usage cases.
  - Notes: Use the existing xUnit + Moq + EF in-memory patterns already established in the admin tests.

- [x] Task 7: Extend the Angular admin service contract for the diagnostics payload
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/admin.service.ts`
  - Action: Add TypeScript interfaces matching the backend diagnostics DTO and update `getUserDetail(...)` or add a dedicated method to consume the richer response.
  - Notes: Keep the service using `BaseHttpService` response extraction so the UI receives the unwrapped payload.

- [x] Task 8: Make the admin user detail component resilient and route-aware
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.ts`
  - Action: Replace the current bare subscription with a proper loading flow that tracks `isLoading`, `error`, and diagnostics data, and handles route param changes, failed requests, and missing users cleanly.
  - Notes: Follow the `admin-dashboard` pattern for retry/error handling so the page never appears blank again.

- [x] Task 9: Build the diagnostics-focused admin detail UI
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.html`
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.sass`
  - Action: Replace the current minimal template with a read-only diagnostics layout showing user summary, current credit balance context, usage metrics, recent credit purchases, recent activity timeline, and recent uploaded/generated image thumbnails.
  - Notes: Use the existing admin shell/nav language and ensure the page remains useful when sections are empty, such as no purchases or no image history.

- [x] Task 10: Add Angular tests for happy path and failure states
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.spec.ts`
  - Action: Add component tests covering successful diagnostics rendering, error state rendering, and loading-to-loaded transition.
  - Notes: Match the direct component-instantiation + Jasmine spy pattern already used in admin component specs.

- [ ] Task 11: Manually verify the end-to-end admin workflow
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-users/admin-users.component.html`
  - File: `AI.ProfilePhotoMaker.UI/src/app/app.routes.ts`
  - Action: Verify that clicking `View` from `/admin/users` consistently reaches the diagnostics page and that the page is informative for users with no usage, zero remaining credits after usage, and recent uploaded/generated images.
  - Notes: This is a verification task, not a code change task, but it is required to confirm the original production symptom is gone.

### Acceptance Criteria

- [x] AC 1: Given an authenticated admin on `/admin/users`, when they click `View` for a valid user, then `/admin/users/:userId` loads a non-blank diagnostics page with a visible loading, loaded, or error state.
- [x] AC 2: Given a valid target user exists, when the admin detail request succeeds, then the page shows core user information including email, name, account status, subscription tier, and current credit balance.
- [x] AC 3: Given a user has product history, when the diagnostics page loads, then it shows usage metrics that help explain account state, including uploaded image count, generated image count, and recent activity timestamps.
- [x] AC 4: Given a user has recent uploads or generated images, when the diagnostics page loads, then recent image thumbnails are shown with enough metadata to distinguish uploaded vs generated assets.
- [x] AC 5: Given a user has credit purchase history, when the diagnostics page loads, then the admin can see recent purchase entries with awarded credits, amount paid, and purchase date.
- [x] AC 6: Given a user has usage log entries or generation/training lifecycle records, when the diagnostics page loads, then the admin can see a recent activity timeline that helps determine whether the user actually used the system.
- [x] AC 7: Given a user has zero current credits because they consumed them through product use, when the diagnostics page loads, then the available diagnostics make that state distinguishable from a user who never engaged with the product.
- [x] AC 8: Given the diagnostics request fails or the target user does not exist, when the admin opens the detail route, then the UI shows an explicit error state with retry or clear failure messaging instead of an empty page.
- [x] AC 9: Given a user has no purchases, no recent images, or no timeline events, when the diagnostics page loads, then the page renders stable empty states for those sections without layout collapse or blank output.
- [x] AC 10: Given the backend diagnostics endpoint is called for an existing user, when it returns successfully, then the response remains wrapped in the standard `{ success, data, message, error }` admin API envelope expected by `BaseHttpService`.

## Additional Context

### Dependencies

- Existing admin auth guard and admin API authorization
- Existing user/profile/image/credit data already present in the application data model
- Existing `UserProfileRepository` optimized stats/recent image query patterns
- Existing `CreditPackageService.GetUserPurchaseHistoryAsync(...)` purchase-history query
- Existing `UsageLog` persistence from `BasicTierService.LogUsageAsync(...)`
- Existing admin API response envelope and Angular `BaseHttpService` extraction behavior

### Testing Strategy

- Backend unit/service tests:
  - Add controller coverage for successful diagnostics retrieval and not-found handling.
  - Add service-level aggregation tests for users with no activity, users with usage logs and zero remaining credits, and users with recent uploads/generated images.
- Frontend component tests:
  - Assert loading, success, and error rendering in `admin-user-detail.component.spec.ts`.
  - Assert representative diagnostics sections render correctly when purchase history, timeline entries, and thumbnails are present or absent.
- Manual verification:
  - Open `/admin/users`, click `View` for a known active user, a low/no-usage user, and a user with recent generated images.
  - Confirm the page never appears blank and that the rendered data explains the user’s state.
  - Confirm retry/error behavior on simulated endpoint failure.

### Notes

- User explicitly wants recent generated and uploaded image thumbnails included in the first version.
- Highest-risk implementation area is timeline composition: data comes from multiple sources with different semantics, so ordering and labeling need to stay simple and auditable.
- The current blank-screen symptom may be caused by request failure or missing data without any UI fallback; fixing that resilience is mandatory even if richer diagnostics are added.
- If direct service reuse from `CreditPackageService` or repository injection becomes awkward, a constrained admin-specific query inside `AdminService` is acceptable, but avoid broad unbounded includes.
