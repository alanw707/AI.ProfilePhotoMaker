---
title: 'Fix Style-to-Prompt Routing and Casual Clothing Guardrails'
slug: 'fix-style-prompt-routing-casual-clothing-guardrails'
created: '2026-02-20T09:23:13-08:00'
status: 'Completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack:
  - C# / .NET 8 (ASP.NET Core Web API)
  - Entity Framework Core + SQL Server
  - Angular 18 + TypeScript
  - Replicate API (FLUX training/generation)
  - SignalR + webhook completion flow
files_to_modify:
  - AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs
  - AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs
  - AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs
  - AI.ProfilePhotoMaker.API/Controllers/StyleController.cs
  - AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs
  - AI.ProfilePhotoMaker.API/Models/ProcessedImage.cs
  - AI.ProfilePhotoMaker.API/Models/DTOs/GenerateImagesRequestDto.cs
  - AI.ProfilePhotoMaker.UI/src/app/services/workflow-orchestration.service.ts
  - AI.ProfilePhotoMaker.UI/src/app/services/replicate.service.ts
  - AI.ProfilePhotoMaker.UI/src/app/pages/gallery/gallery.component.ts
  - AI.ProfilePhotoMaker.UI/src/app/components/photo-gallery/photo-gallery.component.ts
  - AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientNegativePromptTests.cs
  - AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientStyleTuningTests.cs
  - AI.ProfilePhotoMaker.API.Tests/Unit/StylePromptTokenValidationTests.cs
  - AI.ProfilePhotoMaker.API/Migrations/20260220132108_FixStylePromptsDataDriftAndQualityAudit.cs
  - AI.ProfilePhotoMaker.API/Migrations/20260220162443_StrengthenCasualStyleClothingPrompt.cs
code_patterns:
  - Generation path is style-name based (not style-id based): UI submits style names in /replicate/generate/batch; API resolves prompts by style Name in ReplicateApiClient.GetStylePromptsFromDatabase().
  - Persisted gallery style label comes from webhook payload input.style and is stored in ProcessedImages.Style.
  - Style selections are persisted by id in UserStyleSelections, but generation currently uses selected style names from UI state.
  - Prompt fallback is linkedin when requested style not found.
  - Gallery card title derives from ProcessedImages.Style and formatStyleName(); no server-side style normalization at display time.
  - Progress workflow uses a mixed polling/webhook model: frontend tracks prediction IDs while webhook persists completed images.
test_patterns:
  - ReplicateApiClient unit tests capture outbound prediction payload and assert prompt/negative_prompt/tuning fields.
  - Style seed validation tests enforce anti-nudity and quality baseline terms across active styles.
  - Style tuning tests validate pro/casual/default guidance and steps by style group.
  - No current integration test validates style label consistency from prediction request through webhook persistence to gallery rendering.
---

# Tech-Spec: Fix Style-to-Prompt Routing and Casual Clothing Guardrails

**Created:** 2026-02-20T09:23:13-08:00

## Overview

### Problem Statement

Generated outputs are not consistently following selected styles; casual frequently produces fitness-like or underdressed looks (shirtless/tank top), and fitness can produce unrelated styles, indicating prompt/style resolution or mapping defects.

### Solution

Audit and harden end-to-end style resolution (selection -> prompt lookup -> generation -> card association), then enforce stricter casual clothing constraints in prompt rules and validation so casual cannot emit shirtless/tank-top outputs.

### Scope

**In Scope:**
- Investigate style selection/request payload mapping to backend.
- Verify backend style lookup and fallback behavior per image.
- Verify prompt composition and per-style negative guardrails for casual/fitness.
- Verify generated image-to-style metadata association used by UI cards.
- Add targeted tests for style fidelity and casual clothing constraints.

**Out of Scope:**
- New style creation/redesign.
- Broad UI redesign.
- Non-style generation quality tuning unrelated to mapping/guardrails.

## Context for Development

### Codebase Patterns

