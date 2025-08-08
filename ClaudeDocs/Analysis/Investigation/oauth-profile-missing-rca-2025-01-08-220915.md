---
title: "Root Cause Analysis: OAuth Users Getting 'Profile not found' and 401 Errors"
issue_id: "AUTH-001"
severity: "critical"
status: "complete"
root_cause_categories:
  - "code defect"
  - "missing logic"
investigation_timeline:
  start: "2025-01-08T22:09:15Z"
  end: "2025-01-08T22:15:00Z"
  duration: "5m 45s"
linked_documents:
  - path: "AI.ProfilePhotoMaker.API/Controllers/AuthController.cs"
  - path: "AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs"
  - path: "AI.ProfilePhotoMaker.API/Services/Authentication/AuthService.cs"
evidence_files:
  - type: "code"
    path: "AuthController.FindOrCreateUserAsync"
  - type: "code"
    path: "ProfileController.GetProfile"
prevention_actions:
  - category: "testing"
    priority: "high"
  - category: "code review"
    priority: "medium"
---

# Root Cause Analysis: OAuth Authentication Failures

## Executive Summary

Users authenticating via OAuth (Google login) are experiencing "Profile not found" errors and 401 Unauthorized responses because the OAuth flow creates an ApplicationUser record but **fails to create the corresponding UserProfile record** that is required by the application's data model.

## Problem Statement

- **Symptoms**: OAuth users receive "Profile not found" errors immediately after successful authentication
- **Impact**: Complete authentication failure for OAuth users; they cannot access any protected endpoints
- **Frequency**: 100% of OAuth logins
- **Duration**: Present since OAuth implementation

## Investigation Findings

### 1. Authentication Flow Analysis

#### Regular Registration Flow (Working)
```csharp
// AuthService.RegisterAsync - Line 57-73
var userProfile = new UserProfile
{
    UserId = user.Id,
    FirstName = model.FirstName,
    LastName = model.LastName,
    Gender = model.Gender,
    Ethnicity = model.Ethnicity,
    SubscriptionTier = SubscriptionTier.Basic,
    Credits = 3,
    LastCreditReset = DateTime.UtcNow,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
_context.UserProfiles.Add(userProfile);
await _context.SaveChangesAsync();
```

#### OAuth Flow (BROKEN)
```csharp
// AuthController.FindOrCreateUserAsync - Line 263-293
private async Task<ApplicationUser?> FindOrCreateUserAsync(GoogleUserInfo userInfo)
{
    var user = await _userManager.FindByEmailAsync(userInfo.Email);
    
    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = userInfo.Email,
            Email = userInfo.Email,
            FirstName = userInfo.GivenName ?? "",
            LastName = userInfo.FamilyName ?? "",
            EmailConfirmed = true
        };
        
        var createResult = await _userManager.CreateAsync(user);
        // ❌ MISSING: UserProfile creation!
    }
    
    return user;
}
```

### 2. Database Model Requirements

The application has a **dual-entity model**:
- `ApplicationUser` (ASP.NET Identity) - Authentication/login
- `UserProfile` - Application data, credits, settings

**Critical Relationship**: Every ApplicationUser MUST have a corresponding UserProfile for the application to function.

### 3. Failure Path

1. User clicks "Sign in with Google"
2. OAuth flow completes successfully
3. `FindOrCreateUserAsync` creates ApplicationUser only
4. JWT token is generated and returned
5. User is redirected to dashboard
6. Dashboard calls `/api/profile` endpoint
7. `ProfileController.GetProfile()` queries for UserProfile by UserId
8. **No UserProfile exists** → Returns 404 "Profile not found"
9. Frontend interprets 404 as authentication failure
10. User sees error and cannot proceed

### 4. Evidence from Code

#### ProfileController expects UserProfile to exist:
```csharp
// Line 48-51
var profile = await _userProfileRepository.GetByUserIdAsync(userId);

if (profile == null)
    return NotFound("Profile not found");  // ← This is what OAuth users hit
```

#### CreditController also expects UserProfile:
```csharp
// Line 35-38
var profile = await _basicTierService.GetUserProfileWithCreditsAsync(userId);

if (profile == null)
    return ErrorResponse("ProfileNotFound", "User profile not found.", 404);
```

## Root Cause

**The OAuth user creation flow in `AuthController.FindOrCreateUserAsync` does not create a UserProfile record, violating the application's data model requirement that every ApplicationUser must have a corresponding UserProfile.**

## Impact Analysis

### Affected Components
- All OAuth login attempts
- Profile management endpoints
- Credit system endpoints
- Image generation features
- Model training features

### User Impact
- Complete inability to use application after OAuth login
- Confusing error messages
- Poor user experience
- Potential user abandonment

## Recommended Fixes

### Priority 1: Immediate Fix (Critical)

**Solution**: Create UserProfile during OAuth user creation

