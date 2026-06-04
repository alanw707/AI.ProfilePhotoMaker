---
title: 'Admin Panel with User & Credit Management'
slug: 'admin-panel-user-credit-management'
created: '2026-02-16T00:00:00Z'
status: 'code-review-complete'
stepsCompleted: [1, 2, 3, 4, 5]
tech_stack: ['ASP.NET Core 8+', 'Entity Framework Core', 'SQL Server', 'ASP.NET Identity with IdentityRole', 'JWT (cookie-based)', 'Angular 17+ (standalone components)', 'xUnit + Moq', 'Stripe']
files_to_modify: ['AI.ProfilePhotoMaker.API/Controllers/AdminController.cs', 'AI.ProfilePhotoMaker.API/Services/Authentication/AuthService.cs', 'AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs', 'AI.ProfilePhotoMaker.API/Models/ApplicationUser.cs', 'AI.ProfilePhotoMaker.API/Models/UserProfile.cs', 'AI.ProfilePhotoMaker.UI/src/app/app.routes.ts', 'AI.ProfilePhotoMaker.UI/src/app/services/auth.service.ts']
code_patterns: ['BaseController inheritance with SuccessResponse/ErrorResponse helpers', 'Service layer with interface (e.g., ICreditPackageService)', '[Authorize(Roles = "Admin")] for role-based access', 'DTOs for all API request/response', 'AppGuard pattern for Angular route protection', 'BaseHttpService for frontend API calls']
test_patterns: ['xUnit + Moq in AI.ProfilePhotoMaker.API.Tests/Controllers/', 'Mock services and ILogger', 'Controller test pattern: create controller with mocked deps, assert OkObjectResult']
---

# Tech-Spec: Admin Panel with User & Credit Management

**Created:** 2026-02-16

## Overview

### Problem Statement

Currently, all administrative tasks (user management, credit adjustments, coupon management) require direct database access, creating operational friction and security risks.

### Solution

Build a secure, role-based admin panel accessible at `/admin` with full user management, credit controls, and coupon/discount code management capabilities.

### Scope

**In Scope:**
- Role-based admin access (Admin role)
- `/admin` route protected from public access (auth + role check required)
- User management: deactivate/delete accounts
- Credit management: add/subtract credits from user accounts
- Coupon system: percentage & fixed discounts with "first N users" limits
- Audit logging for all admin actions

**Out of Scope:**
- Password reset for users
- User activity/usage analytics
- Subscription plan management
- Bulk operations (batch credit updates, bulk deletes)

## Context for Development

### Codebase Patterns

**Backend (Verified):**
- ASP.NET Core 8+ with Entity Framework Core and SQL Server
- `ApplicationUser` extends `IdentityUser` — inherits `LockoutEnd`, `LockoutEnabled` fields (already in DB schema) — can be used for account deactivation
- Identity configured with `AddIdentity<ApplicationUser, IdentityRole>()` in `Program.cs` (line 273) — role infrastructure exists but no roles are seeded or assigned
- JWT cookie-based auth via `JwtCookieAuthMiddleware` — tokens set as HttpOnly cookies
- `BaseController` provides: `GetCurrentUserId()`, `ValidateAuthentication()`, `SuccessResponse()`, `ErrorResponse()`, `ExecuteAsync<T>()`, logging helpers (`S()`, `Sid()`)
- Existing `AdminController` does NOT extend `BaseController` — extends `ControllerBase` directly
- `[Authorize(Roles = "Admin")]` already used on `StyleController` (lines 273, 318, 367) — pattern established but non-functional because roles are never added to JWT claims
- Service layer pattern: interfaces registered in `Program.cs` (e.g., `ICreditPackageService`)
- `UserProfile.Credits` (int) is the credit balance — starts at 5, tops up weekly

**Frontend (Verified):**
- Angular 17+ with standalone components and lazy loading via `loadComponent`
- `AuthService._isProtectedPath()` already treats `/admin` as protected (line 182)
- `AppGuard` handles `/app` routes — checks auth + email verification + profile completion
- `BaseHttpService` provides typed HTTP helpers with `ApiResponse<T>` unwrapping
- `ConfigService` for API URL building
- No admin guard exists — needs to be created
- No admin route exists in `app.routes.ts` — needs to be added

**Critical Gap — JWT Role Claims (Verified):**
- `AuthService.GenerateJwtToken()` builds claims with: Name, NameIdentifier, Jti, Email, GivenName, Surname
- **Roles are NEVER queried or added to JWT claims** — `GetRolesAsync()` is never called anywhere in the codebase
- This means `[Authorize(Roles = "Admin")]` on StyleController is currently non-functional for all users
- Fix: Fetch roles in each caller and pass as parameter to `GenerateJwtToken` — avoids breaking the synchronous signature

**Coupon/Discount System (Verified Clean Slate):**
- No `Coupon` model, table, or service exists
- Only reference: `CreateSubscriptionRequestDto.CouponCode` (unused DTO field — subscription system not active)
- Full implementation from scratch required

**Account Deactivation (Verified):**
- `IdentityUser` provides `LockoutEnd` and `LockoutEnabled` — already in DB schema
- Can set `LockoutEnd` to `DateTimeOffset.MaxValue` for permanent deactivation
- No custom `IsDeactivated` field exists on `ApplicationUser` or `UserProfile`

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `API/Controllers/AdminController.cs` | Existing admin controller — extend with user/credit/coupon endpoints |
| `API/Controllers/BaseController.cs` | Base controller pattern — AdminController should inherit from this |
| `API/Controllers/CreditController.cs` | Credit operations reference — purchasing flow + coupon integration point |
| `API/Controllers/StyleController.cs` | Has existing `[Authorize(Roles = "Admin")]` pattern (lines 273, 318, 367) |
| `API/Data/ApplicationDbContext.cs` | DB context — add new DbSets for Coupon, CouponRedemption, AdminAuditLog |
| `API/Models/ApplicationUser.cs` | User model — inherits LockoutEnd/LockoutEnabled from IdentityUser |
| `API/Models/UserProfile.cs` | User profile — `Credits` field for credit adjustments |
| `API/Models/CreditPurchase.cs` | Credit purchase model — reference for credit recording pattern |
| `API/Models/CreditPackage.cs` | Credit package model — reference for coupon discount application |
| `API/Models/PaymentTransaction.cs` | Defines `PaymentStatus` enum |
| `API/Models/DTOs/SubscriptionDto.cs` | Has unused `CouponCode` field — reference only |
| `API/Services/Authentication/AuthService.cs` | JWT generation — MUST add roles parameter (line 136-144) |
| `API/Services/Authentication/interfaces/IAuthService.cs` | Auth service interface |
| `API/Program.cs` | Identity + JWT config — line 273: `AddIdentity<ApplicationUser, IdentityRole>()` |
| `API/Middleware/JwtCookieAuthMiddleware.cs` | JWT cookie middleware |
| `UI/src/app/app.routes.ts` | Frontend routes — add `/admin` routes |
| `UI/src/app/guards/app.guard.ts` | App guard pattern — reference for creating AdminGuard |
| `UI/src/app/services/auth.service.ts` | Auth service — already treats `/admin` as protected path |
| `UI/src/app/services/base-http.service.ts` | Base HTTP service — use for admin API calls |
| `API.Tests/Controllers/CreditControllerPaymentConfigTests.cs` | Test pattern reference — xUnit + Moq |

