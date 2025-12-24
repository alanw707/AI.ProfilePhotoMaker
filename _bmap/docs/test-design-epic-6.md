# Test Design: Epic 6 - Credits, Payments, and Data Retention

**Date:** 2025-12-22T12:19:26-08:00
**Author:** Alan
**Status:** Draft

---

## Executive Summary

**Scope:** targeted test design for Epic 6

**Risk Summary:**

- Total risks identified: 6
- High-priority risks (>=6): 3
- Critical categories: BUS, SEC, DATA, OPS

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
| R6-001 | BUS | Stripe webhook not awarding credits after payment | 2 | 3 | 6 | Webhook processing tests | QA/DEV | TBD |
| R6-002 | SEC | Stripe webhook signature validation missing | 2 | 3 | 6 | Signature validation tests | QA/DEV | TBD |
| R6-003 | DATA | Retention cleanup deletes wrong files or leaves stale data | 2 | 3 | 6 | Retention cleanup tests | QA/DEV | TBD |

### Medium-Priority Risks (Score 3-4)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- |
| R6-004 | OPS | Weekly credit reset fails to run | 2 | 2 | 4 | Background service tests | QA |
| R6-005 | DATA | Credit history inaccurate or incomplete | 2 | 2 | 4 | History validation tests | QA |

### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | Probability | Impact | Score | Action |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ------ |
| R6-006 | OPS | Payment config endpoint misconfigured in dev | 1 | 2 | 2 | Monitor |

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

**Criteria**: Revenue and privacy critical paths

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Payment intent creation returns client secret | API | R6-001 | 1 | QA | Stripe PaymentIntent |
| Stripe webhook validates signature | API | R6-002 | 1 | QA | Reject invalid signature |
| Stripe webhook awards credits | Integration | R6-001 | 1 | QA | Credit balance updated |
| Credit status returns weekly + purchased | API | - | 1 | QA | Accurate balances |
| Retention cleanup removes expired originals | Integration | R6-003 | 1 | QA | 30-day policy |
| Retention cleanup removes expired generated | Integration | R6-003 | 1 | QA | 30-day policy |

**Total P0**: 6 tests, 12 hours

### P1 (High) - Run on PR to main

**Criteria**: Important supporting flows

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Credit history endpoint returns ledger | API | R6-005 | 1 | QA | Usage + purchases |
| Credit packages list returns pricing | API | - | 1 | QA | Public packages |
| Credit costs endpoint returns config | API | - | 1 | QA | Costs visible |
| Weekly credit reset job runs | Integration | R6-004 | 1 | QA | Reset behavior |
| Retention policy info endpoint | API | - | 1 | QA | Policy metadata |
| Payment config endpoint in dev | API | R6-006 | 1 | QA | Simulation toggle |

**Total P1**: 6 tests, 6 hours

### P2 (Medium) - Run nightly/weekly

**Criteria**: Edge cases and error handling

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Credit purchase history ordering | API | R6-005 | 1 | QA | Chronological order |
| Retention delete-expired endpoint | API | R6-003 | 1 | QA | Manual trigger |
| Payment intent failure handling | API | R6-001 | 1 | QA | Graceful failure |
| Credit status denies unauthenticated | API | R6-002 | 1 | QA | Auth guard |

**Total P2**: 4 tests, 2 hours

### P3 (Low) - Run on-demand

**Criteria**: Rare scenarios

| Requirement | Test Level | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- |
| Stripe webhook idempotency | API | 1 | QA | Duplicate event handling |
| Retention initialize dates endpoint | API | 1 | QA | Init behavior |

**Total P3**: 2 tests, 0.5 hours

---

## Execution Order

### Smoke Tests

- Payment intent creation returns client secret
- Credit status returns weekly + purchased

### P0 Tests

- Stripe webhook validates signature
- Stripe webhook awards credits
- Retention cleanup removes expired originals
- Retention cleanup removes expired generated

### P1 Tests

- Credit history endpoint returns ledger
- Credit packages list returns pricing
- Weekly credit reset job runs
- Payment config endpoint in dev

### P2/P3 Tests

- Retention delete-expired endpoint
- Payment intent failure handling
- Stripe webhook idempotency

---

## Resource Estimates

### Test Development Effort (Planning Baseline)

| Priority | Count | Hours/Test | Total Hours | Notes |
| --- | --- | --- | --- | --- |
| P0 | 6 | 2.0 | 12 | Revenue + privacy |
| P1 | 6 | 1.0 | 6 | Core supporting flows |
| P2 | 4 | 0.5 | 2 | Edge cases |
| P3 | 2 | 0.25 | 0.5 | Rare paths |
| **Total** | **18** | **-** | **20.5** | **~2.6 days** |

### Prerequisites

**Test Data:**

- Users with weekly and purchased credits
- Credit packages seeded
- Sample expired images for retention tests

**Tooling:**

- xUnit for API tests (`AI.ProfilePhotoMaker.API.Tests`)
- Playwright for webhook and integration checks (`AI.ProfilePhotoMaker.API/tests/playwright`)

**Environment:**

- Stripe webhook secret configured
- Background services enabled for reset and retention

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

### R6-001: Credits not awarded after payment (Score: 6)

**Mitigation Strategy:** Add webhook tests for successful payment event awarding credits.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** Webhook integration tests

### R6-002: Webhook signature validation missing (Score: 6)

**Mitigation Strategy:** Validate Stripe signature in API tests, reject invalid signatures.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** Signature validation tests

### R6-003: Retention cleanup errors (Score: 6)

**Mitigation Strategy:** Integration tests for retention policies and cleanup routines.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** Expired image deletion tests

---

## Assumptions and Dependencies

### Assumptions

1. PaymentIntent creation exists in all environments with simulation in dev/test.
2. Retention cleanup runs in background services.
3. Existing tests cover some payment paths in API and UI suites.

### Dependencies

1. Stripe webhook secret configured for tests.
2. Background services enabled in test environment.

### Risks to Plan

- **Risk**: Retention cleanup relies on scheduled jobs not easily testable in CI
  - **Impact**: Reduced confidence in retention enforcement
  - **Contingency**: Add manual retention endpoint tests

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
- Tech Spec: `docs/sprint-artifacts/tech-spec-email-deliverability-hotmail.md`

---

**Generated by**: BMad TEA Agent - Test Architect Module
**Workflow**: `.bmad/bmm/testarch/test-design`
**Version**: 4.0 (BMad v6)
