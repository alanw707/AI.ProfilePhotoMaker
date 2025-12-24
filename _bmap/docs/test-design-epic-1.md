# Test Design: Epic 1 - Foundation & Core Platform Setup

**Date:** 2025-12-22T12:19:26-08:00
**Author:** Alan
**Status:** Draft

---

## Executive Summary

**Scope:** targeted test design for Epic 1

**Risk Summary:**

- Total risks identified: 6
- High-priority risks (>=6): 2
- Critical categories: TECH, DATA, OPS

**Coverage Summary:**

- P0 scenarios: 4 (8 hours)
- P1 scenarios: 4 (4 hours)
- P2/P3 scenarios: 4 (1.75 hours)
- **Total effort**: 13.75 hours (~1.7 days)

---

## Risk Assessment

### High-Priority Risks (Score >=6)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner | Timeline |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- | -------- |
| R1-001 | TECH | Environment configuration misread causes API to boot with invalid settings | 2 | 3 | 6 | Add startup validation + config tests | DEV/OPS | TBD |
| R1-002 | DATA | Migration failure or schema drift prevents startup or corrupts core tables | 2 | 3 | 6 | Run migrations in CI + startup migration smoke tests | DEV/QA | TBD |

### Medium-Priority Risks (Score 3-4)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- |
| R1-003 | OPS | Health checks report OK while DB is unreachable | 2 | 2 | 4 | Integration tests for `/health` and `/health/db` | QA |
| R1-004 | SEC | Errors expose stack traces or sensitive config in production | 1 | 3 | 3 | Verify exception middleware + log redaction | DEV |

### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | Probability | Impact | Score | Action |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ------ |
| R1-005 | OPS | Background services not registered in all environments | 1 | 2 | 2 | Monitor |
| R1-006 | PERF | Startup diagnostics slow API start under debug configs | 1 | 1 | 1 | Monitor |

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

**Criteria**: Blocks core platform + High risk (>=6) + No workaround

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Health check endpoint responds | API | R1-003 | 1 | QA | `/health` returns 200 in healthy state |
| DB health check fails when DB down | API | R1-003 | 1 | QA | `/health/db` reflects DB outage |
| Startup migration applies cleanly | Integration | R1-002 | 1 | QA | DB schema ready at boot |
| Standard error response in prod | API | R1-004 | 1 | QA | No stack traces in prod responses |

**Total P0**: 4 tests, 8 hours

### P1 (High) - Run on PR to main

**Criteria**: Important infrastructure behavior + Medium risk (3-4)

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Config validation rejects missing keys | Unit | R1-001 | 1 | DEV | Validate required settings |
| Storage path resolver returns safe paths | Unit | - | 1 | DEV | Prevent path traversal |
| Background services registered | Integration | R1-005 | 1 | QA | Hosted services present |
| Logging redacts secrets | Unit | R1-004 | 1 | DEV | Ensure PII/secret masking |

**Total P1**: 4 tests, 4 hours

### P2 (Medium) - Run nightly/weekly

**Criteria**: Secondary checks + Low risk

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Telemetry headers present | API | - | 1 | QA | Trace/correlation headers exist |
| Startup diagnostics do not fail | Integration | R1-006 | 1 | QA | Non-blocking diagnostics |
| Response compression enabled | API | - | 1 | QA | Compression headers on large payload |

**Total P2**: 3 tests, 1.5 hours

### P3 (Low) - Run on-demand

**Criteria**: Nice-to-have diagnostics

| Requirement | Test Level | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- |
| Optional startup checks logged | Unit | 1 | DEV | Diagnostics log present |

**Total P3**: 1 test, 0.25 hours

---

## Execution Order

### Smoke Tests

- Health check endpoint responds
- DB health check reflects outage

### P0 Tests

- Startup migration applies cleanly
- Standard error response in prod

### P1 Tests

- Config validation rejects missing keys
- Storage path resolver returns safe paths
- Background services registered
- Logging redacts secrets

### P2/P3 Tests

- Telemetry headers present
- Response compression enabled
- Optional startup checks logged

---

## Resource Estimates

### Test Development Effort (Planning Baseline)

| Priority | Count | Hours/Test | Total Hours | Notes |
| --- | --- | --- | --- | --- |
| P0 | 4 | 2.0 | 8 | Platform-critical |
| P1 | 4 | 1.0 | 4 | Core infra coverage |
| P2 | 3 | 0.5 | 1.5 | Secondary checks |
| P3 | 1 | 0.25 | 0.25 | Optional diagnostics |
| **Total** | **12** | **-** | **13.75** | **~1.7 days** |

### Prerequisites

**Test Data:**

- Minimal seeded database for health/migration tests
- Config fixtures for valid and invalid settings

**Tooling:**

- xUnit for API unit/integration tests (`AI.ProfilePhotoMaker.API.Tests`)
- Playwright for smoke/API checks (API Playwright tests)

**Environment:**

- API + database available (SQLite dev or SQL Server)
- Environment variables for config validation scenarios

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

### R1-001: Config validation failures (Score: 6)

**Mitigation Strategy:** Add startup config validation with clear error reporting and unit tests for missing/invalid keys.  
**Owner:** DEV/OPS  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** Unit tests + boot-time validation check

### R1-002: Migration failure or drift (Score: 6)

**Mitigation Strategy:** Run migrations in CI and add integration test that verifies schema creation on clean database.  
**Owner:** DEV/QA  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** CI migration step + integration test

---

## Assumptions and Dependencies

### Assumptions

1. API uses standardized exception middleware in production.
2. Health checks exist for both API and DB.
3. Existing test suites are available in `AI.ProfilePhotoMaker.API.Tests` and `AI.ProfilePhotoMaker.API/tests/playwright`.

### Dependencies

1. Valid environment configuration for each deployment target.
2. Database connectivity for migration and health tests.

### Risks to Plan

- **Risk**: Startup validation missing for new env vars
  - **Impact**: Misconfigured deployments
  - **Contingency**: Add config validation tests per environment

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