### Technical Decisions

**Admin Role Strategy (Verified):**
- ASP.NET Identity Role infrastructure already registered (`IdentityRole` in `Program.cs`)
- Need: EF Core migration to seed "Admin" role in `AspNetRoles` table
- Need: Mechanism to assign Admin role to user(s) — migration or CLI command
- Pattern: `[Authorize(Roles = "Admin")]` — already used in StyleController

**JWT Role Claims Fix (Critical — Low-Risk Approach):**
- Instead of making `GenerateJwtToken` async (breaking change), keep it synchronous and add an `IList<string> roles` parameter
- Each caller (`RegisterAsync`, `LoginAsync`, `ProcessExternalLoginAsync`) already has access to `_userManager` and is already async — they fetch roles and pass them in
- Signature change: `GenerateJwtToken(ApplicationUser user)` → `GenerateJwtToken(ApplicationUser user, IList<string> roles)`
- Inside the method, add `authClaims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));`
- This avoids cascading async changes and keeps the risk surface minimal

**Account Deactivation Strategy:**
- Use Identity's built-in `LockoutEnd` field — set to `DateTimeOffset.MaxValue` for permanent lockout
- No schema migration needed — field already exists in `AspNetUsers`
- `UserManager.SetLockoutEndDateAsync()` handles this cleanly

**Account Deletion Strategy:**
- Soft delete: deactivate via lockout (reversible)
- Hard delete: cascade delete `UserProfile`, `ProcessedImages`, `CreditPurchases`, etc.
- Admin chooses between deactivate/delete in UI
- **Safety rule:** Prevent deletion of the last remaining Admin user

**Credit Adjustment Strategy:**
- Modify `UserProfile.Credits` directly
- **Wrap in explicit database transaction** to ensure atomicity with audit log write
- Create `AdminAuditLog` entry for every adjustment
- No new `AdminCreditAdjustment` entity needed — audit log covers it

**Coupon System Strategy:**
- New `Coupon` entity: code, type (percentage/fixed), value, maxUsages, currentUsages, expiresAt, isActive
- New `CouponRedemption` entity: couponId, userId, redeemedAt, discountApplied
- **Two-phase redemption flow:** validate coupon → create Stripe payment intent with discounted amount → redeem coupon ONLY on webhook payment success (not at intent creation)
- "First N users" = `maxUsages` field — configurable per coupon
- **Validation rule:** If `DiscountType` is `Percentage` and `DiscountValue` is 100 (free purchase), require admin confirmation on creation and log a warning in audit log. Allow it but flag it.

**Audit Logging Strategy:**
- New `AdminAuditLog` entity: adminUserId, action (enum), targetUserId, details (JSON string), oldValue, newValue, createdAt
- Logged on: user deactivate/delete, credit add/subtract, coupon create/update/delete

## Implementation Plan

### Tasks

#### Phase 1: Foundation — Role Infrastructure & JWT Fix (CRITICAL PREREQUISITE)

- [x] Task 1: Add roles parameter to JWT token generation
  - File: `AI.ProfilePhotoMaker.API/Services/Authentication/AuthService.cs`
  - Action: In `GenerateJwtToken()` method (around line 131):
    1. Add `IList<string> roles` parameter — new signature: `public (string Token, DateTime Expiration) GenerateJwtToken(ApplicationUser user, IList<string> roles)`
    2. After the existing `authClaims` list (line 143), add: `authClaims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));`
    3. Add `using System.Linq;` if not already present
  - Notes: Method stays synchronous — no cascading async changes. The `roles` parameter is fetched by each caller.

- [x] Task 2: Update IAuthService interface for roles parameter
  - File: `AI.ProfilePhotoMaker.API/Services/Authentication/interfaces/IAuthService.cs`
  - Action: Change `(string Token, DateTime Expiration) GenerateJwtToken(ApplicationUser user)` to `(string Token, DateTime Expiration) GenerateJwtToken(ApplicationUser user, IList<string> roles)`
  - Notes: Interface stays synchronous — no breaking change to callers' async behavior.

- [x] Task 3: Update all callers of GenerateJwtToken to fetch and pass roles
  - File: `AI.ProfilePhotoMaker.API/Services/Authentication/AuthService.cs`
  - Action: Update every call site to fetch roles first and pass them:
    1. `RegisterAsync` (around line 108):
       ```csharp
       var roles = await _userManager.GetRolesAsync(user);
       var token = GenerateJwtToken(user, roles);
       ```
    2. `LoginAsync` (around line 126):
       ```csharp
       var roles = await _userManager.GetRolesAsync(user);
       var token = GenerateJwtToken(user, roles);
       ```
    3. `ProcessExternalLoginAsync` — multiple call sites for existing and new users:
       ```csharp
       var roles = await _userManager.GetRolesAsync(user);
       var token = GenerateJwtToken(user, roles);
       ```
  - Notes: Search the entire file for `GenerateJwtToken(` to find ALL call sites. Each caller is already async so adding `await _userManager.GetRolesAsync()` is safe.

