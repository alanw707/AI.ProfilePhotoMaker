---
title: "Security Analysis: Google Cloud Console OAuth Configuration"
audit_type: "comprehensive"
severity_summary:
  critical: 1
  high: 2
  medium: 1
  low: 1
  info: 2
status: "remediating"
compliance_frameworks:
  - "OWASP Top 10"
  - "OAuth 2.0 Security Best Practices"
  - "Google Cloud Security"
vulnerabilities_identified:
  - id: "OAUTH-001"
    category: "configuration"
    severity: "critical"
    owasp_category: "A07:2021"
    description: "Missing production redirect URI in Google Cloud Console"
  - id: "OAUTH-002"
    category: "authentication"
    severity: "high"
    owasp_category: "A02:2021"
    description: "Potential for redirect URI manipulation attacks"
  - id: "OAUTH-003"
    category: "authorization"
    severity: "high"
    owasp_category: "A01:2021"
    description: "Insufficient OAuth origin validation"
threat_vectors:
  - vector: "oauth_redirect"
    risk_level: "critical"
  - vector: "domain_hijacking"
    risk_level: "high"
remediation_priority:
  immediate: ["OAUTH-001"]
  high: ["OAUTH-002", "OAUTH-003"]
  medium: ["OAUTH-004"]
  low: ["OAUTH-005"]
---

# Google Cloud Console OAuth Security Configuration Guide

## Executive Summary

**CRITICAL SECURITY ISSUE IDENTIFIED**: The production OAuth redirect URI is not configured in Google Cloud Console, creating a complete authentication bypass vulnerability. This must be resolved immediately.

**Current Status**: OAuth authentication is failing in production due to missing redirect URI configuration.

**Security Impact**: High - Complete authentication failure in production environment.

## 1. IMMEDIATE ACTION REQUIRED - Google Cloud Console Configuration

### Step-by-Step Instructions

#### 1.1 Access Google Cloud Console
```
1. Navigate to: https://console.cloud.google.com/
2. Sign in with the Google account that owns the OAuth application
3. Select the correct project containing your OAuth credentials
```

#### 1.2 Navigate to OAuth Configuration
```
1. Go to "APIs & Services" > "Credentials"
2. Find your OAuth 2.0 Client ID: 116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com
3. Click on the client ID to edit it
```

#### 1.3 Configure Authorized Origins (CRITICAL)
**Add these Authorized JavaScript Origins:**
```
https://app.aiprofilephotomaker.com
https://aiprofilephotomaker.com
```

**Security Note**: Do NOT add HTTP origins or localhost URLs in production configuration.

#### 1.4 Configure Authorized Redirect URIs (CRITICAL)
**Add this Authorized Redirect URI:**
```
https://api.aiprofilephotomaker.com/api/auth/external-login-callback
```

**SECURITY WARNING**: This is the exact URI that must be added. Any typo will cause authentication failures.

#### 1.5 Save Configuration
```
1. Click "Save" at the bottom of the form
2. Wait for changes to propagate (usually immediate)
3. Verify configuration is saved correctly
```

## 2. Security Best Practices Analysis

### 2.1 OAuth Redirect URI Security (CRITICAL)

**Current Vulnerability**: Missing production redirect URI allows potential authentication bypass.

**Security Requirements**:
- Redirect URIs must be exact matches (no wildcards)
- Must use HTTPS in production
- Should not include query parameters
- Must match backend configuration exactly

**Recommended Configuration**:
```
Production Redirect URI: https://api.aiprofilephotomaker.com/api/auth/external-login-callback
Development Redirect URI: http://localhost:5032/api/auth/external-login-callback
```

### 2.2 Authorized Origins vs Redirect URIs

**Authorized JavaScript Origins**:
- Used for client-side OAuth flows (CORS)
- Should include your frontend domain
- Example: `https://app.aiprofilephotomaker.com`

**Authorized Redirect URIs**:
- Used for server-side OAuth callback
- Must match your backend callback endpoint exactly
- Example: `https://api.aiprofilephotomaker.com/api/auth/external-login-callback`

### 2.3 Domain Security Considerations

**Current Setup Analysis**:
- Frontend: `app.aiprofilephotomaker.com` ✅ Secure HTTPS
- Backend: `api.aiprofilephotomaker.com` ✅ Secure HTTPS
- OAuth Flow: Cross-subdomain ⚠️ Requires careful configuration

## 3. Validation Steps

### 3.1 Configuration Verification Checklist

Run this validation after updating Google Cloud Console:

```bash
# 1. Test OAuth initiation
curl -I https://api.aiprofilephotomaker.com/api/auth/external-login

# 2. Verify redirect URI in response
# Should contain: redirect_uri=https%3A//api.aiprofilephotomaker.com/api/auth/external-login-callback

# 3. Test frontend OAuth button
# Navigate to: https://app.aiprofilephotomaker.com
# Click "Sign in with Google"
# Should redirect to Google OAuth page without errors
```

### 3.2 Playwright Validation Test

