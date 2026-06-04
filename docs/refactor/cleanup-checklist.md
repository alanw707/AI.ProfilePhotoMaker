# Cleanup & Refactor Readiness Checklist

> Status: Archived planning checklist. Treat this as historical guidance; for current work, rely on `PROJECT_PLAN.md`, `DEVELOPMENT_BACKLOG.md`, and the scripts under `scripts/`.

## Approach & Safety Principles
- Inventory only: this document captures investigation targets before touching code.
- Sequence work by risk: start with isolated folders/files, end with cross-cutting runtime paths.
- Require green automation before and after any change: `dotnet build`, `dotnet test`, `npm run lint`, `npm test`, `npm run test:e2e`, and relevant Playwright suites.
- Snapshot telemetry: gather baseline metrics/logging so we can detect regressions after cleanup.

## Global Verification Prerequisites
1. Run `./dev-start.sh` to ensure local stack is healthy before removing anything.
2. Capture API smoke results via `dotnet test AI.ProfilePhotoMaker.API.Tests`.
3. Capture UI smoke via `npm run quality:check` and `npm run test:e2e` (after `npm run playwright:install`).
4. Document environment settings used for validation (notably storage, webhook tunnels, feature flags).
5. For any item touching deployment/self-hosted scripts, dry-run `./scripts/validate-*.sh` helpers.

## Priority: High (Quick Wins / Low Risk)
- [x] **Evaluate empty legacy project folder** *(API)*  
  - Location: `AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API` (currently only `obj/`).  
  - Rationale: Vestigial directory increases confusion during onboarding.  
  - Action: Confirm `.csproj` and solution references do not rely on this folder, then remove or document necessity.  
  - Validation: `dotnet build AI.ProfilePhotoMaker.sln` and smoke run of API.  
  - Resolution (Feb 2025): Confirmed folder only contains git-ignored build artifacts; no repository changes required beyond routine cleanup.
- [x] **Retire deprecated style preview URL helper** *(UI)*  
  - Location: `AI.ProfilePhotoMaker.UI/src/app/services/config.service.ts` (legacy helper path).  
  - Rationale: Method returns placeholder URL and is superseded by `StylePreviewService`.  
  - Action: Verify no templates/tests call `buildStylePreviewUrl`, remove helper, update docs.  
  - Validation: `npm test`, `npm run test:e2e` focusing on style previews.  
  - Resolution (Feb 2025): Removed `buildStylePreviewUrl`; templates now depend on `StylePreviewService` exclusively.
- [x] **Decommission dormant Cypress suite** *(UI tooling)*  
  - Location: `AI.ProfilePhotoMaker.UI/cypress/e2e/file-upload-icon-positioning.cy.ts`.  
  - Rationale: Playwright replaced Cypress; retaining folder adds maintenance overhead.  
  - Action: Confirm CI/docs do not reference Cypress, archive any findings, delete folder.  
  - Validation: Search `.github/` workflows and `package.json` to ensure no Cypress calls remain.  
  - Resolution (Feb 2025): Removed `cypress/e2e/file-upload-icon-positioning.cy.ts`; remaining automation relies on Playwright paths.
- [x] **Clean empty Angular scaffolding directories** *(UI)*  
  - Locations: `AI.ProfilePhotoMaker.UI/tests/`, `AI.ProfilePhotoMaker.UI/e2e/` (host smoke specs).  
  - Rationale: Reduces clutter and confusion for new contributors.  
  - Action: Ensure Angular CLI schematics do not target these paths; remove or add README if required.  
  - Validation: `npm run lint` and `ng generate component` smoke test.  
  - Resolution (Feb 2025): Directories now host targeted smoke specs; documented to avoid removal during ongoing investigations.
- [x] **Confirm empty `tools/` directory necessity** *(Repo root)*  
  - Location: `tools/` (documented placeholder).  
  - Rationale: Clarify whether this is intentional placeholder.  
  - Action: Add README explaining purpose or remove the directory.  
  - Validation: None—documentation update only.  
  - Resolution (Feb 2025): Added `tools/README.md` outlining intended use so contributors know when to populate or remove the folder.

## Priority: Medium (Coordinated Cleanups)
- [x] **Review console logging left in production services** *(UI)*  
  - Examples: `AI.ProfilePhotoMaker.UI/src/app/services/file-upload.service.ts:445-509`, `.../services/removed legacy workflow orchestration service:384-845`, `.../pages/premium/premium.component.ts:162-185`.  
  - Rationale: Verbose logs can leak sensitive data and clutter prod consoles.  
  - Action: Route critical diagnostics through `LoggingService`; guard or remove noisy logs.  
  - Validation: Manual QA around uploads, workflow routing, premium purchase flows.  
  - Resolution (Feb 2025): Replaced ad-hoc `console` usage with `LoggingService` debug/warn/error hooks across file-upload, workflow orchestration, and premium flows; debug output now honors feature flags.