- [x] Task 4: Create EF Core migration to seed Admin role
  - File: `AI.ProfilePhotoMaker.API/Data/SeedAdminRole.cs` (NEW)
  - Action: Create a data seeding class that:
    1. Seeds "Admin" role into `AspNetRoles` table using `RoleManager<IdentityRole>.CreateAsync()`
    2. Assigns Admin role to a configurable user email (read from `appsettings.json` key `AdminSettings:InitialAdminEmail`)
  - Notes: Call this from `Program.cs` after `app.UseDatabaseMigrationAsync()`. Use `IServiceScope` to resolve `RoleManager<IdentityRole>` and `UserManager<ApplicationUser>`. Only seed if role doesn't exist. Only assign if user exists and doesn't already have role.

- [x] Task 5: Register admin seeding in Program.cs
  - File: `AI.ProfilePhotoMaker.API/Program.cs`
  - Action: After the `app.UseDatabaseMigrationAsync()` block (around line 677), add a call to the admin role seeding logic. Add configuration section for `AdminSettings:InitialAdminEmail` in `appsettings.json` and `appsettings.Development.json`.
  - Notes: Wrap in try/catch with logging — seeding failure should not prevent app startup.

#### Phase 2: Backend Models & Database

- [x] Task 6: Create AdminAuditLog entity
  - File: `AI.ProfilePhotoMaker.API/Models/AdminAuditLog.cs` (NEW)
  - Action: Create entity with properties:
    - `Id` (int, PK)
    - `AdminUserId` (string, required, FK to AspNetUsers)
    - `Action` (string, required — e.g., "UserDeactivated", "UserDeleted", "CreditsAdded", "CreditsSubtracted", "CouponCreated", "CouponUpdated", "CouponDeleted")
    - `TargetUserId` (string, nullable — null for coupon operations)
    - `Details` (string, nullable — JSON string with additional context)
    - `OldValue` (string, nullable)
    - `NewValue` (string, nullable)
    - `CreatedAt` (DateTime, default UTC now)
  - Notes: Follow existing model patterns (see `CreditPurchase.cs`). Index on `AdminUserId` and `CreatedAt` for efficient querying.

- [x] Task 7: Create Coupon entity
  - File: `AI.ProfilePhotoMaker.API/Models/Coupon.cs` (NEW)
  - Action: Create entity with properties:
    - `Id` (int, PK)
    - `Code` (string, required, max 50, unique index)
    - `DiscountType` (enum: `Percentage`, `FixedAmount`)
    - `DiscountValue` (decimal, required — percentage value 0.01-100 or fixed dollar amount > 0)
    - `MaxUsages` (int, required — "first N users" limit)
    - `CurrentUsages` (int, default 0)
    - `ExpiresAt` (DateTime, nullable — null means no expiry)
    - `IsActive` (bool, default true)
    - `CreatedAt` (DateTime, default UTC now)
    - `UpdatedAt` (DateTime, nullable)
    - `CreatedByAdminId` (string, required, FK to AspNetUsers)
    - Navigation: `ICollection<CouponRedemption> Redemptions`
  - Notes: Also create `DiscountType` enum in same file or separate `Enums` file. Percentage value of 100 is allowed but should be flagged during creation (see Task 15 — admin warning).

- [x] Task 8: Create CouponRedemption entity
  - File: `AI.ProfilePhotoMaker.API/Models/CouponRedemption.cs` (NEW)
  - Action: Create entity with properties:
    - `Id` (int, PK)
    - `CouponId` (int, required, FK to Coupons)
    - `UserId` (string, required, FK to AspNetUsers)
    - `RedeemedAt` (DateTime, default UTC now)
    - `DiscountApplied` (decimal — actual discount amount applied)
    - `OriginalPrice` (decimal — price before discount)
    - `FinalPrice` (decimal — price after discount)
    - Navigation: `Coupon Coupon`
  - Notes: Unique composite index on `(CouponId, UserId)` to prevent duplicate redemptions per user.

- [x] Task 9: Register new entities in ApplicationDbContext
  - File: `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs`
  - Action:
    1. Add `public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }`
    2. Add `public DbSet<Coupon> Coupons { get; set; }`
    3. Add `public DbSet<CouponRedemption> CouponRedemptions { get; set; }`
    4. In `OnModelCreating`, configure:
       - `AdminAuditLog`: index on `(AdminUserId, CreatedAt)`
       - `Coupon`: unique index on `Code`
       - `CouponRedemption`: unique composite index on `(CouponId, UserId)`
  - Notes: Follow existing `OnModelCreating` patterns in the file.

- [x] Task 10: Create EF Core migration for new entities
  - Command: `dotnet ef migrations add AddAdminPanelEntities --project AI.ProfilePhotoMaker.API`
  - Action: Run after Tasks 6-9 are complete. Verify the generated migration creates:
    - `AdminAuditLogs` table
    - `Coupons` table
    - `CouponRedemptions` table
    - All indexes and foreign keys
  - Notes: Review generated migration before applying. Ensure no unintended changes to existing tables.

#### Phase 3: Backend DTOs

