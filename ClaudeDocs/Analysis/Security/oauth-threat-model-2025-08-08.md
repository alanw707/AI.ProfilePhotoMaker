# OAuth Threat Model: AI Profile Photo Maker

## Executive Summary

This threat model analyzes the OAuth 2.0 implementation in AI Profile Photo Maker, identifying attack vectors, security controls, and mitigation strategies. The analysis reveals critical vulnerabilities in the current OAuth flow that require immediate remediation.

## System Overview

### OAuth Flow Architecture
```
[User Browser] <-> [Angular Frontend] <-> [.NET API] <-> [Google OAuth]
                      |                        |
                      v                        v
               [LocalStorage]            [Session Store]
                   |                          |
                   v                          v
               [JWT Token]               [OAuth State]
```

### Components
- **Frontend**: Angular SPA with OAuth initiation
- **Backend**: .NET 8 API with OAuth callback handling
- **OAuth Provider**: Google OAuth 2.0
- **Session Storage**: ASP.NET Core Session (Memory/Distributed)
- **Token Storage**: Browser localStorage

## Threat Analysis

### Threat Actors

| Actor | Motivation | Capabilities | Access Level |
|-------|------------|-------------|-------------|
| External Attacker | Financial gain, data theft | Network access, social engineering | External |
| Malicious Insider | Data exfiltration | System access, code knowledge | Internal |
| Script Kiddie | Recognition, disruption | Automated tools, public exploits | External |
| Nation State | Espionage, surveillance | Advanced persistent threats | External |

### Attack Surface

#### 1. OAuth Initiation (`/api/auth/external-login/google`)
**Threats:**
- CSRF attacks against OAuth initiation
- State parameter manipulation
- Redirect URI manipulation

**Current Controls:**
- Manual state generation using `Guid.NewGuid()`
- Session-based state storage
- Redirect URI validation

**Vulnerabilities:**
- No CSRF protection
- Weak state validation
- Session fixation potential

#### 2. OAuth Callback (`/signin-google`, `/api/auth/external-login-callback`)
**Threats:**
- Authorization code interception
- State validation bypass
- Token injection attacks
- Callback URL manipulation

**Current Controls:**
- State parameter validation
- Authorization code exchange
- JWT token generation

**Vulnerabilities:**
- JWT tokens transmitted via URL
- Insufficient error handling
- Sensitive data logging

#### 3. Token Handling
**Threats:**
- Token theft from localStorage
- JWT token manipulation
- Token replay attacks
- XSS-based token extraction

**Current Controls:**
- JWT signing with HS256
- Token expiration (1 hour)
- Client-side token validation

**Vulnerabilities:**
- Weak JWT secret validation
- localStorage storage
- No token refresh mechanism

### STRIDE Analysis

#### Spoofing
- **Threat**: Attacker impersonates legitimate OAuth provider
- **Vulnerability**: Insufficient provider validation
- **Impact**: Account takeover, credential theft
- **Mitigation**: TLS certificate validation, provider whitelist

#### Tampering
- **Threat**: OAuth response manipulation
- **Vulnerability**: Client-side token handling
- **Impact**: Unauthorized access, privilege escalation
- **Mitigation**: Server-side validation, signed responses

#### Repudiation
- **Threat**: Denial of malicious actions
- **Vulnerability**: Insufficient audit logging
- **Impact**: Forensic investigation challenges
- **Mitigation**: Comprehensive security logging

#### Information Disclosure
- **Threat**: Exposure of authentication tokens
- **Vulnerability**: URL-based token transmission, console logging
- **Impact**: Account compromise, data breach
- **Mitigation**: Secure token transmission, log sanitization

#### Denial of Service
- **Threat**: OAuth flow disruption
- **Vulnerability**: No rate limiting on auth endpoints
- **Impact**: Service unavailability
- **Mitigation**: Rate limiting, circuit breakers

#### Elevation of Privilege
- **Threat**: Unauthorized access escalation
- **Vulnerability**: Insufficient authorization checks
- **Impact**: Administrative access, data manipulation
- **Mitigation**: Role-based access control, claim validation

## Specific Threat Scenarios

### Scenario 1: OAuth State Attack
**Attack Vector:**
1. Attacker initiates OAuth flow for victim
2. Victim completes authentication
3. Attacker captures authorization code
4. Attacker completes flow with victim's token

**Current Vulnerability:**
```csharp
// Weak state validation in ExternalLoginCallback
var sessionState = HttpContext.Session.GetString("oauth_state");
if (string.IsNullOrEmpty(state) || state != sessionState)
{
    return Redirect($"{frontendBaseUrl}/login?error=invalid_state");
}
```

**Exploitation:**
- Session state can be predicted or manipulated
- No cryptographic binding between client and server
- Session fixation allows state bypass

**Impact:** Account takeover, unauthorized access

### Scenario 2: JWT Token Interception
**Attack Vector:**
1. Victim completes OAuth flow
2. JWT token transmitted in URL redirect
3. Token captured via browser history, logs, or referrer headers
4. Attacker uses token for unauthorized access

