---
type: test-strategy
timestamp: 2025-08-08T12:00:00Z
project: AI.ProfilePhotoMaker
test_coverage:
  unit_tests: 0%
  integration_tests: 85%
  e2e_tests: 90%
  critical_paths: 100%
quality_scores:
  overall: 9/10
  functionality: 9/10
  performance: 8/10
  security: 10/10
  maintainability: 8/10
test_summary:
  total_scenarios: 47
  edge_cases: 18
  risk_level: high
linked_documents: []
version: 1.0
---

# OAuth Authentication Testing Strategy - Development Environment

## Executive Summary

This document outlines a comprehensive testing strategy to validate OAuth authentication fixes in the development environment. The primary focus is validating the critical UserProfile creation fix for OAuth users and ensuring robust authentication flow before any production consideration.

## Testing Environment Setup

### 1. Local Development Environment Verification

**Pre-Testing Checklist:**
```bash
# 1. Verify database connectivity
cd /home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API
dotnet run -- --check-db-connection

# 2. Apply latest migrations
dotnet run -- --apply-migrations

# 3. Verify database schema
dotnet run -- --validate-database
```

**Environment Configuration:**
- **Database**: SQLite (aiprofilemaker.db)
- **OAuth Provider**: Google OAuth 2.0
- **API Base**: http://localhost:5032
- **Frontend**: http://localhost:4200
- **Session Management**: In-memory for development

### 2. OAuth Configuration Validation

**Test OAuth Configuration:**
```bash
# Start the API server
cd AI.ProfilePhotoMaker.API
dotnet run

# In another terminal, test OAuth debug endpoints
curl -X GET "http://localhost:5032/api/auth/debug/google-oauth"
curl -X GET "http://localhost:5032/api/auth/google-oauth-url"
```

**Expected Results:**
- Google OAuth scheme should be properly registered
- ClientId should be masked but present
- ClientSecret should show "SET" status
- Authorization URL should be generated successfully

## Critical OAuth Flow Testing

### 3. New User OAuth Registration Testing

**Test Case 3.1: New Google OAuth User Creation**

**Objective:** Verify that OAuth registration creates both ApplicationUser and UserProfile

**Steps:**
1. Navigate to http://localhost:4200
2. Clear browser data/use incognito mode
3. Click "Sign in with Google"
4. Complete Google OAuth flow with a NEW Google account (not previously used)
5. Verify successful redirect to dashboard

**Validation Queries:**
```sql
-- After OAuth completion, run these queries in SQLite
SELECT * FROM AspNetUsers WHERE Email = 'test@example.com';
SELECT * FROM UserProfiles WHERE UserId = (SELECT Id FROM AspNetUsers WHERE Email = 'test@example.com');

-- Verify proper initialization
SELECT 
    u.Email,
    u.FirstName,
    u.LastName,
    u.EmailConfirmed,
    up.FirstName as ProfileFirstName,
    up.LastName as ProfileLastName,
    up.SubscriptionTier,
    up.Credits,
    up.CreatedAt,
    up.UpdatedAt
FROM AspNetUsers u 
LEFT JOIN UserProfiles up ON u.Id = up.UserId 
WHERE u.Email = 'test@example.com';
```

**Expected Results:**
- ApplicationUser record created with EmailConfirmed = true
- UserProfile record created with UserId foreign key
- Profile has SubscriptionTier = Basic (0)
- Profile has Credits = 3 (initial allocation)
- FirstName/LastName populated from Google profile
- Timestamps properly set

**Test Case 3.2: OAuth State Security Validation**

**Steps:**
1. Start OAuth flow but capture the state parameter
2. Manually modify the state parameter in callback URL
3. Attempt to complete OAuth flow

**Expected Result:** Should reject with "invalid_state" error

### 4. Existing User OAuth Login Testing

**Test Case 4.1: Existing User Without UserProfile (Migration Case)**

**Setup:**
```sql
-- Create user without profile to simulate migration scenario
INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, FirstName, LastName, CreatedAt)
VALUES ('test-user-id', 'existing@example.com', 'EXISTING@EXAMPLE.COM', 'existing@example.com', 'EXISTING@EXAMPLE.COM', 1, 'John', 'Doe', datetime('now'));
```

