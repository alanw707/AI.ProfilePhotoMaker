# Test Design: Epic 2 - Account Access & Profile Management

**Date:** 2025-12-22T12:19:26-08:00
**Author:** Alan
**Status:** Draft

---

## Executive Summary

**Scope:** targeted test design for Epic 2

**Risk Summary:**

- Total risks identified: 6
- High-priority risks (>=6): 3
- Critical categories: SEC, DATA, BUS

**Coverage Summary:**

- P0 scenarios: 6 (12 hours)
- P1 scenarios: 6 (6 hours)
- P2/P3 scenarios: 6 (2.5 hours)
- **Total effort**: 20.5 hours (~2.6 days)

---

## Risk Assessment

### High-Priority Risks (Score >=6)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner | Timeline |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- | -------- |
| R2-001 | SEC | Auth bypass due to JWT validation or authz gaps | 2 | 3 | 6 | Authz tests + token validation checks | QA/DEV | TBD |
| R2-002 | SEC | OAuth state/redirect mishandling enables account takeover | 2 | 3 | 6 | OAuth state tests + callback validation | QA/DEV | TBD |
| R2-003 | DATA | Account deletion leaves data or files behind | 2 | 3 | 6 | Deletion flow tests for DB + filesystem | QA/DEV | TBD |

### Medium-Priority Risks (Score 3-4)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- |
| R2-004 | DATA | Profile auto-creation results in inconsistent credits | 2 | 2 | 4 | OAuth first-login tests | QA |
| R2-005 | BUS | Data export omits critical assets or metadata | 2 | 2 | 4 | Export content validation | QA |

### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | Probability | Impact | Score | Action |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ------ |
| R2-006 | OPS | Turnstile/rate-limit misconfig blocks auth in some envs | 1 | 2 | 2 | Monitor |

### Risk Category Legend

- **TECH**: Technical/Architecture (flaws, integration, scalability)
- **SEC**: Security (access controls, auth, data exposure)
- **PERF**: Performance (SLA violations, degradation, resource limits)
- **DATA**: Data Integrity (loss, corruption, inconsistency)
- **BUS**: Business Impact (UX harm, logic errors, revenue)
- **OPS**: Operations (deployment, config, monitoring)

---

## Test Coverage Plan

### P0 (Critical) - Run on every commit

**Criteria**: Core access + High risk (>=6) + No workaround

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Email/password login issues JWT | API | R2-001 | 1 | QA | Valid creds -> JWT, invalid -> 401 |
| Protected endpoint rejects unauthenticated | API | R2-001 | 1 | QA | Authz guard enforced |
| Google OAuth callback completes safely | Integration | R2-002 | 1 | QA | State validation required |
| OAuth first login creates profile w/ credits | API | R2-004 | 1 | QA | Default weekly credits set |
| Account deletion removes DB records | API | R2-003 | 1 | QA | Profile + images deleted |
| Account deletion removes files | Integration | R2-003 | 1 | QA | Filesystem cleanup verified |

**Total P0**: 6 tests, 12 hours

### P1 (High) - Run on PR to main

**Criteria**: Important account features + Medium risk

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Register endpoint creates user | API | R2-001 | 1 | QA | Validate password rules |
| Profile CRUD operations | API | - | 2 | QA | GET/PUT/DELETE happy paths |
| Data export includes images + metadata | Integration | R2-005 | 1 | QA | Export coverage check |
| Token expiry handling | Unit | R2-001 | 1 | DEV | JWT expiry enforced |
| OAuth URL generation | Unit | R2-002 | 1 | DEV | Correct provider URL |

**Total P1**: 6 tests, 6 hours

### P2 (Medium) - Run nightly/weekly

**Criteria**: Secondary scenarios + edge cases

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Duplicate profile prevention | API | R2-004 | 1 | QA | No duplicate profiles |
| Export error handling | API | R2-005 | 1 | QA | Graceful failure path |
| Auth rate-limiting response | API | R2-006 | 1 | QA | 429 handled gracefully |
| Profile stats fields validated | Unit | - | 1 | DEV | DTO validation |

**Total P2**: 4 tests, 2 hours

### P3 (Low) - Run on-demand

**Criteria**: Rare issues or config-only scenarios