- [ ] **Prune test artifacts and empty result folders** *(Tests)*  
  - Location: `AI.ProfilePhotoMaker.API/tests/playwright/tests/test-results/` (empty).  
  - Rationale: Avoid tracking empty directories unless required by CI.  
  - Action: Remove if unnecessary; rely on runtime creation.  
  - Validation: `npx playwright test` to confirm reports still emit.
- [ ] **Remove unused Claude docs stub** *(UI docs)*  
  - Location: `AI.ProfilePhotoMaker.UI/ClaudeDocs/Report/` (empty).  
  - Rationale: Abandoned artifact increases repository noise.  
  - Action: Confirm with doc owners, delete or repurpose directory, update indices.  
  - Validation: Ensure `docs/claudedocs-index.md` does not reference the path.
- [ ] **Confirm social login follow-ups should stay visible** *(UI auth)*  
  - Locations: `AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.ts:211-216`, `.../register/register.component.ts:135-140`.  
  - Rationale: Persistent reminders indicate roadmap uncertainty.  
  - Action: Sync with product roadmap; either convert to tracked issues or keep TODOs intentionally.  
  - Validation: None—communication/documentation task.
- [ ] **Finish rollback logic for unified secrets deployment** *(Scripts)*  
  - Location: `scripts/deploy-with-unified-secrets.sh:179`.  
  - Rationale: Leaving placeholders without owners blocks rollout readiness.  
  - Action: Design rollback flow or document manual steps; test via dry-run.  
  - Validation: Execute script in staging; verify failure paths restore prior state.
- [ ] **Review shell duplicates for API/UI control** *(Scripts)*  
  - Locations: `scripts/api-start.sh`, `scripts/api-start.ps1`, matching stop/restart scripts.  
  - Rationale: Maintaining Bash and PowerShell variants may be unnecessary.  
  - Action: Survey developer usage; if PowerShell variants unused, plan deprecation and update onboarding docs.  
  - Validation: Confirm Windows contributors have alternative workflow before removal.

## Priority: Low / Strategic (High Coordination or Long Tail)
- [ ] **Deprecate legacy webhook/auth fallbacks once config parity confirmed** *(API)*  
  - Locations: `AI.ProfilePhotoMaker.API/Services/WebhookUrlResolver.cs:170`, `.../Services/Authentication/AuthService.cs:115`.  
  - Rationale: Backward-compatibility paths complicate config management.  
  - Action: Audit active environments; if legacy keys unused, remove behind feature flag or telemetry toggle.  
  - Validation: Exercise OAuth/webhook flows via ngrok per `docs/webhooks/INTEGRATION.md`.  
  - Status: `LegacyCompatibilityOptions` now gate fallbacks with structured logging (Jan 2025).
- [ ] **Assess retention of legacy enhanced file paths** *(API storage)*  
  - Location: `AI.ProfilePhotoMaker.API/Services/RetentionPolicyService.cs:254-280`.  
  - Rationale: Cleanup job maintains both prefixed and legacy paths; removal reduces complexity.  
  - Action: Confirm storage no longer writes legacy paths; migrate job to single path.  
  - Validation: Run retention policy integration tests and storage cleanup scripts in staging.  
  - Status: Legacy scan wrapped in `LegacyCompatibilityOptions.EnableLegacyEnhancedPathLookup`; logging added when disabled (Jan 2025).
- [ ] **Audit legacy status bridging layer** *(UI model status)*  
  - Locations: `AI.ProfilePhotoMaker.UI/src/app/models/app/enhance.types.ts:73-161`, `.../services/model-status-mapper.service.ts:41-360`, `.../services/model-state.service.ts:234-256`.  
  - Rationale: Extensive conversion logic keeps `legacyStatus` strings alive; cleanup requires API alignment.  
  - Action: Coordinate with API team, gather telemetry on `legacyStatus` usage, plan phased removal.  
  - Validation: Instrument UI logs/metrics before deleting pathways.  
  - Status: Mapper emits a one-time warning + debug traces when legacy adapters run (Jan 2025).
- [ ] **Deduplicate Playwright suites** *(Cross-stack testing)*  
  - Locations: `tests/e2e/` vs `AI.ProfilePhotoMaker.API/tests/playwright/`.  
  - Rationale: Dual suites risk drift; consolidation could reduce maintenance.  
  - Action: Chart coverage per suite, evaluate shared configs or consolidation.  
  - Validation: Run both suites post-change; ensure Azure credential-dependent tests remain isolated.  
  - Status: Coverage and dependency summary captured in `docs/refactor/playwright-suite-overview.md` (Jan 2025).
