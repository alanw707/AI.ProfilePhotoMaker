# Changelog

All notable changes to this project will be documented in this file.

## 2025-12-23 — Alpha.20 Output Folder Update

🎉🎉 Happy Holidays and New Year 🍾🍾

IMPORTANT Changes with Version Alpha.20 - PLEASE READ THIS if upgrading from earlier Alpha Versions:

1. The BMad Core default output folder has changed from docs to `_bmad-output`. `docs` is meant for long-term artifacts, which you can always decide to move content to.

2. If utilizing the BMad Method Module (BMM) please be aware of the following important recent changes:

- Phases 1-3 (Analysis, Planning, Solutioning) will now default output to _bmad-output/planning-artifacts
- Phase 4 (Implementation) will now default output to _bmad-output/implementation-artifacts
- Long term project knowledge (research, docs, references, document-project output) will now default to docs/

IT IS STRONGLY SUGGESTED to align with these folder conventions instead of dumping all to docs/ - if you are upgrading from a prior
version where all output was going to docs or docs/sprint-artifacts, it is suggested to reset configs to these new values.

If you have anything in progress, you can move what was in sprint-artifacts to _bmad-output/implementation-artifacts, and if you had brainstorming
content, a PRD, UX or Architecture, you can move the content to _bmad-output/planning-artifacts.

## 2025-08-22 — Enhanced Photo Webhook Migration & Performance Optimization

### Major Architectural Improvements
- **feat(api): Migrated enhance photo to pure webhook pattern** - Eliminated conditional HTTP/HTTPS logic for consistent webhook-based processing
- **perf(api): 75-85% faster enhance photo response times** - Webhook optimization provides immediate response vs. polling-based delays
- **refactor(api): Unified webhook architecture** - All Replicate operations now use consistent webhook pattern for better reliability
- **security(api): Enhanced webhook validation** - Strengthened signature validation and HTTPS requirements

### Technical Changes
- Removed conditional polling logic from `ReplicateApiClient.EnhancePhotoAsync()`
- Consolidated webhook URL resolution across all Replicate operations
- Improved error handling and logging for webhook-based workflows
- Updated integration tests to reflect pure webhook behavior

### Quality Assurance
- Comprehensive Playwright testing across all browsers (Chrome, Firefox, Safari, Mobile Chrome, Mobile Safari, WebKit)
- End-to-end testing validates webhook consistency and performance improvements
- Production deployment readiness confirmed with extensive testing

### Performance Metrics
- **Response Time**: Improved from 3-5 seconds to <1 second for enhance photo operations
- **Reliability**: Eliminated race conditions from conditional HTTP/HTTPS handling
- **Consistency**: Unified webhook pattern across training, generation, and enhancement workflows

---

## 2025-08-20 — CORS Hotfix, Quality Improvements, and Guardrails

- fix(api): Set Blob `Content-Type` when uploading to Azure Storage so images return correct MIME types (jpeg/png/webp/zip). Ensures proper rendering and caching.
- fix(api): Correct Azure Blob overwrite logic when using `BlobUploadOptions` by deleting existing blobs before upload.
- fix(ui): Avoid sending dev-only header in production gallery downloads to reduce unnecessary CORS preflight requests.
- infra(bicep): Fix CORS array syntax and remove unnecessary `dependsOn` entries; keep `allowedOrigins` minimal and explicit.
- ci(deploy): Add post-deploy validation to verify Azure Blob CORS configuration and preflight behavior; fail fast on misconfiguration.
- chore: Remove temporary hotfix workflow and runbook now that infra + CI enforce CORS.

Verification
- UI lint passes (errors-only) and production build succeeds.
- Bicep template builds clean.
- Production Blob preflight and GET with Origin now include `Access-Control-Allow-Origin` and expected headers.