- API generation uses style names end-to-end in batch mode (`GenerateBatchImagesRequestDto.Styles`); prompt resolution is `Styles.Name` lookup in `ReplicateApiClient.GetStylePromptsFromDatabase()`.
- Webhook persistence writes `ProcessedImages.Style = input.style` directly from prediction payload; gallery labels are derived from this stored style string.
- Selection endpoint stores style IDs (`UserStyleSelections`) but generation itself is driven by selected style names from frontend state, creating potential id/name drift blind spots.
- Current local DB has migration drift: latest two prompt migrations are not applied, so runtime prompt content can diverge from current source code and tests.
- User expectation is strict casual clothing constraints: casual must not produce shirtless or tank-top/undershirt outputs.
- Fitness should preserve fitness intent and avoid cross-style outputs (e.g., edgy-urban streetwear).

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs` | Entry points for single + batch generation; passes style names to Replicate client. |
| `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs` | Prompt lookup, fallback behavior, prompt assembly, tuning, and prediction payload creation. |
| `AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs` | Writes generated rows with webhook `input.style`; potential style-label source of truth mismatch point. |
| `AI.ProfilePhotoMaker.API/Controllers/StyleController.cs` | Active styles and selection persistence by style ID. |
| `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs` | Seed style IDs/prompts; source-of-truth for migrations/tests. |
| `AI.ProfilePhotoMaker.API/Models/DTOs/GenerateImagesRequestDto.cs` | Confirms batch request uses `List<string> Styles`. |
| `AI.ProfilePhotoMaker.API/Models/ProcessedImage.cs` | Gallery record model containing persisted style label. |
| `AI.ProfilePhotoMaker.UI/src/app/services/workflow-orchestration.service.ts` | Builds batch generation request from selected style names and tracks prediction IDs. |
| `AI.ProfilePhotoMaker.UI/src/app/services/replicate.service.ts` | API client for `/replicate/generate/batch` and polling endpoints. |
| `AI.ProfilePhotoMaker.UI/src/app/pages/gallery/gallery.component.ts` | Converts `ProcessedImage.style` to card title shown to user. |
| `AI.ProfilePhotoMaker.UI/src/app/components/photo-gallery/photo-gallery.component.ts` | Rendering, style label formatting, and card metadata display. |
| `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientNegativePromptTests.cs` | Verifies fallback and negative prompt composition behaviors. |
| `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientStyleTuningTests.cs` | Verifies style tuning group resolution and prompt modifiers. |
| `AI.ProfilePhotoMaker.API.Tests/Unit/StylePromptTokenValidationTests.cs` | Enforces style seed guardrails including anti-nudity and quality baselines. |

### Technical Decisions

- Treat runtime DB state as first-class investigation target for this issue; do not assume source seed/migrations are applied.
- Add a verification gate that compares code-expected style ids/prompts against live `Styles` table before generation debugging conclusions.
- Prioritize style-fidelity instrumentation: log requested style, resolved style row, and persisted `ProcessedImages.Style` per prediction.
- Add explicit casual anti-underdress guardrails (shirtless/tank-top/undershirt negatives) at source seed + migration level, then verify live DB parity.
- Define a style-consistency contract test: requested style -> webhook `input.style` -> persisted style -> gallery label must match.
- Keep fallback behavior deterministic (`linkedin`) and observable in logs/tests to prevent hidden style substitutions.

## Implementation Plan

### Tasks

- [x] Task 1: Add runtime style-resolution instrumentation in generation path
  - File: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs`
  - Action: Log requested style name, resolved DB style name/id, fallback usage, and final prompt source per prediction request.
  - Notes: Keep logs sanitized; include prediction id/user id correlation for webhook traceability.

- [x] Task 2: Enforce style existence validation for batch requests before generation
  - File: `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`
  - Action: Validate each requested style against active styles; return structured error for invalid names and prevent silent fallback in batch mode unless explicitly intended.
  - Notes: This reduces accidental cross-style output from stale UI names or malformed payloads.

- [x] Task 3: Add durable style metadata to prediction persistence
  - File: `AI.ProfilePhotoMaker.API/Models/Prediction.cs`
  - Action: Add fields for `RequestedStyle`, `ResolvedStyle`, and optional `ResolvedStyleId` (or equivalent) with migration support.
  - Notes: Enables deterministic webhook mapping and post-mortem analysis without parsing logs.

- [x] Task 4: Persist resolved style (not raw input) in webhook-created generated images
  - File: `AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs`
  - Action: Resolve persisted `ProcessedImages.Style` from trusted prediction metadata (Task 3) and fall back safely when missing.
  - Notes: Prevents UI card labels from drifting when webhook payload style is stale/incorrect.

- [x] Task 5: Introduce style-name normalization contract
  - File: `AI.ProfilePhotoMaker.API/Controllers/StyleController.cs`
  - Action: Add/centralize normalization helper for style names (trim, case-insensitive canonical match) used by selection and generation boundaries.
  - Notes: Keep matching deterministic and avoid multi-source normalization differences.

- [x] Task 6: Patch style seed parity and casual clothing guardrails
  - File: `AI.ProfilePhotoMaker.API/Data/ApplicationDbContext.cs`
  - Action: Ensure style ID/name parity with live DB expectations and strengthen `casual` prompt negatives for tank-top/undershirt/underdress exclusions.
  - Notes: Align with business rule: casual must be fully clothed upper-body portraits.

- [x] Task 7: Add migration to reconcile live DB style records
  - File: `AI.ProfilePhotoMaker.API/Migrations/` (new migration + snapshot updates)
  - Action: Apply style parity fixes and prompt-template updates to runtime DB, including ID/name correction where required and casual negative prompt strengthening.
  - Notes: Current DB is missing latest style-fix migrations; this task must include safe data updates and rollback behavior.