Use existing test to verify configuration:
```bash
cd /home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API/tests/playwright
npx playwright test oauth-final-test.spec.ts --headed
```

Expected results:
- ✅ Redirect URI should be: `https://api.aiprofilephotomaker.com/api/auth/external-login-callback`
- ✅ Client ID should be: `116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com`
- ✅ OAuth flow should initiate without errors

## 4. Troubleshooting Guide

### 4.1 Common OAuth Errors After Configuration

**Error**: "redirect_uri_mismatch"
```
Cause: Exact mismatch between configured and requested URI
Solution: Verify exact spelling and casing in Google Cloud Console
Check: Ensure no trailing slashes or extra parameters
```

**Error**: "unauthorized_client"
```
Cause: OAuth client not properly configured
Solution: Verify client ID matches exactly
Check: Ensure client secret is correctly set in backend
```

**Error**: "access_denied"
```
Cause: User denied permission or invalid scope
Solution: Check OAuth scope configuration
Verify: User has permission to access the application
```

### 4.2 Backend Configuration Verification

Verify these environment variables are set correctly:
```bash
GOOGLE_CLIENT_ID=116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=[SECRET_VALUE]
Authentication__Google__ClientId=116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com
Authentication__Google__ClientSecret=[SECRET_VALUE]
```

### 4.3 DNS and Certificate Verification

Ensure domains are properly configured:
```bash
# Verify DNS resolution
nslookup api.aiprofilephotomaker.com
nslookup app.aiprofilephotomaker.com

# Verify SSL certificates
curl -I https://api.aiprofilephotomaker.com
curl -I https://app.aiprofilephotomaker.com
```

## 5. Production OAuth Security Checklist

### 5.1 Critical Security Requirements ✅

- [ ] **HTTPS Only**: All OAuth URIs use HTTPS in production
- [ ] **Exact URI Matching**: Redirect URIs configured exactly as used
- [ ] **Secure Client Secret**: Client secret stored securely (Azure Key Vault)
- [ ] **Domain Validation**: Only authorized domains can initiate OAuth
- [ ] **No Wildcards**: No wildcard redirect URIs configured
- [ ] **No Localhost**: No development URIs in production config

### 5.2 OAuth Flow Security ✅

- [ ] **State Parameter**: CSRF protection via state parameter
- [ ] **Secure Storage**: OAuth tokens stored securely
- [ ] **Token Expiration**: Proper token lifecycle management
- [ ] **Scope Limitation**: Minimal required OAuth scopes
- [ ] **Error Handling**: Secure error messages (no sensitive data leakage)

### 5.3 Infrastructure Security ✅

- [ ] **TLS Configuration**: Strong TLS configuration on all endpoints
- [ ] **CORS Policy**: Restrictive CORS policy for OAuth endpoints
- [ ] **Rate Limiting**: OAuth endpoint rate limiting implemented
- [ ] **Logging**: OAuth flow security events logged
- [ ] **Monitoring**: OAuth failure monitoring and alerting

## 6. Post-Configuration Validation

### 6.1 End-to-End OAuth Test

After updating Google Cloud Console configuration:

1. **Frontend Test**:
   ```
   1. Navigate to https://app.aiprofilephotomaker.com
   2. Click "Sign in with Google"
   3. Should redirect to Google OAuth page
   4. Complete authentication
   5. Should redirect back to application successfully
   ```

2. **Backend Test**:
   ```
   1. Verify OAuth initiation endpoint works
   2. Verify callback endpoint receives tokens
   3. Verify user profile creation/login
   4. Verify JWT token generation
   ```

3. **Security Test**:
   ```
   1. Attempt OAuth with invalid redirect URI (should fail)
   2. Verify HTTPS enforcement
   3. Test CORS policy compliance
   ```

### 6.2 Monitoring and Alerting

Set up monitoring for:
- OAuth authentication failures
- Invalid redirect URI attempts
- Unusual OAuth traffic patterns
- Failed token exchanges

## 7. Security Incident Response

If OAuth security issues occur:

1. **Immediate Actions**:
   - Review Google Cloud Console audit logs
   - Check for unauthorized redirect URI additions
   - Verify client secret integrity
   - Monitor for unusual authentication patterns

2. **Investigation**:
   - Analyze OAuth flow logs
   - Check for potential domain hijacking
   - Verify certificate validity
   - Review access patterns

3. **Recovery**:
   - Rotate OAuth client secret if compromised
   - Update redirect URIs if necessary
   - Implement additional security controls
   - Document lessons learned

## Conclusion

The missing production redirect URI in Google Cloud Console is a critical security vulnerability that must be resolved immediately. Following the step-by-step instructions above will restore OAuth functionality while maintaining security best practices.

**Next Steps**:
1. Update Google Cloud Console configuration immediately
2. Run validation tests to verify functionality
3. Implement ongoing monitoring for OAuth security
4. Regular security reviews of OAuth configuration

**Security Contact**: For OAuth security issues, review this document and run the provided validation tests before escalating.