- [x] Task 11: Create Admin DTOs
  - File: `AI.ProfilePhotoMaker.API/Models/DTOs/AdminDtos.cs` (NEW)
  - Action: Create the following DTOs:

  ```csharp
  // User Management
  public class AdminUserListDto
  {
      public string Id { get; set; }
      public string Email { get; set; }
      public string FirstName { get; set; }
      public string LastName { get; set; }
      public int Credits { get; set; }
      public bool IsLockedOut { get; set; }
      public DateTime CreatedAt { get; set; }
      public DateTime? LastLoginAt { get; set; }
  }

  public class AdminUserDetailDto : AdminUserListDto
  {
      public string Gender { get; set; }
      public string Ethnicity { get; set; }
      public string SubscriptionTier { get; set; }
      public bool EmailConfirmed { get; set; }
      public List<string> Roles { get; set; }
  }

  // Credit Management
  public class AdminCreditAdjustmentDto
  {
      [Required]
      public string UserId { get; set; }
      [Required]
      public int Amount { get; set; } // Positive to add, negative to subtract
      [Required]
      [MaxLength(500)]
      public string Reason { get; set; }
  }

  // Coupon Management
  public class CouponCreateDto
  {
      [Required] [MaxLength(50)]
      public string Code { get; set; }
      [Required]
      public DiscountType DiscountType { get; set; }
      [Required] [Range(0.01, 100)]
      public decimal DiscountValue { get; set; } // For FixedAmount type, 100 is max $100 discount
      [Required] [Range(1, int.MaxValue)]
      public int MaxUsages { get; set; }
      public DateTime? ExpiresAt { get; set; }
  }

  public class CouponUpdateDto
  {
      public int? MaxUsages { get; set; }
      public DateTime? ExpiresAt { get; set; }
      public bool? IsActive { get; set; }
  }

  public class CouponListDto
  {
      public int Id { get; set; }
      public string Code { get; set; }
      public string DiscountType { get; set; }
      public decimal DiscountValue { get; set; }
      public int MaxUsages { get; set; }
      public int CurrentUsages { get; set; }
      public DateTime? ExpiresAt { get; set; }
      public bool IsActive { get; set; }
      public bool IsExpired { get; set; }
      public DateTime CreatedAt { get; set; }
  }

  // Audit Log
  public class AdminAuditLogDto
  {
      public int Id { get; set; }
      public string AdminEmail { get; set; }
      public string Action { get; set; }
      public string TargetUserEmail { get; set; }
      public string Details { get; set; }
      public string OldValue { get; set; }
      public string NewValue { get; set; }
      public DateTime CreatedAt { get; set; }
  }
  ```
  - Notes: Follow existing DTO patterns. Add `[Required]` and validation attributes matching existing conventions. For `CouponCreateDto`, the `[Range(0.01, 100)]` on `DiscountValue` applies to both types — for `FixedAmount`, this means max $100 discount per coupon.

#### Phase 4: Backend Service Layer

- [x] Task 12: Create IAdminService interface
  - File: `AI.ProfilePhotoMaker.API/Services/IAdminService.cs` (NEW)
  - Action: Define interface:
    ```csharp
    public interface IAdminService
    {
        // User Management
        Task<(List<AdminUserListDto> Users, int TotalCount)> GetUsersAsync(int page, int pageSize, string? searchTerm);
        Task<AdminUserDetailDto?> GetUserDetailAsync(string userId);
        Task<bool> DeactivateUserAsync(string userId, string adminUserId, string reason);
        Task<bool> ReactivateUserAsync(string userId, string adminUserId, string reason);
        Task<(bool Success, string Message)> DeleteUserAsync(string userId, string adminUserId, string reason);

        // Credit Management
        Task<(bool Success, string Message, int NewBalance)> AdjustCreditsAsync(AdminCreditAdjustmentDto dto, string adminUserId);

        // Coupon Management
        Task<List<CouponListDto>> GetCouponsAsync();
        Task<(Coupon? Coupon, string? Warning)> CreateCouponAsync(CouponCreateDto dto, string adminUserId);
        Task<bool> UpdateCouponAsync(int couponId, CouponUpdateDto dto, string adminUserId);
        Task<bool> DeleteCouponAsync(int couponId, string adminUserId);

        // Audit Log
        Task<(List<AdminAuditLogDto> Logs, int TotalCount)> GetAuditLogsAsync(int page, int pageSize, string? actionFilter);

        // Dashboard
        Task<AdminDashboardDto> GetDashboardAsync();
    }
    ```

- [x] Task 13: Implement AdminService
  - File: `AI.ProfilePhotoMaker.API/Services/AdminService.cs` (NEW)
  - Action: Implement `IAdminService` with constructor dependencies:
    - `ApplicationDbContext _context`
    - `UserManager<ApplicationUser> _userManager`
    - `ILogger<AdminService> _logger`
  - **User Management implementation:**
    - `GetUsersAsync`: Join `AspNetUsers` with `UserProfiles`, support search by email/name, paginate
    - `GetUserDetailAsync`: Full user + profile + roles via `_userManager.GetRolesAsync()`
    - `DeactivateUserAsync`: Reject if `userId == adminUserId` (self-deactivation). Call `_userManager.SetLockoutEnabledAsync(user, true)` then `_userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)`. Write `AdminAuditLog`.
    - `ReactivateUserAsync`: Call `_userManager.SetLockoutEndDateAsync(user, null)`. Write `AdminAuditLog`.
    - `DeleteUserAsync`:
      - Reject if `userId == adminUserId` (self-deletion)
      - **Prevent deletion of last admin:** Query `_userManager.GetUsersInRoleAsync("Admin")` — if count is 1 and the target user is that admin, reject with message "Cannot delete the last admin user"
      - Delete in order within explicit `IDbContextTransaction`: CouponRedemptions, CreditPurchases, ProcessedImages (and associated blob storage), UserProfile, then `_userManager.DeleteAsync(user)`. Write `AdminAuditLog`.
      - Rollback on any failure.
  - **Credit Management implementation:**
    - `AdjustCreditsAsync`: Wrap in explicit `IDbContextTransaction`. Load `UserProfile` by userId, validate new balance >= 0 (prevent negative), update `Credits`, write `AdminAuditLog` with old/new values, commit. Rollback on failure.
  - **Coupon Management implementation:**
    - `CreateCouponAsync`: Validate code uniqueness. If `DiscountType == Percentage && DiscountValue == 100`, return coupon with warning string "100% discount coupon created — this allows free purchases". Write `AdminAuditLog` with warning in details.
    - `UpdateCouponAsync`: Load coupon, apply updates, write `AdminAuditLog`.
    - `DeleteCouponAsync`: Soft delete (set `IsActive = false`), write `AdminAuditLog`.
    - `GetCouponsAsync`: Return all coupons with computed `IsExpired` flag.
  - **Audit Log implementation:**
    - `GetAuditLogsAsync`: Paginated query with optional action filter, join with AspNetUsers for admin/target emails.
  - Notes: All mutating operations must write to `AdminAuditLog`. Use `LoggingSanitizer` for log messages. Use explicit `IDbContextTransaction` for credit adjustments, coupon redemptions, and user deletion.

- [x] Task 14: Register AdminService in DI
  - File: `AI.ProfilePhotoMaker.API/Program.cs`
  - Action: Add `builder.Services.AddScoped<IAdminService, AdminService>();` near the other service registrations (around line 514).
  - Notes: Follow existing registration pattern.