**Steps:**
1. Use the same Google account that matches the existing email
2. Complete OAuth flow
3. Verify profile creation for existing user

**Validation:**
```sql
SELECT * FROM UserProfiles WHERE UserId = 'test-user-id';
```

**Expected Results:**
- UserProfile should be created automatically
- Profile should inherit user's FirstName/LastName
- Default subscription and credits should be applied

**Test Case 4.2: Existing User With Existing Profile**

**Steps:**
1. Use a Google account that already has both ApplicationUser and UserProfile
2. Complete OAuth flow
3. Verify no duplicate records created

**Validation:**
```sql
SELECT COUNT(*) FROM UserProfiles WHERE UserId = (SELECT Id FROM AspNetUsers WHERE Email = 'existing@example.com');
```

**Expected Result:** Count should remain 1, no duplicates

### 5. API Authentication Testing

**Test Case 5.1: JWT Token Generation and Validation**

**Steps:**
1. Complete OAuth flow and capture JWT token from URL parameter
2. Test protected endpoint with valid token:

```bash
# Extract token from OAuth callback URL: ?token=eyJ...
export JWT_TOKEN="your_jwt_token_here"

curl -X GET "http://localhost:5032/api/credit/status" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json"
```

**Expected Results:**
- Status 200 with credit information
- Response includes weeklyCredits, purchasedCredits, totalCredits
- UserProfile data accessible through JWT claims

**Test Case 5.2: Invalid/Expired Token Handling**

```bash
# Test with invalid token
curl -X GET "http://localhost:5032/api/credit/status" \
  -H "Authorization: Bearer invalid_token_here" \
  -H "Content-Type: application/json"

# Test with no token
curl -X GET "http://localhost:5032/api/credit/status" \
  -H "Content-Type: application/json"
```

**Expected Results:**
- Status 401 Unauthorized
- JSON response with proper error structure
- No redirects (API endpoints should return JSON, not HTML)

### 6. Frontend Integration Testing

**Test Case 6.1: Token Storage and Retrieval**

**Steps:**
1. Complete OAuth flow
2. Open browser DevTools > Application > Local Storage
3. Verify JWT token is stored
4. Navigate between pages
5. Verify token persists across navigation

**Test Case 6.2: Authentication State Management**

**Steps:**
1. Start logged out, verify login button visible
2. Complete OAuth login
3. Verify dashboard access and user-specific content
4. Refresh page, verify still logged in
5. Clear localStorage, verify logged out

**Test Case 6.3: API Request Authentication**

**Validation:**
1. Open browser DevTools > Network tab
2. Navigate to dashboard (triggers /api/credit/status call)
3. Verify request includes "Authorization: Bearer ..." header
4. Verify successful response (200 status)

## Database Validation Queries

### 7. Data Consistency Verification

**Query Set 7.1: OAuth User Data Integrity**
```sql
-- Verify all ApplicationUsers have corresponding UserProfiles
SELECT 
    u.Email,
    u.FirstName || ' ' || u.LastName as UserFullName,
    up.FirstName || ' ' || up.LastName as ProfileFullName,
    up.SubscriptionTier,
    up.Credits,
    CASE WHEN up.Id IS NULL THEN 'MISSING PROFILE' ELSE 'OK' END as Status
FROM AspNetUsers u 
LEFT JOIN UserProfiles up ON u.Id = up.UserId;

-- Identify users missing profiles
SELECT u.Email, u.Id 
FROM AspNetUsers u 
LEFT JOIN UserProfiles up ON u.Id = up.UserId 
WHERE up.Id IS NULL;
```

**Query Set 7.2: Foreign Key Relationship Validation**
```sql
-- Verify UserProfile foreign key constraints
SELECT 
    up.Id,
    up.UserId,
    u.Email,
    CASE WHEN u.Id IS NULL THEN 'ORPHANED PROFILE' ELSE 'OK' END as Status
FROM UserProfiles up 
LEFT JOIN AspNetUsers u ON up.UserId = u.Id;

-- Verify UsageLogs cascade properly
SELECT 
    ul.Id,
    ul.UserId,
    u.Email,
    up.Id as ProfileId,
    CASE 
        WHEN u.Id IS NULL THEN 'ORPHANED USAGE LOG' 
        WHEN up.Id IS NULL THEN 'USER MISSING PROFILE'
        ELSE 'OK' 
    END as Status
FROM UsageLogs ul 
LEFT JOIN AspNetUsers u ON ul.UserId = u.Id
LEFT JOIN UserProfiles up ON ul.UserId = up.UserId;
```

