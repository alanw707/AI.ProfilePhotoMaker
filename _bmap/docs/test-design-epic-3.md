# Test Design: Epic 3 - Uploads, Training Data, and Gallery

**Date:** 2025-12-22T12:19:26-08:00
**Author:** Alan
**Status:** Draft

---

## Executive Summary

**Scope:** targeted test design for Epic 3

**Risk Summary:**

- Total risks identified: 6
- High-priority risks (>=6): 2
- Critical categories: SEC, DATA, PERF

**Coverage Summary:**

- P0 scenarios: 5 (10 hours)
- P1 scenarios: 5 (5 hours)
- P2/P3 scenarios: 5 (2 hours)
- **Total effort**: 17 hours (~2.1 days)

---

## Risk Assessment

### High-Priority Risks (Score >=6)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner | Timeline |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- | -------- |
| R3-001 | SEC | Path traversal or unsafe file paths allow unauthorized file access | 2 | 3 | 6 | Add path validation + delete safeguards | QA/DEV | TBD |
| R3-002 | DATA | Invalid files bypass validation and pollute storage | 2 | 3 | 6 | Strict validation tests for type/size/magic bytes | QA/DEV | TBD |

### Medium-Priority Risks (Score 3-4)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ---------- | ----- |
| R3-003 | DATA | Training ZIP created with <10 images | 2 | 2 | 4 | Enforce min count tests | QA |
| R3-004 | PERF | Large uploads or limits cause request failures | 2 | 2 | 4 | Limit and size tests | QA |

### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | Probability | Impact | Score | Action |
| ------- | -------- | ----------- | ----------- | ------ | ----- | ------ |
| R3-005 | OPS | Gallery URLs not normalized to absolute URLs | 1 | 2 | 2 | Monitor |
| R3-006 | DATA | Orphaned files remain after delete | 1 | 2 | 2 | Monitor |

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

**Criteria**: Upload integrity + High risk (>=6) + No workaround

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Upload rejects invalid types/sizes/magic bytes | API | R3-002 | 1 | QA | Validation enforcement |
| Upload enforces max 20 files | API | R3-004 | 1 | QA | Limit handling |
| Delete endpoint blocks path traversal | API | R3-001 | 1 | QA | Ownership + path safety |
| Delete removes file + DB record | Integration | R3-001 | 1 | QA | Verify filesystem cleanup |
| Training ZIP requires >=10 images | API | R3-003 | 1 | QA | Minimum count enforced |

**Total P0**: 5 tests, 10 hours

### P1 (High) - Run on PR to main

**Criteria**: Core gallery and training data flows

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Training ZIP creation success | API | R3-003 | 1 | QA | ZIP path + URL returned |
| Training ZIP list/get/delete endpoints | API | - | 2 | QA | ZIP lifecycle |
| Gallery returns absolute URLs | API | R3-005 | 1 | QA | URL normalization |
| Upload stores to correct path | Integration | R3-002 | 1 | QA | `/uploads/{userId}` |

**Total P1**: 5 tests, 5 hours

### P2 (Medium) - Run nightly/weekly

**Criteria**: Secondary edge cases

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| Gallery includes enhanced/generated flags | API | - | 1 | QA | Metadata completeness |
| Training ZIP delete cleans files | Integration | R3-006 | 1 | QA | No orphan ZIP |
| Upload failure returns safe error | API | R3-002 | 1 | QA | User-facing errors |

**Total P2**: 3 tests, 1.5 hours

### P3 (Low) - Run on-demand

**Criteria**: Rare behaviors

| Requirement | Test Level | Test Count | Owner | Notes |
| --- | --- | --- | --- | --- |
| Upload of webp images accepted | API | 1 | QA | Format coverage |
| Debug/repair endpoints guarded | API | 1 | QA | Dev-only access |

**Total P3**: 2 tests, 0.5 hours

---

## Execution Order

### Smoke Tests

- Upload rejects invalid types/sizes
- Training ZIP requires >=10 images

### P0 Tests

- Upload enforces max 20 files
- Delete blocks path traversal
- Delete removes file + DB record

### P1 Tests

- Training ZIP lifecycle
- Gallery returns absolute URLs
- Upload stores to correct path

### P2/P3 Tests

- Gallery metadata completeness
- Upload error handling
- Debug endpoints guarded

---

## Resource Estimates

### Test Development Effort (Planning Baseline)

| Priority | Count | Hours/Test | Total Hours | Notes |
| --- | --- | --- | --- | --- |
| P0 | 5 | 2.0 | 10 | Upload integrity |
| P1 | 5 | 1.0 | 5 | Core flows |
| P2 | 3 | 0.5 | 1.5 | Edge cases |
| P3 | 2 | 0.25 | 0.5 | Rare paths |
| **Total** | **15** | **-** | **17** | **~2.1 days** |

### Prerequisites

**Test Data:**

- Seeded users with storage directories
- Sample image fixtures (jpg/png/webp)

**Tooling:**

- xUnit for API tests (`AI.ProfilePhotoMaker.API.Tests`)
- Playwright for API-level checks (`AI.ProfilePhotoMaker.API/tests/playwright`)

**Environment:**

- File storage writable on test environment
- DB seeded with user profiles

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

### R3-001: Path traversal/unsafe file paths (Score: 6)

**Mitigation Strategy:** Validate file paths, enforce user scoping, add deletion tests.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** API tests for delete and path validation

### R3-002: Invalid files bypass validation (Score: 6)

**Mitigation Strategy:** Add strict validation tests for file types, sizes, and magic bytes.  
**Owner:** QA/DEV  
**Timeline:** TBD  
**Status:** Planned  
**Verification:** Upload validation tests

---

## Assumptions and Dependencies

### Assumptions

1. Upload endpoints return normalized absolute URLs.
2. Training ZIP endpoints exist and are authorized per user.
3. Existing tests in `AI.ProfilePhotoMaker.API.Tests` cover some image flows.

### Dependencies

1. File storage path is writable in CI/staging.
2. Seed data utilities for image fixtures.

### Risks to Plan

- **Risk**: Storage abstraction differs between local and blob storage
  - **Impact**: Test parity issues
  - **Contingency**: Separate storage adapter tests

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