#### Phase 5: Backend Controller

- [x] Task 15: Refactor and expand AdminController
  - File: `AI.ProfilePhotoMaker.API/Controllers/AdminController.cs`
  - Action: Major refactor:
    1. Change base class from `ControllerBase` to `BaseController`
    2. Add `[Authorize(Roles = "Admin")]` at class level
    3. Inject `IAdminService` via constructor
    4. Keep existing orphan cleanup endpoint
    5. Add new endpoints:

    **User Management:**
    - `GET /api/admin/users?page=1&pageSize=20&search=` — paginated user list
    - `GET /api/admin/users/{userId}` — user detail
    - `POST /api/admin/users/{userId}/deactivate` — deactivate account (body: `{ reason: string }`)
    - `POST /api/admin/users/{userId}/reactivate` — reactivate account (body: `{ reason: string }`)
    - `DELETE /api/admin/users/{userId}` — hard delete account (body: `{ reason: string }`)

    **Credit Management:**
    - `POST /api/admin/credits/adjust` — add/subtract credits (body: `AdminCreditAdjustmentDto`)

    **Coupon Management:**
    - `GET /api/admin/coupons` — list all coupons
    - `POST /api/admin/coupons` — create coupon (body: `CouponCreateDto`). If service returns a warning (e.g., 100% discount), include it in the response: `{ success: true, data: coupon, warning: "..." }`
    - `PUT /api/admin/coupons/{id}` — update coupon (body: `CouponUpdateDto`)
    - `DELETE /api/admin/coupons/{id}` — soft delete coupon

    **Audit Log:**
    - `GET /api/admin/audit-logs?page=1&pageSize=50&action=` — paginated audit logs

    **Dashboard:**
    - `GET /api/admin/dashboard` — summary stats (total users, active users, total credits issued, active coupons)

  - Notes: Use `BaseController` helpers: `GetCurrentUserId()` for admin user ID, `SuccessResponse()` / `ErrorResponse()` for consistent responses. Follow existing pattern in `CreditController`.

#### Phase 6: Coupon Redemption Integration

- [x] Task 16: Create coupon validation and redemption service
  - File: `AI.ProfilePhotoMaker.API/Services/ICouponService.cs` (NEW)
  - File: `AI.ProfilePhotoMaker.API/Services/CouponService.cs` (NEW)
  - Action: Create service for coupon validation and redemption at purchase time:
    ```csharp
    public interface ICouponService
    {
        Task<(bool IsValid, string Message, decimal DiscountAmount)> ValidateCouponAsync(string code, string userId, decimal originalPrice);
        Task<bool> RedeemCouponAsync(string code, string userId, decimal originalPrice, decimal discountApplied);
    }
    ```
  - **ValidateCouponAsync**: Check coupon exists, is active, not expired, not at max usages, user hasn't already redeemed it. Calculate discount amount based on type (percentage or fixed). For percentage: `discountAmount = originalPrice * (discountValue / 100)`. For fixed: `discountAmount = min(discountValue, originalPrice)`.
  - **RedeemCouponAsync**: Wrap in `IDbContextTransaction`. Increment `CurrentUsages`, create `CouponRedemption` record. Commit. Rollback on failure.
  - Notes: Register in `Program.cs` as `AddScoped<ICouponService, CouponService>()`.

- [x] Task 17: Integrate coupon validation into credit purchase flow (two-phase)
  - File: `AI.ProfilePhotoMaker.API/Controllers/CreditController.cs`
  - Action: Implement two-phase coupon redemption:
    1. Add optional `couponCode` parameter to the purchase endpoint
    2. **Phase 1 — Validation:** If `couponCode` provided, call `ICouponService.ValidateCouponAsync()` before creating Stripe payment intent. Apply discount to the Stripe amount. Store validated coupon info in the session/payment metadata.
    3. **Phase 2 — Redemption:** In the Stripe webhook handler (`StripeWebhookService` or equivalent), on `payment_intent.succeeded`, call `ICouponService.RedeemCouponAsync()` to finalize the redemption. This ensures coupon is only consumed on actual payment success.
    4. If payment fails or is cancelled, the coupon is NOT redeemed — user can try again.
  - Notes: This modifies an existing endpoint — be careful to maintain backward compatibility. The `couponCode` parameter should be optional. For payment simulation mode (development), redeem immediately after simulated success.

- [x] Task 17b: Add coupon code input to frontend purchase/checkout UI
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/premium/premium.component.ts` (or wherever the credit purchase UI lives)
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/premium/premium.component.html`
  - Action:
    1. Add an optional "Have a coupon code?" expandable input field to the purchase form
    2. On coupon entry, call a new `validateCoupon(code)` method on the frontend credit service that hits a new `POST /api/credits/validate-coupon` endpoint
    3. Show the discount preview (original price, discount amount, final price) before the user confirms purchase
    4. Pass the `couponCode` to the existing purchase API call
  - Notes: This is a user-facing UI change. Keep it unobtrusive — collapsed by default, expandable on click. Show green "Coupon applied!" or red "Invalid coupon" feedback.

- [x] Task 17c: Add coupon validation endpoint for user-facing preview
  - File: `AI.ProfilePhotoMaker.API/Controllers/CreditController.cs`
  - Action: Add endpoint:
    ```csharp
    [HttpPost("validate-coupon")]
    [Authorize]
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponDto dto)
    {
        var userId = GetCurrentUserId();
        var (isValid, message, discountAmount) = await _couponService.ValidateCouponAsync(dto.Code, userId, dto.OriginalPrice);
        return Ok(new { success = true, data = new { isValid, message, discountAmount, finalPrice = dto.OriginalPrice - discountAmount } });
    }
    ```
  - Notes: This is a read-only validation — does not redeem the coupon. Requires `[Authorize]` (any authenticated user, not just admin).

#### Phase 7: Frontend — Admin Guard & Routing

