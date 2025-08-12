---
type: test-strategy
timestamp: 2025-01-11T18:50:00Z
project: ai-profile-photo-maker
test_coverage:
  build_process: pending
  deployment: pending  
  domain_persistence: pending
  oauth_integration: pending
  end_to_end: pending
risk_assessment:
  level: high
  critical_areas: [domain_persistence, oauth_configuration, deployment_process]
focus_areas: [deployment, domain_configuration, oauth_validation]
version: 1.0
---

# OAuth & Domain Persistence Testing Strategy

## Executive Summary

Comprehensive testing strategy for validating updated Google OAuth configuration and Bicep template deployment with domain persistence solution. Critical focus on ensuring custom domains survive full deployment cycles.

## Test Objectives

### Primary Goals
1. **Build Process Validation**: Ensure container builds work with updated environment
2. **Deployment Verification**: Test updated Bicep template deploys successfully  
3. **Domain Persistence**: Validate custom domains persist through full deployment
4. **OAuth Functionality**: Confirm OAuth works with custom domains
5. **End-to-End Validation**: Complete user journey testing

### Success Criteria
- Build process completes without errors
- Deployment succeeds with domain configuration intact
- Custom domains remain accessible post-deployment
- OAuth login functions correctly with app.aiprofilephotomaker.com
- Cross-domain API communication works properly

## Risk Assessment

### High-Risk Areas
1. **Domain Configuration Loss**: Custom domains may not persist through deployment
2. **Certificate Binding Issues**: SSL certificates may become unbound
3. **OAuth Origin Mismatch**: Google OAuth may reject requests from custom domains
4. **CORS Configuration**: Cross-domain requests may fail

### Medium-Risk Areas
1. **Build Process Failures**: Docker image builds may fail with updated config
2. **ACR Authentication**: Container registry access issues
3. **Health Check Failures**: Application startup issues in new environment

## Test Categories

### 1. Pre-Deployment Validation
- **Scope**: Infrastructure and build readiness
- **Tools**: Docker, Azure CLI, static analysis
- **Coverage**: Build process, ACR connectivity, Bicep validation

### 2. Deployment Testing  
- **Scope**: Infrastructure deployment and configuration
- **Tools**: Azure CLI, Bicep deployment
- **Coverage**: Resource creation, domain binding, certificate application

### 3. Domain Persistence Testing
- **Scope**: Custom domain configuration survival
- **Tools**: Azure CLI, custom validation scripts
- **Coverage**: Domain binding verification, certificate persistence

### 4. Playwright E2E Testing
- **Scope**: End-user functionality validation
- **Tools**: Playwright, browser automation
- **Coverage**: Frontend access, OAuth flow, API communication

### 5. OAuth Integration Testing
- **Scope**: Google OAuth functionality with custom domains
- **Tools**: Playwright, OAuth flow automation
- **Coverage**: Login flow, token validation, session management

## Test Execution Plan

### Phase 1: Pre-Deployment Validation (5-10 min)
1. Environment configuration check
2. Docker build process validation
3. ACR connectivity verification
4. Bicep template syntax validation

### Phase 2: Build & Push Testing (10-15 min)
1. Local image building
2. ACR authentication and push
3. Image integrity verification
4. Tag management validation

### Phase 3: Deployment Testing (15-20 min)
1. Infrastructure deployment
2. Resource creation verification
3. Custom domain configuration check
4. Certificate binding validation

### Phase 4: Domain Persistence Verification (5-10 min)
1. Pre-deployment domain state capture
2. Post-deployment domain state verification
3. DNS resolution testing
4. SSL certificate continuity check

### Phase 5: Playwright E2E Testing (10-15 min)
1. Frontend accessibility testing
2. Backend health check validation
3. CORS functionality testing
4. OAuth login flow validation

### Phase 6: Regression & Edge Case Testing (10-15 min)
1. Multiple deployment cycles
2. Certificate renewal simulation
3. DNS propagation testing
4. Error condition handling

## Test Environment

### Infrastructure Components
- **Frontend**: app.aiprofilephotomaker.com
- **Backend**: api.aiprofilephotomaker.com  
- **Container Registry**: aipmcrv16j74jubocuukg.azurecr.io
- **Resource Group**: aiprofilemaker-v1

### Certificate Configuration
- Frontend Certificate ID: `/subscriptions/.../mc-aipm-env-v1-6j-app-aiprofilepho-5691`
- Backend Certificate ID: `/subscriptions/.../mc-aipm-env-v1-6j-api-aiprofilepho-8094`

## Testing Tools & Framework

### Playwright Configuration
- **Browser**: Chromium (headless for CI, headed for debugging)
- **Timeout**: 30s per test, 10 min total suite
- **Retries**: 3 attempts for network-dependent tests
- **Screenshots**: On failure for debugging

### Validation Scripts
- **Primary**: `/scripts/validate-deployment.js` (Node.js/Playwright)
- **Wrapper**: `/scripts/validate-deployment.sh` (Bash automation)
- **Build**: `/scripts/build-local.sh` & `/scripts/push-to-acr.sh`

## Expected Results

### Build Process
- Docker images build successfully
- Images tagged with latest and build number
- ACR push completes without errors
- Image verification passes

### Deployment Process  
- Bicep deployment succeeds
- All Azure resources created/updated
- Custom domains properly configured
- SSL certificates correctly bound

### Domain Persistence
- Custom domains survive deployment
- DNS resolution remains functional
- SSL certificates stay valid
- HTTPS access works immediately

### OAuth Integration
- Google OAuth accepts requests from custom domain
- Login flow completes successfully
- JWT tokens generated and validated
- Session management functions properly

## Failure Scenarios & Mitigation

### Domain Configuration Loss
- **Detection**: DNS resolution fails, domain binding missing
- **Mitigation**: Re-run deployment, manual domain binding
- **Prevention**: Verify Bicep template customDomains section

### Certificate Binding Issues
- **Detection**: SSL errors, HTTPS access fails
- **Mitigation**: Manual certificate binding, certificate renewal
- **Prevention**: Validate certificate IDs in Bicep template

### OAuth Origin Rejection
- **Detection**: OAuth login fails with CORS/origin errors
- **Mitigation**: Update Google OAuth console allowed origins
- **Prevention**: Verify CORS configuration matches domains

## Reporting & Documentation

### Test Reports
- **Location**: `/scripts/validation-report.json`
- **Format**: JSON with detailed test results and timings
- **Distribution**: Console output + saved report file

### Success Metrics
- **Build Success Rate**: 100% (all builds must succeed)
- **Deployment Success Rate**: 100% (infrastructure deployment must work)
- **Domain Persistence Rate**: 100% (domains must survive deployment)
- **OAuth Success Rate**: 100% (login flow must function)
- **E2E Test Pass Rate**: ≥90% (core functionality must work)

### Quality Gates
- All high-risk scenarios must pass
- Domain persistence must be validated
- OAuth integration must be functional
- Full deployment cycle must complete successfully

## Next Steps After Testing

### On Success
1. Document successful deployment process
2. Update deployment documentation
3. Schedule periodic validation runs
4. Monitor production metrics

### On Failure  
1. Capture detailed error information
2. Document failure scenarios and resolutions
3. Update test strategy based on findings
4. Implement additional safeguards

---

**Test Strategy Owner**: Claude (QA Engineer)  
**Review Date**: 2025-01-11  
**Next Review**: Post-deployment validation