```csharp
private async Task<ApplicationUser?> FindOrCreateUserAsync(GoogleUserInfo userInfo)
{
    var user = await _userManager.FindByEmailAsync(userInfo.Email);
    
    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = userInfo.Email,
            Email = userInfo.Email,
            FirstName = userInfo.GivenName ?? "",
            LastName = userInfo.FamilyName ?? "",
            EmailConfirmed = true
        };
        
        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            Console.WriteLine($"❌ User creation failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            return null;
        }
        
        // FIX: Create UserProfile for OAuth users
        var userProfile = new UserProfile
        {
            UserId = user.Id,
            FirstName = userInfo.GivenName ?? "",
            LastName = userInfo.FamilyName ?? "",
            Gender = null,  // Will be set during profile completion
            Ethnicity = null,  // Will be set during profile completion
            SubscriptionTier = SubscriptionTier.Basic,
            Credits = 3,
            LastCreditReset = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.UserProfiles.Add(userProfile);
        await _context.SaveChangesAsync();
        
        Console.WriteLine($"✅ New user and profile created: {user.Email}");
    }
    else
    {
        // Check if existing user has a profile
        var hasProfile = await _context.UserProfiles.AnyAsync(p => p.UserId == user.Id);
        if (!hasProfile)
        {
            // Create profile for existing user (migration case)
            var userProfile = new UserProfile
            {
                UserId = user.Id,
                FirstName = user.FirstName ?? userInfo.GivenName ?? "",
                LastName = user.LastName ?? userInfo.FamilyName ?? "",
                SubscriptionTier = SubscriptionTier.Basic,
                Credits = 3,
                LastCreditReset = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ Profile created for existing user: {user.Email}");
        }
        
        Console.WriteLine($"✅ Existing user found: {user.Email}");
    }
    
    return user;
}
```

### Priority 2: Data Migration (High)

**Solution**: Create UserProfiles for existing OAuth users without profiles

```sql
-- Find OAuth users without profiles
SELECT u.Id, u.Email, u.FirstName, u.LastName 
FROM AspNetUsers u
LEFT JOIN UserProfiles p ON u.Id = p.UserId
WHERE p.Id IS NULL;

-- Create missing profiles
INSERT INTO UserProfiles (UserId, FirstName, LastName, SubscriptionTier, Credits, LastCreditReset, CreatedAt, UpdatedAt)
SELECT u.Id, u.FirstName, u.LastName, 0, 3, GETUTCDATE(), GETUTCDATE(), GETUTCDATE()
FROM AspNetUsers u
LEFT JOIN UserProfiles p ON u.Id = p.UserId
WHERE p.Id IS NULL;
```

### Priority 3: Defensive Programming (Medium)

**Solution**: Add safety checks in critical endpoints

```csharp
// Add to BaseController or as middleware
protected async Task<UserProfile?> GetOrCreateUserProfileAsync(string userId)
{
    var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
    
    if (profile == null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                SubscriptionTier = SubscriptionTier.Basic,
                Credits = 3,
                LastCreditReset = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();
            
            _logger.LogWarning("Created missing UserProfile for user {UserId}", userId);
        }
    }
    
    return profile;
}
```

## Validation Steps

### Test Case 1: New OAuth User
1. Clear browser data/use incognito
2. Click "Sign in with Google"
3. Use a new Google account
4. Complete OAuth flow
5. **Expected**: Dashboard loads successfully
6. **Verify**: `/api/profile` returns 200 with user data
7. **Verify**: `/api/credit/status` returns 200 with credit info

### Test Case 2: Existing OAuth User
1. Sign in with previously used Google account
2. **Expected**: Dashboard loads successfully
3. **Verify**: Profile data persists from previous sessions

### Test Case 3: Mixed Authentication
1. Register with email/password
2. Logout
3. Login with Google using same email
4. **Expected**: Same user account accessed
5. **Verify**: Profile data maintained

### Database Validation
```sql
-- All users should have profiles
SELECT COUNT(*) as UsersWithoutProfiles
FROM AspNetUsers u
LEFT JOIN UserProfiles p ON u.Id = p.UserId
WHERE p.Id IS NULL;
-- Expected: 0
```

## Prevention Measures

### 1. Add Integration Tests
```csharp
[Test]
public async Task OAuthLogin_CreatesUserProfile()
{
    // Arrange
    var googleUserInfo = new GoogleUserInfo 
    { 
        Email = "test@gmail.com",
        GivenName = "Test",
        FamilyName = "User"
    };
    
    // Act
    var user = await _authController.FindOrCreateUserAsync(googleUserInfo);
    
    // Assert
    Assert.NotNull(user);
    var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
    Assert.NotNull(profile);
    Assert.AreEqual("Test", profile.FirstName);
}
```

### 2. Add Database Constraints
```sql
-- Consider adding a trigger or check constraint to ensure UserProfile exists
CREATE TRIGGER EnsureUserProfile
AFTER INSERT ON AspNetUsers
BEGIN
    INSERT INTO UserProfiles (UserId, SubscriptionTier, Credits, LastCreditReset, CreatedAt, UpdatedAt)
    VALUES (NEW.Id, 0, 3, DATETIME('now'), DATETIME('now'), DATETIME('now'));
END;
```

### 3. Monitoring Alerts
- Alert when UserProfile lookup fails for authenticated user
- Track OAuth registration success/failure rates
- Monitor 404 responses on `/api/profile` endpoint

## Lessons Learned

1. **Data Model Consistency**: When using multiple related entities for user data, ensure all creation paths maintain consistency
2. **Testing Coverage**: OAuth flows need dedicated integration tests
3. **Defensive Programming**: Critical endpoints should handle missing data gracefully
4. **Documentation**: Document entity relationships and requirements clearly

## Conclusion

The root cause is a missing UserProfile creation step in the OAuth user registration flow. This is a critical defect that prevents all OAuth users from using the application. The fix is straightforward - ensure UserProfile is created whenever an ApplicationUser is created via OAuth, matching the behavior of the regular registration flow.

**Estimated Fix Time**: 30 minutes
**Testing Time**: 1 hour
**Risk**: Low (additive change only)
**Priority**: CRITICAL - Blocking all OAuth users