- [x] Task 18: Add role parsing to Angular AuthService
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/auth.service.ts`
  - Action:
    1. Add `private _rolesSubject = new BehaviorSubject<string[]>([]);` and `public roles$ = this._rolesSubject.asObservable();`
    2. Add `public isAdmin$ = this.roles$.pipe(map(roles => roles.includes('Admin')));`
    3. Add `public isAdmin(): boolean` method that checks `_rolesSubject.value.includes('Admin')`
    4. In `hydrateUserFromProfile()`, after fetching profile, also call `GET /api/auth/user-roles` and update `_rolesSubject`
    5. Cache roles — only fetch once per session (clear on logout in `clearAllAuthData()`)
  - Notes: Since JWT is in HttpOnly cookie, the frontend cannot parse it directly. The `user-roles` endpoint provides roles to the UI.

- [x] Task 19: Add user-roles endpoint to AuthController
  - File: `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`
  - Action: Add endpoint:
    ```csharp
    [HttpGet("user-roles")]
    [Authorize]
    public async Task<IActionResult> GetUserRoles()
    {
        var userId = GetCurrentUserId();
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new { success = true, data = new { roles } });
    }
    ```
  - Notes: This is a lightweight endpoint. Cache the result on the frontend to avoid repeated calls.

- [x] Task 20: Create AdminGuard
  - File: `AI.ProfilePhotoMaker.UI/src/app/guards/admin.guard.ts` (NEW)
  - Action: Create guard that:
    1. Checks authentication (similar to `AppGuard`)
    2. Calls `AuthService.isAdmin()` or checks roles from `AuthService.roles$`
    3. If not admin, redirect to `/app/enhance` with a "not authorized" message
    4. If not authenticated, redirect to `/auth/login`
  - Notes: Follow `AppGuard` pattern for structure. Simpler than AppGuard — no need for profile completion or email verification checks (those should already be done).

- [x] Task 21: Add admin routes to app.routes.ts
  - File: `AI.ProfilePhotoMaker.UI/src/app/app.routes.ts`
  - Action: Add before the `// 404 and Wildcard` section:
    ```typescript
    // Admin Routes (protected by AdminGuard)
    {
      path: 'admin',
      canActivate: [AdminGuard],
      canActivateChild: [AdminGuard],
      children: [
        { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
        {
          path: 'dashboard',
          loadComponent: () => import('./admin/admin-dashboard/admin-dashboard.component').then(m => m.AdminPhotoWorkspaceComponent),
          title: 'Admin Dashboard',
        },
        {
          path: 'users',
          loadComponent: () => import('./admin/admin-users/admin-users.component').then(m => m.AdminUsersComponent),
          title: 'User Management',
        },
        {
          path: 'users/:userId',
          loadComponent: () => import('./admin/admin-user-detail/admin-user-detail.component').then(m => m.AdminUserDetailComponent),
          title: 'User Detail',
        },
        {
          path: 'coupons',
          loadComponent: () => import('./admin/admin-coupons/admin-coupons.component').then(m => m.AdminCouponsComponent),
          title: 'Coupon Management',
        },
        {
          path: 'audit-log',
          loadComponent: () => import('./admin/admin-audit-log/admin-audit-log.component').then(m => m.AdminAuditLogComponent),
          title: 'Audit Log',
        },
      ],
    },
    ```
  - Notes: Uses lazy loading via `loadComponent` consistent with existing patterns.

#### Phase 8: Frontend — Admin Service & Components

- [x] Task 22: Create AdminService (frontend)
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/admin.service.ts` (NEW)
  - Action: Create service extending `BaseHttpService` with methods:
    - `getUsers(page, pageSize, search)` → `GET /api/admin/users`
    - `getUserDetail(userId)` → `GET /api/admin/users/{userId}`
    - `deactivateUser(userId, reason)` → `POST /api/admin/users/{userId}/deactivate`
    - `reactivateUser(userId, reason)` → `POST /api/admin/users/{userId}/reactivate`
    - `deleteUser(userId, reason)` → `DELETE /api/admin/users/{userId}`
    - `adjustCredits(dto)` → `POST /api/admin/credits/adjust`
    - `getCoupons()` → `GET /api/admin/coupons`
    - `createCoupon(dto)` → `POST /api/admin/coupons`
    - `updateCoupon(id, dto)` → `PUT /api/admin/coupons/{id}`
    - `deleteCoupon(id)` → `DELETE /api/admin/coupons/{id}`
    - `getAuditLogs(page, pageSize, action)` → `GET /api/admin/audit-logs`
    - `getDashboard()` → `GET /api/admin/dashboard`
  - Notes: Extend `BaseHttpService` for consistent error handling. All requests use `withCredentials: true` for cookie auth.

- [x] Task 23: Create Admin Photo Workspace component
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-dashboard/admin-dashboard.component.ts` (NEW)
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-dashboard/admin-dashboard.component.html` (NEW)
  - Action: Create standalone component displaying:
    - Summary cards: Total Users, Active Users, Total Credits Issued, Active Coupons
    - Quick action links to User Management, Coupon Management, Audit Log
  - Notes: Standalone component with `imports: [CommonModule]`. Use `AdminService.getDashboard()` for data.

- [x] Task 24: Create Admin Users component
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-users/admin-users.component.ts` (NEW)
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-users/admin-users.component.html` (NEW)
  - Action: Create standalone component with:
    - Search input for filtering by email/name
    - Paginated table: Email, Name, Credits, Status (active/locked), Created Date
    - Row actions: View Detail, Deactivate/Reactivate, Delete (with confirmation dialog)
    - Credit adjustment: inline form with Amount and Reason fields
  - Notes: Use Angular reactive forms. Confirmation dialogs for destructive actions. Show toast/notification on success.

- [x] Task 25: Create Admin User Detail component
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.ts` (NEW)
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-user-detail/admin-user-detail.component.html` (NEW)
  - Action: Create standalone component showing:
    - Full user profile info (email, name, gender, ethnicity, subscription tier, email confirmed status)
    - Current credit balance with add/subtract controls
    - Account status with deactivate/reactivate/delete actions
    - Roles list
  - Notes: Navigate here from user list via `routerLink`. Use `ActivatedRoute` to get `userId` param.

- [x] Task 26: Create Admin Coupons component
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-coupons/admin-coupons.component.ts` (NEW)
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-coupons/admin-coupons.component.html` (NEW)
  - Action: Create standalone component with:
    - Coupons table: Code, Type, Value, Usage (current/max), Expires, Status, Actions
    - Create coupon form: Code, Type dropdown (Percentage/Fixed), Value, Max Usages, Expiry Date
    - **Warning display:** If creating a 100% percentage coupon, show a yellow warning banner: "This coupon allows free purchases. Proceed?"
    - Edit coupon: inline or modal — can only modify MaxUsages, ExpiresAt, IsActive
    - Delete coupon (soft delete with confirmation)
  - Notes: Use reactive forms with validation matching backend DTO constraints. Show visual badge for expired/depleted coupons.

- [x] Task 27: Create Admin Audit Log component
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-audit-log/admin-audit-log.component.ts` (NEW)
  - File: `AI.ProfilePhotoMaker.UI/src/app/admin/admin-audit-log/admin-audit-log.component.html` (NEW)
  - Action: Create standalone component with:
    - Paginated table: Date, Admin, Action, Target User, Old Value, New Value, Details
    - Filter dropdown by action type
    - Read-only view — no edit/delete actions
  - Notes: Format dates consistently. Truncate long detail strings with expand-on-click.