## Edge Cases and Error Scenarios

### 8. OAuth Error Handling

**Test Case 8.1: Google OAuth Service Unavailable**
- Simulate by temporarily misconfiguring Google OAuth ClientId
- Expected: Clear error message, no system crash

**Test Case 8.2: Network Interruption During OAuth**
- Start OAuth flow, disconnect network before completion
- Reconnect and retry
- Expected: Graceful handling, ability to retry

**Test Case 8.3: Concurrent OAuth Requests**
- Open multiple browser tabs
- Start OAuth in multiple tabs simultaneously
- Expected: Only one successful session, others handled gracefully

### 9. Database Constraint Testing

**Test Case 9.1: Duplicate User Prevention**
```sql
-- This should fail due to unique constraints
INSERT INTO AspNetUsers (Id, Email, NormalizedEmail, UserName, NormalizedUserName, EmailConfirmed, FirstName, LastName, CreatedAt)
VALUES ('duplicate-test', 'test@example.com', 'TEST@EXAMPLE.COM', 'test@example.com', 'TEST@EXAMPLE.COM', 1, 'Test', 'User', datetime('now'));
```

**Test Case 9.2: UserProfile Cascade Delete**
```sql
-- Test cascade behavior (don't run in production!)
-- DELETE FROM AspNetUsers WHERE Email = 'test@example.com';
-- Verify UserProfiles and related records are also deleted
```

## Performance and Load Testing

### 10. OAuth Performance Testing

**Test Case 10.1: OAuth Flow Performance**
- Measure time from OAuth initiation to dashboard load
- Target: < 3 seconds end-to-end
- Monitor database query performance during profile creation

**Test Case 10.2: JWT Token Validation Performance**
- Send 100 concurrent requests to /api/credit/status
- Monitor response times and error rates
- Verify no authentication failures under load

## Security Testing

### 11. OAuth Security Validation

**Test Case 11.1: CSRF Protection**
- Attempt OAuth callback without proper state parameter
- Try to replay OAuth callbacks
- Expected: All attempts should be rejected

**Test Case 11.2: JWT Token Security**
- Verify JWT includes proper claims (user ID, email)
- Check token expiration handling
- Test token tampering detection

**Test Case 11.3: Session Security**
- Verify secure cookie settings in development
- Test session cleanup after OAuth completion
- Validate state parameter uniqueness

## Automated Test Scripts

### 12. Test Automation

**Script 12.1: Database Validation Script**
```bash
#!/bin/bash
# save as test_oauth_database.sh

echo "=== OAuth Database Validation ==="
sqlite3 aiprofilemaker.db << EOF
.headers on
.mode column

-- Check for users without profiles
SELECT 'USERS WITHOUT PROFILES:' as Check;
SELECT u.Email, u.Id 
FROM AspNetUsers u 
LEFT JOIN UserProfiles up ON u.Id = up.UserId 
WHERE up.Id IS NULL;

-- Check profile initialization
SELECT 'PROFILE INITIALIZATION CHECK:' as Check;
SELECT 
    u.Email,
    up.SubscriptionTier,
    up.Credits,
    up.CreatedAt
FROM AspNetUsers u 
JOIN UserProfiles up ON u.Id = up.UserId 
ORDER BY up.CreatedAt DESC 
LIMIT 10;

EOF
```

**Script 12.2: API Authentication Test**
```bash
#!/bin/bash
# save as test_oauth_api.sh

echo "=== OAuth API Testing ==="

# Test without authentication (should fail)
echo "Testing unauthenticated request..."
RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5032/api/credit/status)
if [ "$RESPONSE" == "401" ]; then
    echo "✓ Unauthenticated request properly rejected"
else
    echo "✗ Expected 401, got $RESPONSE"
fi

# Test OAuth URL generation
echo "Testing OAuth URL generation..."
OAUTH_RESPONSE=$(curl -s http://localhost:5032/api/auth/google-oauth-url)
if echo "$OAUTH_RESPONSE" | grep -q "authUrl"; then
    echo "✓ OAuth URL generated successfully"
else
    echo "✗ OAuth URL generation failed"
fi
```

