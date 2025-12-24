# Test Design: Epic 4 - Styles, Model Training, and Generation

**Date:** 2025-12-22T12:19:26-08:00
**Author:** Alan
**Status:** Draft

---

## Executive Summary

**Scope:** targeted test design for Epic 4

**Risk Summary:**

- Total risks identified: 7
- High-priority risks (>=6): 4
- Critical categories: BUS, SEC, DATA, TECH

**Coverage Summary:**

- P0 scenarios: 7 (14 hours)
- P1 scenarios: 6 (6 hours)
- P2/P3 scenarios: 6 (2.5 hours)
- **Total effort**: 22.5 hours (~2.8 days)

---

## Risk Assessment

### High-Priority Risks (Score >=6)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner | Timeline |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- | -------- |
| R4-001 | BUS | Credit consumption/refund incorrect for training or generation | 2 | 3 | 6 | Credit ledger tests for train/generate | QA/DEV | TBD |
| R4-002 | SEC | Replicate webhook signature validation missing or incorrect | 2 | 3 | 6 | Signature validation tests | QA/DEV | TBD |
| R4-003 | DATA | Webhook processing fails to persist generated images | 2 | 3 | 6 | Webhook persistence tests | QA/DEV | TBD |
| R4-004 | TECH | Training polling fails to transition model to READY | 2 | 3 | 6 | Polling tests + status transitions | QA/DEV | TBD |

### Medium-Priority Risks (Score 3-4)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- |
| R4-005 | PERF | Batch generation stalls or times out under load | 2 | 2 | 4 | Batch generation API tests | QA |
| R4-006 | DATA | Style selection not persisted correctly | 2 | 2 | 4 | Selection persistence tests | QA |

### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | Probability | Impact | Score | Action |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ------ |
| R4-007 | OPS | Generation status endpoint returns stale data | 1 | 2 | 2 | Monitor |

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

**Criteria**: Core generation path + High risk (>=6)

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Training consumes 15 purchased credits | API | R4-001 | 1 | QA | Credit ledger check |
| Retrain blocked when model READY | API | R4-004 | 1 | QA | Prevent duplicate training |
| Training status transitions to READY | Integration | R4-004 | 1 | QA | Polling updates status |
| Generation consumes credits per output | API | R4-001 | 1 | QA | 5 credits per output |
| Webhook signature validation enforced | API | R4-002 | 1 | QA | Invalid signature rejected |
| Webhook persists generated images | Integration | R4-003 | 1 | QA | DB + file records |
| Generation status endpoint returns output | API | R4-007 | 1 | QA | Output URLs returned |

**Total P0**: 7 tests, 14 hours

### P1 (High) - Run on PR to main

**Criteria**: Important feature flows

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Style catalog returns templates | API | - | 1 | QA | Prompt and negative prompt |
| User style selection persists | API | R4-006 | 1 | QA | Persist + fetch |
| Batch generation request enqueues jobs | API | R4-005 | 1 | QA | Pending generation created |
| Generation status error handling | API | R4-007 | 1 | QA | Failed prediction surfaces |
| Training request fails with insufficient credits | API | R4-001 | 1 | QA | No credit consumption |
| Style selection ownership enforced | API | R4-006 | 1 | QA | User scoping |

**Total P1**: 6 tests, 6 hours

### P2 (Medium) - Run nightly/weekly

**Criteria**: Secondary behaviors

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Batch generation refunds failed styles | Integration | R4-001 | 1 | QA | Partial refund logic |
| Training status endpoint handles missing ID | API | R4-004 | 1 | QA | 404/validation |
| Style template lookup by name | API | - | 1 | QA | `/api/style/name/{name}` |
| Generation output count bounds enforced | API | R4-005 | 1 | QA | 1-4 outputs |

**Total P2**: 4 tests, 2 hours

### P3 (Low) - Run on-demand

**Criteria**: Rare paths

| Requirement | Test Level | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- |
| Prediction list endpoint returns metadata | API | 1 | QA | If present in API |
| Model discovery sync job logs | Integration | 1 | QA | Non-blocking checks |

**Total P3**: 2 tests, 0.5 hours

---

## Execution Order

### Smoke Tests

- Training consumes credits
- Webhook signature validation enforced

### P0 Tests

- Retrain blocked when READY
- Training status transitions to READY
- Webhook persists generated images
- Generation consumes credits
- Generation status returns output

### P1 Tests

- Style catalog returns templates
- User style selection persists
- Batch generation enqueues jobs
- Training insufficient credits fails

### P2/P3 Tests

- Batch generation refunds failed styles
- Generation output count bounds enforced
- Prediction metadata endpoint (if applicable)

---

## Resource Estimates

### Test Development Effort (Planning Baseline)

| Priority | Count | Hours/Test | Total Hours | Notes |
| --- | --- | --- | --- | --- |
| P0 | 7 | 2.0 | 14 | Credit + webhook critical |
| P1 | 6 | 1.0 | 6 | Core feature coverage |
| P2 | 4 | 0.5 | 2 | Edge cases |
| P3 | 2 | 0.25 | 0.5 | Rare paths |
| **Total** | **19** | **-** | **22.5** | **~2.8 days** |

### Prerequisites

**Test Data:**

- Seeded styles, users, and credits
- Mock Replicate training/prediction responses

**Tooling:**

- xUnit for API tests (`AI.ProfilePhotoMaker.API.Tests`)
- Playwright for webhook and integration flows (`AI.ProfilePhotoMaker.API/tests/playwright`)

**Environment:**

- Replicate tokens configured or mocked
- Webhook endpoints reachable in staging

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

### R4-001: Credit consumption/refund errors (Score: 6)

**Mitigation Strategy:** Credit ledger tests for training/generation including refunds.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** API tests for credit balances and usage logs

### R4-002: Webhook signature validation (Score: 6)

**Mitigation Strategy:** Add webhook tests for valid/invalid signatures.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** Webhook validation tests

### R4-003: Webhook persistence failures (Score: 6)

**Mitigation Strategy:** Integration tests verifying persisted images and retention scheduling.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** DB + filesystem checks

### R4-004: Training polling failures (Score: 6)

**Mitigation Strategy:** Polling tests to confirm READY status transition.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** Training status endpoint tests

---

## Assumptions and Dependencies

### Assumptions

1. Credit costs are configured per PRD (training 15, generation 5 per output).
2. Replicate webhook signature validation uses HMAC with a time window.
3. Existing API tests provide some coverage of Replicate integration flows.

### Dependencies

1. Replicate API token or mock available for CI.
2. Webhook secret configured in test environment.

### Risks to Plan

- **Risk**: External Replicate outages affect integration tests
  - **Impact**: Flaky tests
  - **Contingency**: Use mock clients in CI

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