**Current Vulnerability:**
```csharp
// JWT transmitted via URL parameter
return Redirect($"{frontendBaseUrl}{returnUrl}?token={tokenInfo.Token}");
```

**Exploitation:**
- URLs logged in server access logs
- Browser history contains tokens
- Referrer headers leak tokens to external sites

**Impact:** Complete account compromise

### Scenario 3: XSS-based Token Theft
**Attack Vector:**
1. Attacker injects malicious JavaScript
2. Script accesses localStorage tokens
3. Tokens exfiltrated to attacker's server
4. Account takeover achieved

**Current Vulnerability:**
```typescript
// Insecure token storage in localStorage
localStorage.setItem(this.TOKEN_KEY, authResult.token);
```

**Exploitation:**
- XSS vulnerabilities enable script execution
- localStorage accessible to malicious scripts
- No httpOnly flag protection

**Impact:** Mass account compromise

### Scenario 4: Console Log Information Disclosure
**Attack Vector:**
1. Attacker gains access to application logs
2. Sensitive OAuth data extracted from console output
3. Tokens or state parameters used for attacks

**Current Vulnerability:**
```csharp
Console.WriteLine($"🔄 OAuth Callback - Code: {code?.Substring(0, Math.Min(10, code?.Length ?? 0))}...");
Console.WriteLine($"✅ OAuth success - User: {user.Email}");
```

**Exploitation:**
- Authorization codes partially logged
- User emails exposed in logs
- Log aggregation systems may expose data

**Impact:** Data breach, privacy violation

## Risk Assessment Matrix

| Threat | Likelihood | Impact | Risk Score | Priority |
|--------|------------|---------|------------|----------|
| OAuth State Attack | High | High | Critical | P0 |
| JWT Token Interception | High | High | Critical | P0 |
| XSS Token Theft | Medium | High | High | P1 |
| Console Log Disclosure | Medium | Medium | Medium | P2 |
| CSRF OAuth Initiation | Low | High | Medium | P2 |
| Session Fixation | Low | High | Medium | P2 |
| Token Replay | Medium | Low | Low | P3 |

## Security Controls Assessment

### Existing Controls

| Control | Implementation | Effectiveness | Status |
|---------|----------------|---------------|--------|
| State Parameter | Manual GUID generation | Weak | ❌ Inadequate |
| JWT Signing | HS256 with secret | Medium | ⚠️ Weak secret |
| Token Expiration | 1-hour fixed | Medium | ✅ Adequate |
| HTTPS Enforcement | Production only | Medium | ⚠️ Dev bypass |
| Input Validation | Basic checks | Weak | ❌ Insufficient |

### Missing Controls

| Control | Priority | Implementation Effort | Security Impact |
|---------|----------|----------------------|-----------------|
| CSRF Protection | Critical | Low | High |
| Secure Token Transmission | Critical | Medium | High |
| State Cryptographic Binding | Critical | Medium | High |
| Rate Limiting | High | Medium | Medium |
| Security Headers | High | Low | Medium |
| Audit Logging | Medium | Medium | Medium |

## Mitigation Strategies

### Immediate (P0) - Week 1

#### 1. Secure Token Transmission
**Current:**
```csharp
return Redirect($"{frontendBaseUrl}{returnUrl}?token={tokenInfo.Token}");
```

**Secure Implementation:**
```csharp
// Store token temporarily in secure session
HttpContext.Session.SetString($"temp_token_{sessionId}", tokenInfo.Token);
return Redirect($"{frontendBaseUrl}/auth/complete?session={sessionId}");

// Separate endpoint to exchange session for token
[HttpPost("exchange-token")]
public async Task<IActionResult> ExchangeToken([FromBody] TokenExchangeRequest request)
{
    var token = HttpContext.Session.GetString($"temp_token_{request.SessionId}");
    HttpContext.Session.Remove($"temp_token_{request.SessionId}");
    
    return Ok(new { token, httpOnly = true });
}
```

#### 2. Implement Proper OAuth State Validation
**Current:**
```csharp
var state = Guid.NewGuid().ToString();
HttpContext.Session.SetString("oauth_state", state);
```

**Secure Implementation:**
```csharp
// Use ASP.NET Core OAuth middleware correlation cookies
public IActionResult ExternalLogin(string provider, string returnUrl)
{
    var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, 
        Url.Action("ExternalLoginCallback"));
    return Challenge(properties, provider);
}
```

#### 3. Remove Sensitive Data from Logs
**Implementation:**
```csharp
// Replace Console.WriteLine with proper logging
_logger.LogInformation("OAuth callback received for provider {Provider}", provider);
_logger.LogInformation("OAuth authentication successful for user {UserId}", user.Id);

// Implement log sanitization
public static class LogSanitizer
{
    public static string SanitizeUrl(string url)
    {
        var uri = new Uri(url);
        var sanitized = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
        return sanitized;
    }
}
```

### High Priority (P1) - Week 2

