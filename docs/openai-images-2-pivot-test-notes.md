# OpenAI Images 2 Pivot Test Notes

Date: 2026-05-14

## Passing verification

- Backend full test suite: `dotnet test AI.ProfilePhotoMaker.API.Tests/AI.ProfilePhotoMaker.API.Tests.csproj --no-restore` — 330 passed.
- Frontend lint: `npm run lint -- --quiet` — passed.
- Frontend development build: `npx ng build --configuration development` — passed.
- Frontend headshot service spec: `PUPPETEER_EXECUTABLE_PATH=/usr/bin/google-chrome npx ng test --watch=false --browsers=ChromeHeadless --include='src/app/services/headshot-generation.service.spec.ts'` — passed.

## Added tests

- `AI.ProfilePhotoMaker.API.Tests/Services/HeadshotGenerationServiceTests.cs`
  - verifies generation stores metadata and consumes credits
  - verifies provider failure refunds credits
  - verifies source image ownership/path validation

- `AI.ProfilePhotoMaker.API.Tests/Integration/HeadshotGenerationEndpointIntegrationTests.cs`
  - verifies authenticated `/api/headshots/generate` request generates stored image metadata and consumes credits
  - verifies unauthenticated requests return Unauthorized

- `AI.ProfilePhotoMaker.UI/src/app/services/headshot-generation.service.spec.ts`
  - verifies frontend service posts to `/api/headshots/generate`

- `AI.ProfilePhotoMaker.API/tests/playwright/tests/instant-headshot-mocked-flow.spec.ts`
  - mocked upload → generate → preview/download/regenerate flow added
  - passed with `BASE_URL=http://127.0.0.1:4300 npx playwright test tests/instant-headshot-mocked-flow.spec.ts --project=chromium --timeout=30000 --reporter=list`
  - uses a development-only `e2eAuthBypass=1` guard bypass to avoid brittle login setup while still exercising the real routed page, upload service, headshot service call, result preview, download button, and regenerate button

## E2E coverage note

The mocked Playwright flow passes against the Angular dev server. The shared Playwright global setup still emits expected Azure credential warnings because upload/performance visual tests share the same harness; those warnings do not affect the mocked instant headshot flow.
