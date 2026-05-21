# Architecture - UI

## Executive Summary
Angular front-end for onboarding, style selection, enhancement, and gallery workflows.

## Technology Stack
- Angular 19 + TypeScript
- RxJS
- Stripe JS, face-api.js

## Architecture Pattern
Standalone Angular configuration using `app.config.ts` and route-driven pages/components.

## Routing
Key routes (non-exhaustive):
- 404, app, auth, complete-profile, confirm-email, cookies, workspace, enhance, examples, faq, features, gallery, help, home, legal, login, packages, pricing, privacy, register, settings, signup, support, terms, verify-email

## API Integration
- Production API base: `https://api.aiprofilephotomaker.com/api`
- Angular services use HttpClient (see `docs/api-contracts-ui.md`)

## Component Overview
- Components detected: 32
- See `docs/component-inventory-ui.md`

## Source Tree
See `docs/source-tree-analysis.md`.

## Development Workflow
See `docs/development-guide.md`.

## Deployment Architecture
- Docker build using `Dockerfile.frontend`
- Nginx config in `nginx.conf`

## Testing Strategy
- Karma/Jasmine unit tests
- Playwright E2E tests (`npm run test:e2e`)