#### Phase 9: Testing

- [x] Task 28: Create AdminController tests
  - File: `AI.ProfilePhotoMaker.API.Tests/Controllers/AdminControllerTests.cs` (NEW)
  - Action: Create xUnit test class with Moq following pattern from `CreditControllerPaymentConfigTests.cs`:
    - Mock `IAdminService`, `ILogger<AdminController>`
    - Test each endpoint:
      - `GetUsers_ReturnsOk_WithPaginatedUsers`
      - `GetUserDetail_ReturnsNotFound_WhenUserDoesNotExist`
      - `DeactivateUser_ReturnsOk_WhenSuccessful`
      - `DeactivateUser_ReturnsBadRequest_WhenSelfDeactivation`
      - `DeleteUser_ReturnsBadRequest_WhenLastAdmin`
      - `AdjustCredits_ReturnsOk_WhenSuccessful`
      - `AdjustCredits_ReturnsBadRequest_WhenWouldGoNegative`
      - `CreateCoupon_ReturnsCreated_WhenValid`
      - `CreateCoupon_ReturnsCreated_WithWarning_When100Percent`
      - `CreateCoupon_ReturnsConflict_WhenCodeExists`
      - `GetAuditLogs_ReturnsOk_WithPaginatedLogs`
  - Notes: Set up admin claims on `HttpContext.User` for `GetCurrentUserId()` to work in tests.

- [x] Task 29: Create AdminService unit tests
  - File: `AI.ProfilePhotoMaker.API.Tests/Services/AdminServiceTests.cs` (NEW)
  - Action: Test service logic:
    - `AdjustCredits_PreventsNegativeBalance`
    - `AdjustCredits_WritesAuditLog`
    - `AdjustCredits_UsesTransaction`
    - `DeactivateUser_SetsLockoutEndToMaxValue`
    - `DeactivateUser_RejectsSelfDeactivation`
    - `ReactivateUser_ClearsLockoutEnd`
    - `DeleteUser_CascadeDeletesRelatedData`
    - `DeleteUser_PreventsLastAdminDeletion`
    - `DeleteUser_RollsBackOnFailure`
  - Notes: Use in-memory database for EF Core tests. Mock `UserManager<ApplicationUser>`.

- [x] Task 30: Create CouponService unit tests
  - File: `AI.ProfilePhotoMaker.API.Tests/Services/CouponServiceTests.cs` (NEW)
  - Action: Test coupon validation logic:
    - `ValidateCoupon_ReturnsInvalid_WhenExpired`
    - `ValidateCoupon_ReturnsInvalid_WhenMaxUsagesReached`
    - `ValidateCoupon_ReturnsInvalid_WhenAlreadyRedeemedByUser`
    - `ValidateCoupon_ReturnsValid_WithCorrectDiscount_Percentage`
    - `ValidateCoupon_ReturnsValid_WithCorrectDiscount_FixedAmount`
    - `ValidateCoupon_ReturnsValid_WithCorrectDiscount_100Percent`
    - `ValidateCoupon_FixedAmount_CapsAtOriginalPrice`
    - `RedeemCoupon_IncrementsCurrentUsages`
    - `RedeemCoupon_CreatesCouponRedemptionRecord`
    - `RedeemCoupon_UsesTransaction`
  - Notes: Use in-memory database. Seed test coupons in test setup.

- [x] Task 31: Create integration test for admin auth pipeline
  - File: `AI.ProfilePhotoMaker.API.Tests/Integration/AdminAuthorizationTests.cs` (NEW)
  - Action: Create integration test using `WebApplicationFactory<Program>` that:
    - Seeds a test user with Admin role
    - Authenticates and obtains JWT with role claim
    - Calls `GET /api/admin/dashboard` — asserts 200 OK
    - Authenticates as non-admin user — calls `GET /api/admin/dashboard` — asserts 403 Forbidden
    - Calls `GET /api/admin/dashboard` without auth — asserts 401 Unauthorized
  - Notes: This is the critical end-to-end test that validates the entire JWT role claim → authorization pipeline. Uses real middleware, real auth, real controller — not mocked. Follow existing integration test patterns in `AI.ProfilePhotoMaker.API.Tests/Integration/`.

- [x] Task 32: Create Angular AdminGuard unit test
  - File: `AI.ProfilePhotoMaker.UI/src/app/guards/admin.guard.spec.ts` (NEW)
  - Action: Create test using Angular `TestBed`:
    - Mock `AuthService` with `isAdmin()` returning `true` — assert guard allows navigation
    - Mock `AuthService` with `isAdmin()` returning `false` — assert guard redirects to `/app/enhance`
    - Mock `AuthService` with `isAuthenticated$` emitting `false` — assert guard redirects to `/auth/login`
  - Notes: Follow standard Angular guard testing patterns with `RouterTestingModule`.

### Acceptance Criteria

#### User Management
- [x] AC 1: Given an authenticated admin user, when they navigate to `/admin/users`, then they see a paginated list of all users with email, name, credits, and status
- [x] AC 2: Given an authenticated admin user, when they search for a user by email, then the user list filters to matching results
- [x] AC 3: Given an authenticated admin user, when they click "Deactivate" on a user, then that user's account is locked (LockoutEnd set to MaxValue) and an audit log entry is created
- [x] AC 4: Given an authenticated admin user, when they click "Reactivate" on a deactivated user, then that user's lockout is cleared and an audit log entry is created
- [x] AC 5: Given an authenticated admin user, when they click "Delete" on a user and confirm, then all user data is cascade deleted and an audit log entry is created
- [x] AC 6: Given an admin tries to deactivate/delete their own account, when the request is processed, then it is rejected with an appropriate error message
- [x] AC 6b: Given there is only one admin user, when any admin tries to delete that user, then the request is rejected with "Cannot delete the last admin user"

