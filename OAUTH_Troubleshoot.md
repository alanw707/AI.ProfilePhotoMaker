# OAuth Authentication Troubleshooting Guide

This document outlines the troubleshooting steps and solutions for OAuth authentication issues in the AI.ProfilePhotoMaker application.

## Issue: OAuth Login Shows Username Instead of Full Name

### Problem Description
When users log in via OAuth (Google), the dashboard welcome message displays the username (e.g., "alanw707") instead of the user's full name (e.g., "Alan Wang"). Email login correctly shows the full name.

### Root Cause Analysis

#### 1. JWT Token Structure Investigation
**Problem:** OAuth-generated JWT tokens were missing firstName/lastName claims.

**Debugging Steps:**
1. Added console logging to `auth.service.ts` `extractUserFromToken()` method
2. Logged JWT payload structure during OAuth login
3. Found that JWT only contained:
   - `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name` (username)
   - `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress` (email)
   - Missing: `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname` (firstName)
   - Missing: `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname` (lastName)

**Console Output Example:**
```
JWT Payload for debugging: {
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "alanw707",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "alanw707@gmail.com",
  "jti": "5f376578-7db1-4cef-9238-0a64ce8c2e89",
  "exp": 1750251050,
  "iss": "http://localhost:5035",
  "aud": "http://localhost:5035"
}
```

#### 2. Backend JWT Generation Analysis
**Investigated:** `AuthController.cs` OAuth callback methods and JWT generation in `AuthService.cs`

**Finding:** The backend `GenerateJwtToken()` method correctly adds firstName/lastName claims using:
```csharp
new Claim(ClaimTypes.GivenName, user.FirstName ?? ""),
new Claim(ClaimTypes.Surname, user.LastName ?? "")
```

**Issue:** During OAuth callback processing, the `ApplicationUser` object's `FirstName` and `LastName` properties were not being populated with data from the OAuth provider.

#### 3. Profile API Verification
**Tested:** Profile API endpoint (`/api/profile`) to verify user data availability

**Result:** Profile API correctly returned complete user data:
```json
{
  "id": 2,
  "firstName": "Alan",
  "lastName": "Wang",
  "gender": "Male",
  "ethnicity": "Asian",
  ...
}
```

### Solution Implementation

#### 1. Enhanced JWT Extraction with Fallback
**File:** `src/app/services/auth.service.ts`

**Changes:**
- Modified `extractUserFromToken()` to return `null` when firstName/lastName are missing
- Enhanced `handleOAuthCallback()` to detect incomplete JWT data
- Implemented profile API fallback when JWT lacks complete user information

**Key Code:**
```typescript
private extractUserFromToken(token: string): AuthResponseDto | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    
    const firstName = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'] || '';
    const lastName = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname'] || '';
    
    // If no firstName/lastName in JWT, return null to force profile lookup
    if (!firstName && !lastName) {
      console.log('No firstName/lastName in JWT, returning null to force profile lookup');
      return null;
    }
    
    return {
      token: token,
      email: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '',
      firstName: firstName,
      lastName: lastName
    };
  } catch (error) {
    return null;
  }
}
```

#### 2. Profile API Fallback Implementation
**Implementation:**
```typescript
handleOAuthCallback(token: string, expiration?: string): void {
  if (token) {
    localStorage.setItem(this.TOKEN_KEY, token);
    
    const user = this.extractUserFromToken(token);
    
    if (user && user.firstName && user.lastName) {
      // Complete user data from JWT
      localStorage.setItem('currentUser', JSON.stringify(user));
      this.currentUserSubject.next(user);
      this.isAuthenticatedSubject.next(true);
    } else {
      // Incomplete JWT data, fetch from profile API
      this.isAuthenticatedSubject.next(true);
      this.fetchUserProfileForOAuth(token);
    }
  }
}
```

#### 3. Profile API Integration
**Method:** `fetchUserProfileForOAuth()`
```typescript
private fetchUserProfileForOAuth(token: string): void {
  const payload = JSON.parse(atob(token.split('.')[1]));
  const email = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '';
  
  this.http.get<any>(`${this.config.baseUrl}/profile`).subscribe({
    next: (response) => {
      if (response && (response.firstName || response.lastName)) {
        const completeUser = {
          token: token,
          email: email,
          firstName: response.firstName || '',
          lastName: response.lastName || ''
        };
        
        localStorage.setItem('currentUser', JSON.stringify(completeUser));
        this.currentUserSubject.next(completeUser);
      } else {
        // Fallback to email username
        const fallbackUser = {
          token: token,
          email: email,
          firstName: email.split('@')[0],
          lastName: ''
        };
        localStorage.setItem('currentUser', JSON.stringify(fallbackUser));
        this.currentUserSubject.next(fallbackUser);
      }
    },
    error: (error) => {
      // Fallback to email username on API error
      const fallbackUser = {
        token: token,
        email: email,
        firstName: email.split('@')[0],
        lastName: ''
      };
      localStorage.setItem('currentUser', JSON.stringify(fallbackUser));
      this.currentUserSubject.next(fallbackUser);
    }
  });
}
```

### Testing Steps

1. **Clear browser authentication state**
2. **Login with OAuth (Google)**
3. **Verify console logs show:**
   ```
   Fetching user profile from API for complete user data
   Response firstName: Alan
   Response lastName: Wang
   SUCCESS: Updated user with profile data: {firstName: "Alan", lastName: "Wang", ...}
   ```
4. **Verify dashboard welcome message shows:** "Welcome, Alan Wang!"

### API Endpoint Dependencies

**Required API Endpoint:** `GET /api/profile`
- **Authentication:** Required (JWT token in Authorization header)
- **Response Format:** Direct object with user properties
- **Required Fields:** `firstName`, `lastName`

### Debugging Commands

**View JWT Payload:**
```javascript
// In browser console
const token = localStorage.getItem('auth_token');
const payload = JSON.parse(atob(token.split('.')[1]));
console.log('JWT Payload:', payload);
```

**Check Current User State:**
```javascript
// In browser console
const currentUser = JSON.parse(localStorage.getItem('currentUser'));
console.log('Current User:', currentUser);
```

### Future Improvements

1. **Backend Enhancement:** Fix OAuth callback to properly populate `ApplicationUser.FirstName` and `ApplicationUser.LastName` from OAuth provider data
2. **Error Handling:** Add retry logic for profile API calls
3. **Caching:** Implement profile data caching to reduce API calls
4. **Fallback Strategy:** Consider alternative data sources for user names (e.g., Google OAuth user info endpoint)

### Related Files

- `src/app/services/auth.service.ts` - Authentication service with OAuth handling
- `src/app/dashboard/dashboard.component.ts` - Dashboard user display logic
- `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs` - Backend OAuth processing
- `AI.ProfilePhotoMaker.API/Services/Authentication/AuthService.cs` - JWT generation

### Resolution Status

✅ **RESOLVED** - OAuth login now correctly displays full name by fetching profile data from API when JWT lacks complete user information.