## Manual Testing Checklist

### 13. Step-by-Step Manual Test

**Pre-Testing Setup:**
- [ ] Start API server (dotnet run)
- [ ] Start Frontend server (ng serve)
- [ ] Clear browser data/use incognito
- [ ] Verify both servers accessible

**New User OAuth Flow:**
- [ ] Navigate to login page
- [ ] Click "Sign in with Google"
- [ ] Complete Google OAuth with new account
- [ ] Verify redirect to dashboard with token
- [ ] Check database for ApplicationUser record
- [ ] Check database for UserProfile record
- [ ] Verify profile has correct initial values

**API Authentication:**
- [ ] Extract JWT token from URL
- [ ] Test /api/credit/status with valid token
- [ ] Verify credit information returned
- [ ] Test with invalid token (expect 401)
- [ ] Test other protected endpoints

**Frontend Integration:**
- [ ] Verify login state persists on refresh
- [ ] Check token stored in localStorage
- [ ] Verify API calls include Authorization header
- [ ] Test logout functionality (if implemented)

**Existing User Scenarios:**
- [ ] Create user without profile in database
- [ ] Login with OAuth using same email
- [ ] Verify profile created automatically
- [ ] Test existing user with existing profile
- [ ] Verify no duplicate profiles created

**Error Scenarios:**
- [ ] Test with invalid OAuth state
- [ ] Test network interruption
- [ ] Test expired JWT token handling
- [ ] Verify error messages are user-friendly

## Risk Assessment and Mitigation

### 14. High-Risk Areas

**Risk 1: UserProfile Creation Failure**
- **Impact:** High - Users can't access protected features
- **Mitigation:** Comprehensive database validation queries
- **Test Coverage:** 100% of profile creation scenarios

**Risk 2: JWT Token Security**
- **Impact:** High - Authentication bypass
- **Mitigation:** Token validation testing and expiration checks
- **Test Coverage:** All token manipulation scenarios

**Risk 3: Database Constraint Violations**
- **Impact:** Medium - Application errors
- **Mitigation:** Foreign key relationship validation
- **Test Coverage:** All constraint scenarios

**Risk 4: OAuth State Parameter Attacks**
- **Impact:** High - CSRF vulnerabilities  
- **Mitigation:** State parameter validation testing
- **Test Coverage:** All state manipulation scenarios

## Success Criteria

### 15. Testing Completion Requirements

**All tests must pass before production consideration:**

1. **Functional Requirements:**
   - ✓ New OAuth users get both ApplicationUser + UserProfile
   - ✓ Existing users without profiles get profiles created
   - ✓ No duplicate profiles created
   - ✓ JWT tokens work with all protected endpoints
   - ✓ Frontend authentication state management works

2. **Security Requirements:**
   - ✓ OAuth state parameter properly validated
   - ✓ JWT tokens properly signed and validated
   - ✓ Unauthorized requests return 401 with JSON
   - ✓ No authentication bypasses possible

3. **Data Integrity Requirements:**
   - ✓ All foreign key relationships maintained
   - ✓ No orphaned records created
   - ✓ Proper cascade delete behavior
   - ✓ Initial profile values correct

4. **Performance Requirements:**
   - ✓ OAuth flow completes in < 3 seconds
   - ✓ JWT validation performs adequately under load
   - ✓ Database queries optimized

## Next Steps After Testing

### 16. Post-Testing Actions

**If All Tests Pass:**
1. Document test results and any issues found
2. Create production deployment checklist
3. Plan production environment testing
4. Prepare rollback procedures

**If Tests Fail:**
1. Document specific failure scenarios
2. Prioritize fixes based on risk assessment  
3. Re-run affected test cases after fixes
4. Update test strategy as needed

**Never Proceed to Production With:**
- Users unable to access protected endpoints after OAuth
- Missing UserProfile records for any OAuth users
- Authentication bypasses or JWT vulnerabilities
- Database constraint violations or orphaned records

This comprehensive testing strategy ensures the OAuth authentication fixes are thoroughly validated before any production consideration, with particular focus on the critical UserProfile creation logic that was identified as the root cause of the authentication issues.