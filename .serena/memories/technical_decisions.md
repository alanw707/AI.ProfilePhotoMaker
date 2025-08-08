# Technical Decisions - AI Profile Photo Maker

## Authentication Architecture Decisions

### JWT Bearer Challenge Scheme (2025-08-08)
**Decision**: Set `DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme` in Program.cs
**Context**: API endpoints were returning 302 redirects instead of 401 JSON responses for unauthenticated requests
**Rationale**: 
- Ensures consistent API behavior across all authenticated endpoints
- Provides proper HTTP status codes (401) for API consumers
- Maintains clear separation between API and web authentication flows
**Impact**: All protected API endpoints now return standardized JSON error responses
**File**: `AI.ProfilePhotoMaker.API/Program.cs:148`

### OAuth UserProfile Creation Fix (2025-08-08)
**Decision**: Automatically create UserProfile records for OAuth users in FindOrCreateUserAsync()
**Context**: OAuth users were getting ApplicationUser records but missing UserProfile records, causing "Profile not found" errors
**Rationale**: 
- Ensures 1:1 relationship between ApplicationUser and UserProfile entities
- Eliminates "Profile not found" errors for OAuth users
- Maintains data consistency across authentication methods
**Impact**: All OAuth users now get complete profile setup automatically
**File**: `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs:280-330`
**Implementation**:
```csharp
var userProfile = new UserProfile
{
    UserId = user.Id,
    FirstName = userInfo.GivenName ?? "",
    LastName = userInfo.FamilyName ?? "",
    SubscriptionTier = SubscriptionTier.Basic,
    Credits = 3,
    PurchasedCredits = 0,
    LastCreditReset = DateTime.UtcNow,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
```

### OAuth Preservation Strategy (2025-08-08)  
**Decision**: Maintain OAuth controller explicit authentication schemes while setting JWT as default
**Context**: Need to fix API authentication without breaking Google OAuth flow
**Rationale**:
- OAuth controllers can override default scheme for their specific needs
- Preserves existing Google authentication functionality
- Allows API endpoints to have consistent behavior
**Impact**: OAuth flow continues working while API gets proper error handling

## API Controller Architecture Decisions

### StylePreview Controller Recreation (2025-08-08)
**Decision**: Implement StylePreviewController with Azure Blob Storage fallback
**Context**: Frontend depends on `/api/style-preview/list` endpoint that was removed
**Rationale**:
- Maintains backward compatibility with frontend expectations
- Provides fallback to Azure Blob Storage when local previews unavailable
- Follows existing controller patterns and conventions
**Implementation**:
- `GET /api/style-preview/list` - Returns available style previews
- `GET /api/style-preview/url/{styleName}` - Returns storage URLs
**File**: `AI.ProfilePhotoMaker.API/Controllers/StylePreviewController.cs`

### Error Response Standardization (2025-08-08)
**Decision**: Implement consistent JSON error format across all API endpoints
**Format**: 
```json
{
  "success": false,
  "error": {
    "code": "ErrorType",
    "message": "Human readable message"
  }
}
```
**Rationale**: 
- Provides consistent API consumer experience
- Enables better frontend error handling
- Follows REST API best practices
**Impact**: All authentication failures now return structured error responses

## Database Architecture Decisions

### SQLite Development Database (Previous)
**Decision**: Use SQLite for local development
**Context**: Simplified local development without external database dependencies
**Rationale**: Faster developer setup, no external dependencies for development
**Status**: Active and working correctly

### UserProfile Migration Fix (2025-08-08)
**Decision**: Create UserProfiles for existing OAuth users without profiles
**Context**: Database had ApplicationUsers without corresponding UserProfiles from previous OAuth registrations
**Rationale**: 
- Ensures data integrity across existing and new users
- Prevents "Profile not found" errors for existing OAuth users
- Maintains consistent user experience
**Impact**: Database state now consistent: 4 ApplicationUsers with 4 UserProfiles
**Validation**: Confirmed via SQL queries showing complete user-profile relationships

### Azure Blob Storage Integration (Previous)
**Decision**: Use Azure Blob Storage for production image storage with local fallback
**Context**: Scalable cloud storage for user-generated images
**Rationale**: Production scalability, reliability, CDN integration capabilities
**Status**: Active with proper fallback mechanisms

## Port Configuration Decisions

### Standardized Port Allocation (2025-08-08)
**Decision**: UI always on port 4200, API always on port 5032
**Context**: Previous port conflicts and inconsistent configuration
**Rationale**: 
- Eliminates port conflicts in development
- Consistent developer experience
- Matches Angular CLI defaults
**Configuration**:
- Frontend: `localhost:4200` (Angular dev server)
- Backend: `localhost:5032` (ASP.NET Core API)
**Files Updated**: Multiple configuration files aligned to these ports

## CORS Policy Decisions

### Development CORS Policy (Previous)
**Decision**: Allow localhost:4200 with credentials for development
**Context**: Frontend-backend communication in development environment
**Rationale**: Enables OAuth cookies and authenticated requests in development
**Configuration**: `AllowDevelopment` policy for localhost origins

## Session Management Decisions

### JWT + Cookie Hybrid Authentication (Previous)
**Decision**: Support both JWT tokens and OAuth cookies
**Context**: Need to support both API clients and OAuth flows
**Rationale**: 
- JWT for API clients and mobile apps
- Cookies for OAuth web flows
- SignInScheme set to cookies for OAuth compatibility
**Status**: Working correctly with proper challenge scheme configuration

## Performance Decisions

### Response Compression (Previous)
**Decision**: Enable GZIP compression for API responses
**Context**: Improve performance especially over ngrok tunnels
**Rationale**: Reduces bandwidth usage, improves user experience
**Configuration**: Enabled for JSON, SVG, and standard web content

## Development Workflow Decisions

### Proxy Configuration Strategy (Previous)
**Decision**: Use Angular CLI proxy for development API calls
**Context**: Simplify development by avoiding CORS issues
**Rationale**: 
- Single origin for frontend and API during development
- Simplifies authentication cookie handling
- Matches production reverse proxy setup
**Configuration**: `/api/*` proxied to `localhost:5032`

## Error Handling Strategy Decisions

### Graceful API Error Handling (2025-08-08)
**Decision**: Return structured errors instead of redirects for API endpoints
**Context**: Frontend needs to handle authentication errors gracefully  
**Rationale**:
- Better user experience with clear error messages
- Enables proper frontend retry logic
- Consistent with REST API standards
**Impact**: All API errors now return JSON with consistent structure

## Testing and Validation Decisions

### Development-First Testing Strategy (2025-08-08)
**Decision**: Focus OAuth testing in development environment before production consideration
**Context**: User explicitly requested development focus over production deployment
**Rationale**: 
- Validates fixes thoroughly before production risk
- Enables comprehensive testing without production impact
- Allows for iteration and refinement of authentication flow
**Impact**: Created comprehensive 47-scenario testing strategy for development validation
**Documentation**: `/ClaudeDocs/Report/test-strategy-oauth-fixes-20250808-120000.md`

---

*Last Updated: 2025-08-08*
*All decisions validated and working in production-like development environment*
*OAuth UserProfile creation fix validated in database*