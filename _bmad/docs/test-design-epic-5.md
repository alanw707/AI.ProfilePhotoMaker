# Test Design: Epic 5 - Photo Enhancement (Weekly Credits)

**Date:** 2025-12-22T12:19:26-08:00
**Author:** Alan
**Status:** Draft

---

## Executive Summary

**Scope:** targeted test design for Epic 5

**Risk Summary:**

- Total risks identified: 5
- High-priority risks (>=6): 2
- Critical categories: BUS, DATA, TECH

**Coverage Summary:**

- P0 scenarios: 4 (8 hours)
- P1 scenarios: 4 (4 hours)
- P2/P3 scenarios: 3 (1.25 hours)
- **Total effort**: 13.25 hours (~1.7 days)

---

## Risk Assessment

### High-Priority Risks (Score >=6)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner | Timeline |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- | -------- |
| R5-001 | BUS | Weekly credit deduction/refund incorrect for enhancements | 2 | 3 | 6 | Credit ledger tests for enhance flows | QA/DEV | TBD |
| R5-002 | DATA | Enhancement output not persisted or linked to user | 2 | 3 | 6 | Output persistence tests | QA/DEV | TBD |

### Medium-Priority Risks (Score 3-4)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- |
| R5-003 | TECH | OpenAI enhancement fails without clear errors | 2 | 2 | 4 | Error handling tests | QA |
| R5-004 | DATA | Enhanced image stored in wrong path | 2 | 2 | 4 | Path validation tests | QA |

### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | Probability | Impact | Score | Action |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ------ |
| R5-005 | OPS | Enhancement endpoint slow under load | 1 | 2 | 2 | Monitor |

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

**Criteria**: Credit correctness + High risk (>=6)

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Replicate enhance consumes 1 weekly credit | API | R5-001 | 1 | QA | Credit balance updated |
| OpenAI enhance consumes 2 weekly credits | API | R5-001 | 1 | QA | Credit balance updated |
| Enhance failure refunds weekly credits | API | R5-001 | 1 | QA | Refund on error |
| Enhanced output persisted to user | Integration | R5-002 | 1 | QA | DB + file record |

**Total P0**: 4 tests, 8 hours

### P1 (High) - Run on PR to main

**Criteria**: Core enhancement flows

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| OpenAI enhance returns direct output | API | R5-003 | 1 | QA | Direct image output |
| Replicate enhance returns prediction wrapper | API | - | 1 | QA | Prediction status ID |
| Enhanced images stored in `/enhanced/{userId}` | Integration | R5-004 | 1 | QA | Path verification |
| Enhancement endpoint validates inputs | API | R5-003 | 1 | QA | Reject invalid input |

**Total P1**: 4 tests, 4 hours

### P2 (Medium) - Run nightly/weekly

**Criteria**: Secondary checks

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Enhancement history appears in gallery | API | R5-002 | 1 | QA | Gallery metadata |
| Weekly credit reset interaction | API | R5-001 | 1 | QA | Credits reset

**Total P2**: 2 tests, 1 hour

### P3 (Low) - Run on-demand

**Criteria**: Rare errors

| Requirement | Test Level | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- |
| Enhancement rate-limit error messaging | API | 1 | QA | User-friendly error |

**Total P3**: 1 test, 0.25 hours

---

## Execution Order

### Smoke Tests

- Replicate enhance consumes credits
- OpenAI enhance consumes credits

### P0 Tests

- Enhance failure refunds credits
- Enhanced output persisted to user

### P1 Tests

- OpenAI direct output returned
- Replicate prediction wrapper returned
- Enhanced image stored in correct path

### P2/P3 Tests

- Enhancement history appears in gallery
- Rate-limit error messaging

---

## Resource Estimates

### Test Development Effort (Planning Baseline)

| Priority | Count | Hours/Test | Total Hours | Notes |
| --- | --- | --- | --- | --- |
| P0 | 4 | 2.0 | 8 | Credit correctness |
| P1 | 4 | 1.0 | 4 | Core enhancement flows |
| P2 | 2 | 0.5 | 1 | Secondary checks |
| P3 | 1 | 0.25 | 0.25 | Rare errors |
| **Total** | **11** | **-** | **13.25** | **~1.7 days** |

### Prerequisites

**Test Data:**

- Users with weekly credits
- Sample images for enhancement

**Tooling:**

- xUnit for API tests (`AI.ProfilePhotoMaker.API.Tests`)
- Playwright API tests for enhancement flows (`AI.ProfilePhotoMaker.API/tests/playwright`)

**Environment:**

- OpenAI API key or mock for CI
- Replicate API token or mock for CI

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
- [ ] Business logic tests pass 100%

---

## Mitigation Plans

### R5-001: Weekly credit deduction/refund errors (Score: 6)

**Mitigation Strategy:** Credit ledger tests for Replicate and OpenAI enhancement flows.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** API tests for credit balance and usage logs

### R5-002: Enhancement output persistence (Score: 6)

**Mitigation Strategy:** Integration tests verifying stored output and gallery inclusion.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** DB + filesystem checks

---

## Assumptions and Dependencies

### Assumptions

1. Weekly credits are available for basic tier users.
2. Enhancement endpoints return standardized success/failure responses.
3. Existing tests cover some enhancement behaviors in `AI.ProfilePhotoMaker.API.Tests`.

### Dependencies

1. OpenAI and Replicate API credentials for integration tests.
2. Storage paths writable for enhanced images.

### Risks to Plan

- **Risk**: External API variability affects enhancement tests
  - **Impact**: Flaky integration tests
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
- Tech Spec: `docs/sprint-artifacts/tech-spec-openai-enhancement-credit-deduction.md`

---

**Generated by**: BMad TEA Agent - Test Architect Module
**Workflow**: `.bmad/bmm/testarch/test-design`
**Version**: 4.0 (BMad v6)