- [x] Task 8: Stabilize UI generation request style mapping
  - File: `AI.ProfilePhotoMaker.UI/src/app/services/workflow-orchestration.service.ts`
  - Action: Build batch request styles from canonical API-provided style names; prevent duplicate or stale style names from being submitted.
  - Notes: Keep selected style id/name mapping explicit in memory to avoid accidental mismatches.

- [x] Task 9: Add gallery label integrity checks and fallback display behavior
  - File: `AI.ProfilePhotoMaker.UI/src/app/pages/gallery/gallery.component.ts`
  - Action: Detect unknown style labels, show explicit fallback label, and optionally surface diagnostics tag in debug mode.
  - Notes: Avoid presenting wrong style title as if correct when style string is invalid.

- [x] Task 10: Add backend unit tests for style fidelity contract
  - File: `AI.ProfilePhotoMaker.API.Tests/Unit/ReplicateApiClientNegativePromptTests.cs`
  - Action: Add tests asserting style validation behavior, fallback behavior, and resolved-style metadata capture.
  - Notes: Include explicit casual/fitness regression fixtures.

- [x] Task 11: Add seed and tuning regression tests for casual + fitness boundaries
  - File: `AI.ProfilePhotoMaker.API.Tests/Unit/StylePromptTokenValidationTests.cs`
  - Action: Add assertions that casual negatives include anti-shirtless + anti-tank-top terms and that fitness remains intentionally distinct.
  - Notes: Keep intentional exceptions documented in test comments.

- [x] Task 12: Add end-to-end integration test for style round-trip
  - File: `AI.ProfilePhotoMaker.API.Tests/Integration/ReplicateMockIntegrationTests.cs`
  - Action: Validate requested style -> prediction metadata -> webhook persistence -> stored `ProcessedImages.Style` round-trip consistency.
  - Notes: This closes the current testing blind spot for label mismatches seen in gallery.

### Acceptance Criteria

- [ ] AC 1: Given a batch generation request containing valid styles (`casual`, `fitness`), when generation starts, then each prediction records both requested and resolved style metadata for traceability.
- [ ] AC 2: Given a batch request with an unknown style name, when the API receives it, then the request is rejected with a clear validation error and no generation call is made for that style.
- [ ] AC 3: Given a completed prediction webhook, when generated images are persisted, then `ProcessedImages.Style` matches the resolved style metadata for that prediction (not an untrusted/raw label).
- [ ] AC 4: Given style `casual`, when prompts are resolved, then negative prompts include anti-shirtless and anti-tank-top/undershirt guard terms.
- [ ] AC 5: Given style `fitness`, when prompts are resolved, then fitness-specific prompt intent remains intact and does not inherit casual-only clothing constraints.
- [ ] AC 6: Given live DB starts from current local state, when migrations are applied, then style ID/name mapping and prompt templates match code-defined expected values with no duplicate active style names.
- [ ] AC 7: Given generated images are loaded in gallery, when style labels are rendered, then card titles reflect persisted style accurately and unknown labels render an explicit fallback indicator.
- [ ] AC 8: Given unit and integration test suites run, when style fidelity tests execute, then they verify requested style to persisted style consistency and prevent casual/fitness regression.

## Additional Context

### Dependencies

- SQL Server schema migration capability (`dotnet ef migrations add` / `dotnet ef database update`).
- Replicate prediction webhook flow must remain enabled and signature validation operational.
- Existing style seed data in `ApplicationDbContext.SeedStyles()` remains source-of-truth for style rows.
- Angular style selection pipeline must continue consuming active styles from `/api/style`.
- Logging infrastructure must support correlation between generation and webhook events.

### Testing Strategy

- Unit tests: extend `ReplicateApiClient` tests to assert style resolution metadata, validation, and fallback behavior.
- Unit tests: extend style prompt token tests for casual anti-underdress constraints and fitness non-regression.
- Integration tests: add mock prediction/webhook round-trip test that verifies persisted style correctness.
- Migration verification: run DB query assertions post-migration for style ID/name uniqueness and prompt content parity.
- Manual tests: generate `casual` and `fitness` in one batch, verify resulting gallery cards and visual outputs match intended style categories.

### Notes

- High risk: live DB migration drift currently exists (local DB missing latest style-fix migrations), so code-only changes will not fix runtime behavior without data reconciliation.
- High risk: style-name and style-id are handled in different layers; mismatch can silently pass unless validation and metadata contracts are enforced.
- Known limitation: visual style quality is model-probabilistic; this spec addresses deterministic routing/labeling and prompt guardrails, not perfect model output determinism.
- Future consideration: move style prompt/version management into admin-controlled versioned configs to reduce migration-driven drift.

## Review Notes

- Adversarial review completed.
- Findings: 12 total, 12 fixed, 0 skipped.
- Resolution approach: auto-fix.