#### 4. Implement CSRF Protection
```csharp
[HttpGet("external-login/{provider}")]
[ValidateAntiForgeryToken]
public IActionResult ExternalLogin(string provider, string returnUrl = "/")
{
    // CSRF token automatically validated
}
```

#### 5. Secure Frontend Token Storage
```typescript
// Use HTTP-only cookies instead of localStorage
// Configure cookie settings
const secureCookieOptions = {
    httpOnly: true,
    secure: true,
    sameSite: 'strict',
    maxAge: 3600 // 1 hour
};

// Backend sets secure cookie
response.cookie('auth_token', token, secureCookieOptions);
```

#### 6. Add Security Headers
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Content-Security-Policy", 
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'");
    
    await next();
});
```

### Medium Priority (P2) - Week 3-4

#### 7. Implement Rate Limiting
```csharp
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

[EnableRateLimiting("AuthPolicy")]
public async Task<IActionResult> Login([FromBody] LoginDto model)
```

#### 8. Enhanced Audit Logging
```csharp
public class SecurityAuditService
{
    public void LogAuthenticationEvent(string eventType, string userId, string ipAddress, 
        Dictionary<string, string> additionalData)
    {
        var auditEvent = new
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            UserId = userId,
            IPAddress = ipAddress,
            UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
            AdditionalData = additionalData
        };
        
        _logger.LogInformation("SECURITY_AUDIT: {AuditEvent}", 
            JsonSerializer.Serialize(auditEvent));
    }
}
```

## Monitoring and Detection

### Security Metrics
1. **Failed Authentication Attempts** - Monitor for brute force attacks
2. **OAuth Callback Failures** - Detect state manipulation attempts
3. **Token Usage Patterns** - Identify suspicious access patterns
4. **Geographic Access Anomalies** - Flag unusual login locations

### Alerting Rules
```yaml
# Example security alerts
alerts:
  - name: "Multiple Auth Failures"
    condition: "failed_auth_count > 5 in 5m"
    severity: "high"
    
  - name: "OAuth State Validation Failures"
    condition: "oauth_state_failures > 3 in 10m"
    severity: "critical"
    
  - name: "Token in URL Detection"
    condition: "logs contain 'token=' in URL"
    severity: "high"
```

## Compliance Mapping

### OWASP OAuth Security Cheat Sheet
- ✅ Use HTTPS for all OAuth flows
- ❌ Implement PKCE for public clients
- ❌ Validate redirect URIs strictly
- ❌ Use state parameter with CSRF protection
- ❌ Implement proper token storage

### NIST Cybersecurity Framework
- **Identify**: Asset inventory complete
- **Protect**: Missing security controls
- **Detect**: Limited monitoring capabilities
- **Respond**: No incident response procedures
- **Recover**: No backup authentication methods

## Testing Strategy

### Security Test Cases

#### Authentication Tests
1. **State Parameter Tampering**
   - Modify state parameter in OAuth callback
   - Verify proper error handling

2. **Token Interception**
   - Monitor network traffic for token leakage
   - Test URL parameter injection

3. **Session Security**
   - Test session fixation attacks
   - Verify proper session invalidation

#### Authorization Tests
1. **JWT Token Manipulation**
   - Modify token claims
   - Test signature validation

2. **Cross-User Access**
   - Attempt access with different user tokens
   - Verify proper authorization checks

### Automated Testing
```csharp
[Test]
public async Task OAuth_StateParameter_ShouldBeValidated()
{
    // Arrange
    var invalidState = "malicious_state";
    
    // Act
    var response = await client.GetAsync($"/api/auth/external-login-callback?state={invalidState}&code=valid_code");
    
    // Assert
    Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
}

[Test]
public async Task JWT_Token_ShouldNotAppearInURL()
{
    // Test that OAuth flow doesn't expose tokens in URLs
    // Implementation specific to your testing framework
}
```

## Recovery Procedures

### Security Incident Response

#### Token Compromise
1. **Immediate Actions**
   - Revoke compromised tokens
   - Force re-authentication for affected users
   - Analyze access logs for unauthorized activity

2. **Investigation**
   - Identify compromise vector
   - Assess scope of impact
   - Document lessons learned

#### OAuth Provider Compromise
1. **Failover Procedures**
   - Disable OAuth provider integration
   - Enable alternative authentication methods
   - Communicate with users about temporary restrictions

## Conclusion

The current OAuth implementation contains critical security vulnerabilities that require immediate attention. The threat model identifies specific attack vectors and provides detailed mitigation strategies prioritized by risk level.

**Key Recommendations:**
1. Implement secure token transmission via HTTP-only cookies
2. Use built-in ASP.NET Core OAuth middleware for proper state handling
3. Remove sensitive data from application logs
4. Add comprehensive security controls (CSRF, rate limiting, security headers)
5. Implement proper security monitoring and incident response procedures

**Timeline:** 4 weeks for complete threat mitigation implementation.

---
*Threat Model Version: 1.0*
*Last Updated: 2025-08-08*
*Next Review: 2025-09-08*