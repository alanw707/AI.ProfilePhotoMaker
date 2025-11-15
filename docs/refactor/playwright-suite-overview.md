# Playwright Suite Overlap Assessment

> Status: Archived refactor note. Use the root `tests/e2e/README.md` and `AI.ProfilePhotoMaker.API/tests/playwright/README.md` as the primary references when working on Playwright tests.

## Repository-Level Suite (`tests/e2e`)
- Entry point: `tests/e2e/image-upload-validation.spec.js` with shared config `tests/e2e/playwright.config.js`.
- Focus: Production/staging smoke for image upload validation across desktop, cross-browser, and mobile devices.
- Execution model: Sequential (fullyParallel=false) with retries in CI, optional local `webServer` spin-up when `START_LOCAL_SERVER` set.
- Reporting: HTML + JSON outputs in `test-results/`, GitHub reporter enabled on CI.
- Dependencies: Reads `TEST_BASE_URL`, `STAGING_URL` env vars; no storage credentials required (validates response codes and basic flows).

## API Release Suite (`AI.ProfilePhotoMaker.API/tests/playwright`)
- Entry point: `AI.ProfilePhotoMaker.API/tests/playwright/tests/*` with configuration in `playwright.config.ts`.
- Coverage highlights:
  - Style preview lifecycle (`01-pre-upload-validation.spec.ts` → `04-style-preview-integration.spec.ts`).
  - Azure credential validation (`03-credential-validation.spec.ts`, `azure-config.ts`).
  - OAuth regression coverage (`08-oauth-production-validation.spec.ts`, `09-simple-oauth-check.spec.ts`, `oauth-final-test.spec.ts`).
  - Retention/cleanup verification for enhanced images (`enhanced-image-deletion-*.spec.ts`).
  - Training workflow diagnostics (`comprehensive-training-workflow-validation.spec.ts`).
  - Authenticated API smoke (`authenticated-request-test.spec.ts`, `auth-profile-completion-flow.spec.ts`).
- Execution profiles: Multiple npm scripts (`test`, `test:headed`, `test:performance`, etc.) with optional Azure credentials to unlock post-upload checks.
- Reporting: Playwright default output plus curated reports in `tests/test-results/` (HTML/trace retained).
- Dependencies: Azure Storage SAS/connection string, OAuth secrets, environment-specific URLs (documented in README and `.env.template`).

## Observations
- Suites target different scopes: root suite validates end-user journey, API suite validates backend release readiness and infrastructure.
- There is overlap on style preview validation (404 vs 200 checks) but API suite adds credential-dependent coverage.
- Both suites maintain their own configs/reporting; risk of drift in shared constants (style names, endpoints).

## Proposed Follow-Up (Low Priority Roadmap)
1. Introduce a shared catalog of style names/endpoints (`tests/shared/style-catalog.ts`) consumed by both suites.
2. Evaluate merging reporters to a single `test-results` root with sub-folders (`/e2e`, `/api-release`) to simplify artifact collection.
3. Create matrix documenting which CI jobs run each suite to avoid redundant production hits.
4. Consider housing API suite within root `tests/` via Playwright projects, while preserving credential-gated scripts.