| Requirement | Test Level | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- |
| Turnstile misconfig returns clear error | API | 1 | QA | Clear error message |
| OAuth provider error mapping | Unit | 1 | DEV | Error mapping coverage |

**Total P3**: 2 tests, 0.5 hours

---

## Execution Order

### Smoke Tests

- Login issues JWT
- Protected endpoint rejects unauthenticated

### P0 Tests

- OAuth callback completes safely
- OAuth first login creates profile
- Account deletion removes DB records
- Account deletion removes files

### P1 Tests

- Register creates user
- Profile CRUD operations
- Data export includes images
- Token expiry handling
- OAuth URL generation

### P2/P3 Tests

- Duplicate profile prevention
- Export error handling
- Auth rate-limiting response
- Turnstile misconfig handling

---

## Resource Estimates

### Test Development Effort (Planning Baseline)

| Priority | Count | Hours/Test | Total Hours | Notes |
| --- | --- | --- | --- | --- |
| P0 | 6 | 2.0 | 12 | Auth and deletion critical |
| P1 | 6 | 1.0 | 6 | Core profile flows |
| P2 | 4 | 0.5 | 2 | Edge cases |
| P3 | 2 | 0.25 | 0.5 | Config-only |
| **Total** | **18** | **-** | **20.5** | **~2.6 days** |

### Prerequisites

**Test Data:**

- Users with local auth and OAuth identities
- Profiles with sample image records

**Tooling:**

- xUnit for API tests (`AI.ProfilePhotoMaker.API.Tests`)
- Playwright for E2E auth flows (`tests/e2e`, `AI.ProfilePhotoMaker.UI/e2e`)

**Environment:**

- OAuth test credentials configured
- File storage available for delete/export tests

---

## Quality Gate Criteria

### Pass/Fail Thresholds

- **P0 pass rate**: 100%
- **P1 pass rate**: >=95%
- **P2/P3 pass rate**: >=90%
- **High-risk mitigations**: 100% complete or waived

### Coverage Targets

- **Critical paths**: >=80%
- **Security scenarios**: 100%
- **Business logic**: >=70%
- **Edge cases**: >=50%

### Non-Negotiable Requirements

- [ ] All P0 tests pass
- [ ] No high-risk (>=6) items unmitigated
- [ ] Security tests (SEC) pass 100%

---

## Mitigation Plans

### R2-001: Auth bypass risk (Score: 6)

**Mitigation Strategy:** Add API tests for authz checks and JWT validation (expiry, signature).  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** API tests for protected endpoints

### R2-002: OAuth state/redirect issues (Score: 6)

**Mitigation Strategy:** Integration tests for OAuth state validation and callback security.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** OAuth flow tests in staging

### R2-003: Account deletion incomplete (Score: 6)

**Mitigation Strategy:** Tests that verify DB records and filesystem assets are fully removed.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** Deletion tests with file checks

---

## Assumptions and Dependencies

### Assumptions

1. OAuth providers are reachable in test environments.
2. File storage is accessible for delete/export tests.
3. Existing tests cover some auth flows in `AI.ProfilePhotoMaker.UI/e2e` and `tests/e2e`.

### Dependencies

1. OAuth client IDs/secrets configured in environment.
2. Storage paths for profile assets available.

### Risks to Plan

- **Risk**: OAuth provider downtime affects test reliability
  - **Impact**: Flaky auth tests
  - **Contingency**: Use mocked OAuth callback for CI

---

## Approval

**Test Design Approved By:**

- [ ] Product Manager: TBD Date: TBD
- [ ] Tech Lead: TBD Date: TBD
- [ ] QA Lead: TBD Date: TBD

**Comments:**

---

---

---

## Appendix

### Knowledge Base References

- `risk-governance.md` - Risk classification framework
- `probability-impact.md` - Risk scoring methodology
- `test-levels-framework.md` - Test level selection
- `test-priorities-matrix.md` - P0-P3 prioritization

### Related Documents

- PRD: `docs/product/PRD.md`
- Epic: `docs/epics.md`
- Architecture: `docs/architecture/ARCHITECTURE_OVERVIEW.md`
- Tech Spec: `docs/sprint-artifacts/tech-spec-dashboard-background-progress.md`

---

**Generated by**: BMad TEA Agent - Test Architect Module
**Workflow**: `.bmad/bmm/testarch/test-design`
**Version**: 4.0 (BMad v6)