#### Credit Management
- [x] AC 7: Given an authenticated admin user viewing a user detail, when they enter a positive amount and reason and submit, then the user's credit balance increases by that amount and an audit log entry is created
- [x] AC 8: Given an authenticated admin user viewing a user detail, when they enter a negative amount that would make the balance negative, then the request is rejected with a "balance cannot go negative" error
- [x] AC 9: Given an authenticated admin user viewing a user detail, when they enter a negative amount within the available balance, then the user's credit balance decreases and an audit log entry is created

#### Coupon Management
- [x] AC 10: Given an authenticated admin user, when they create a coupon with code "SAVE20", type "Percentage", value 20, max usages 100, then the coupon is created and appears in the coupon list
- [x] AC 11: Given an authenticated admin user, when they try to create a coupon with a code that already exists, then the request is rejected with a "code already exists" error
- [x] AC 11b: Given an authenticated admin user, when they create a 100% percentage discount coupon, then the coupon is created but a warning is displayed and logged in the audit trail
- [x] AC 12: Given a regular user at checkout with a valid coupon code, when they apply the coupon, then the price is reduced by the correct amount (percentage or fixed) and a preview is shown before purchase
- [x] AC 13: Given a coupon with max usages of 50 and current usages of 50, when a user tries to apply it, then it is rejected with a "coupon has reached its usage limit" error
- [x] AC 14: Given a coupon with an expiry date in the past, when a user tries to apply it, then it is rejected with a "coupon has expired" error
- [x] AC 15: Given a user who has already redeemed a coupon, when they try to apply the same coupon again, then it is rejected with a "coupon already used" error
- [x] AC 15b: Given a user who validated a coupon but payment failed, when they retry, then the coupon is still available (not consumed by the failed attempt)

#### Authorization & Security
- [x] AC 16: Given a non-authenticated user, when they try to access any `/api/admin/*` endpoint, then they receive a 401 Unauthorized response
- [x] AC 17: Given an authenticated user WITHOUT the Admin role, when they try to access any `/api/admin/*` endpoint, then they receive a 403 Forbidden response
- [x] AC 18: Given an authenticated user WITHOUT the Admin role, when they navigate to `/admin` in the browser, then they are redirected to `/app/enhance`
- [x] AC 19: Given an authenticated admin user, when they log in, then their JWT token includes the "Admin" role claim

#### Audit Logging
- [x] AC 20: Given an authenticated admin user, when they navigate to `/admin/audit-log`, then they see a paginated list of all admin actions with date, admin, action, target, and details
- [x] AC 21: Given an authenticated admin user, when they filter the audit log by action type, then only matching entries are displayed

## Additional Context

### Dependencies

- **ASP.NET Identity** — already registered in `Program.cs` with `IdentityRole` support
- **EF Core Migrations** — for `AdminAuditLog`, `Coupon`, `CouponRedemption` tables
- **Angular Router** — for admin route guard and lazy-loaded components
- **Stripe** — existing integration for coupon discount application at checkout (Task 17)
- **No new NuGet packages or npm packages required**

### Testing Strategy

**Backend Unit Tests (xUnit + Moq):**
- `AdminControllerTests.cs` — controller endpoint tests with mocked service
- `AdminServiceTests.cs` — service logic tests with in-memory DB and mocked UserManager
- `CouponServiceTests.cs` — coupon validation and redemption logic

**Backend Integration Tests:**
- `AdminAuthorizationTests.cs` — end-to-end test of JWT role claim → `[Authorize(Roles = "Admin")]` → controller response pipeline using `WebApplicationFactory<Program>`

**Frontend Unit Tests:**
- `admin.guard.spec.ts` — Angular AdminGuard guard tests with mocked AuthService

**Manual Testing Steps:**
1. Seed Admin role and assign to your user account
2. Log out and log back in — verify JWT now contains Admin role claim
3. Navigate to `/admin` — verify access is granted
4. Test user search, deactivation, reactivation, deletion
5. Test credit add/subtract with edge cases (zero, negative balance)
6. Test coupon CRUD and redemption flow through checkout (including coupon input on purchase UI)
7. Test two-phase coupon flow: validate → pay → verify coupon consumed only on success
8. Verify all actions appear in audit log
9. Log in as non-admin user — verify `/admin` redirects and API returns 403

### Notes

**High-Risk Items:**
- **JWT role parameter change (Tasks 1-3)**: Adding `IList<string> roles` parameter to `GenerateJwtToken` — low risk since method stays synchronous. Each caller fetches roles before calling. Test login/register/OAuth flows thoroughly after this change.
- **User deletion cascade (Task 13)**: Hard delete must cascade correctly within a transaction. Missing a table means orphaned FK references and runtime errors. Investigate all tables with `UserId` FK. Transaction rollback on any failure.
- **Coupon/Stripe two-phase integration (Task 17)**: Coupon is validated at purchase time but only redeemed on webhook payment success. Risk: webhook failure could leave coupon un-redeemed. Mitigation: ensure webhook retry logic handles this gracefully.
- **Last admin protection (Task 13)**: Must query admin count before deletion. Race condition possible if two admins delete each other simultaneously — mitigate with transaction isolation level.

**Known Limitations:**
- Coupon system does not support: stacking multiple coupons, minimum purchase amount, category-specific discounts
- No role management UI — Admin role assignment requires database or CLI seeding
- Audit log is append-only with no export functionality
- 100% discount coupons are allowed but flagged with a warning

**Future Considerations (Out of Scope):**
- Admin role management UI (assign/revoke roles from frontend)
- Bulk operations (batch credit adjustments, bulk coupon generation)
- User activity/usage analytics dashboard
- User generation history view in admin panel (for support scenarios)
- Audit log export (CSV/PDF)
- Multi-tier admin roles (Super Admin, Support, etc.)
