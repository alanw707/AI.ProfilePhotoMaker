# Decision Log

This is a lightweight, high-signal log for major product/architecture decisions. Detailed design notes live in `docs/architecture/ARCHITECTURE_OVERVIEW.md`, `docs/development/PROJECT_PLAN.md`, and related docs referenced from `docs/INDEX.md`.

## Decision 1 – Hybrid storage + self-healing
- **Date:** 2025-07-27 (see PROJECT_PLAN)
- **Context:** Gallery and dashboard showed 0 generated photos while images existed on disk; webhooks were occasionally failing.
- **Decision:** Normalize the database model around `ModelCreationRequest` and adopt a hybrid approach: DB as the source of truth, filesystem as a backing store, with auto-healing reconciliation paths.
- **Alternatives Considered:** (a) Filesystem-first with periodic DB imports; (b) forcing strict webhook reliability and treating failures as fatal.
- **Consequences:** Gallery stats auto-repair, missing DB records are backfilled, and the system remains usable even when webhooks are flaky.

## Decision 2 – Stripe credit gating with real webhooks
- **Date:** 2025-11-14
- **Context:** Credit purchase flow was partially implemented (intent + webhook handlers) but not fully validated end to end.
- **Decision:** Treat Stripe Payment Intents + webhooks as the only way to move from “intent created” → “credits granted”, with a dedicated CLI listener (`scripts/stripe-webhook-listen.sh`) and local test workflow.
- **Alternatives Considered:** (a) Simulated payments only; (b) direct “credit grant” endpoints for MVP.
- **Consequences:** Local + production flows align with Stripe best practices, credits are tightly coupled to confirmed payments, and test workflows are scripted.

## Decision 3 – Headless Angular tests with Puppeteer
- **Date:** 2025-11-15
- **Context:** Angular unit tests required a locally installed Chrome, causing friction across environments and CI.
- **Decision:** Use Puppeteer-managed Chromium (`CHROME_BIN`) with Karma’s `ChromeHeadless` and wire Angular’s test builder to `karma.conf.js`.
- **Alternatives Considered:** (a) Disable Karma tests in CI; (b) require Chrome installation on all dev/CI machines.
- **Consequences:** `npm test -- --watch=false` now runs consistently everywhere, lowering regression risk without adding manual setup.
