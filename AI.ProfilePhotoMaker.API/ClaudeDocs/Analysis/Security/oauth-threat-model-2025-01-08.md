# OAuth Threat Model - AI.ProfilePhotoMaker

## System Components

### OAuth Flow Components
1. **Frontend Application** (http://localhost:4200)
2. **API Backend** (http://localhost:5032)
3. **Google OAuth Provider** (accounts.google.com)
4. **Session Storage** (Memory-based)
5. **Database** (SQLite - User accounts)

## Trust Boundaries

### Boundary 1: Frontend ↔ API
- **Interface:** HTTPS/HTTP requests
- **Authentication:** JWT tokens, session cookies
- **Trust Level:** Partial (CORS-protected)

### Boundary 2: API ↔ Google OAuth
- **Interface:** OAuth 2.0 protocol
- **Authentication:** Client credentials, authorization codes
- **Trust Level:** High (OAuth provider)

### Boundary 3: API ↔ Database
- **Interface:** Entity Framework queries
- **Authentication:** Connection string
- **Trust Level:** Full (internal)

## Threat Analysis

### T1: OAuth Credential Theft
- **Attack Vector:** Hardcoded client ID exposure
- **Impact:** Complete OAuth flow compromise
- **Likelihood:** High
- **Severity:** Critical
- **Mitigations:** Remove hardcoded credentials, use secure config

### T2: State Parameter Manipulation
- **Attack Vector:** Predictable/weak state generation
- **Impact:** CSRF attacks, session hijacking
- **Likelihood:** Medium
- **Severity:** High
- **Mitigations:** Cryptographically secure state generation

### T3: Information Disclosure
- **Attack Vector:** Debug endpoints, verbose logging
- **Impact:** System reconnaissance, credential exposure
- **Likelihood:** High
- **Severity:** High
- **Mitigations:** Remove debug endpoints, sanitize logs

### T4: Session Fixation
- **Attack Vector:** Insecure cookie configuration
- **Impact:** Session hijacking
- **Likelihood:** Medium
- **Severity:** Medium
- **Mitigations:** Secure cookie settings, proper SameSite policy

### T5: Code Interception
- **Attack Vector:** Missing PKCE implementation
- **Impact:** Authorization code theft
- **Likelihood:** Low
- **Severity:** Medium
- **Mitigations:** Implement PKCE protection

## Attack Scenarios

### Scenario 1: OAuth Phishing Attack
1. Attacker obtains hardcoded client ID from API response
2. Creates malicious OAuth application with same client ID
3. Tricks users into authorizing malicious app
4. Gains access to user Google account data

### Scenario 2: CSRF Via State Manipulation
1. Attacker analyzes state generation pattern
2. Predicts or manipulates state parameter
3. Initiates OAuth flow with victim's session
4. Completes authorization with attacker's account
5. Victim unknowingly logs into attacker's account

### Scenario 3: Information Gathering
1. Attacker discovers debug endpoints
2. Extracts OAuth configuration details
3. Identifies security weaknesses
4. Plans targeted attack using gathered intelligence

## Risk Matrix

| Threat | Likelihood | Impact | Risk Level |
|--------|-----------|--------|------------|
| T1: Credential Theft | High | Critical | Critical |
| T2: State Manipulation | Medium | High | High |
| T3: Information Disclosure | High | High | High |
| T4: Session Fixation | Medium | Medium | Medium |
| T5: Code Interception | Low | Medium | Low |

## Security Controls

### Existing Controls (Effective)
- ✅ HTTPS enforcement
- ✅ CORS policy implementation
- ✅ State parameter validation
- ✅ JWT token authentication
- ✅ Secure password policies

### Missing Controls (Required)
- ❌ Secure credential management
- ❌ PKCE implementation
- ❌ Production-ready error handling
- ❌ OAuth rate limiting
- ❌ Security monitoring

### Ineffective Controls (Needs Fix)
- ⚠️ Session cookie configuration
- ⚠️ OAuth state management consistency
- ⚠️ Debug endpoint exposure
- ⚠️ Sensitive data logging

## Recommendations

### Immediate (Critical)
1. Remove all hardcoded OAuth credentials
2. Implement secure configuration management
3. Disable debug endpoints in production

### Short-term (High Priority)
1. Standardize OAuth state management
2. Fix session cookie security settings
3. Implement proper error handling

### Long-term (Enhancement)
1. Add PKCE support
2. Implement OAuth monitoring
3. Add rate limiting protection
4. Enhance audit logging

## Monitoring Strategy

### Security Events to Monitor
- Failed OAuth attempts
- State validation failures
- Debug endpoint access
- Unusual redirect patterns
- Session anomalies

### Alert Thresholds
- More than 5 failed OAuth attempts per IP/hour
- Any access to debug endpoints in production
- State validation failure rate > 1%
- Cross-origin requests from unknown domains