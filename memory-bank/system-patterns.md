# System Patterns

This file captures the few key patterns that matter most for day-to-day reasoning about the system. For deeper detail, see `docs/architecture/ARCHITECTURE_OVERVIEW.md` and `docs/architecture/cloud-architecture.md`.

## Architecture Patterns

- **Hybrid storage with self-healing** – Database is the source of truth for metadata, with the filesystem as a backing store; background services repair gaps so dashboard/gallery stay consistent even if webhooks fail.
- **Async AI workflows** – Long-running Replicate training/generation happens via webhooks and polling services, decoupled from HTTP requests, with idempotent handlers and retry-aware options.
- **Credit-gated operations** – All expensive operations (training, generation, enhancement) go through the credit system, with Stripe purchases updating a normalized payment + credit ledger.

## Code Patterns

- **Feature-first organization** – API and UI group controllers/components/services by feature (auth, profile, gallery, credit) rather than by technical layer, making it easier to reason about user flows.
- **Thin controllers, rich services** – Controllers handle auth/validation and delegate business rules to services (e.g., credit packages, webhook processing, retention policies).
- **Background services for cross-cutting jobs** – Hosted services handle recurring work like credit resets, model polling, and retention, keeping HTTP request paths simple.

## Documentation Patterns

- **Single canonical index** – `docs/INDEX.md` is the entry point; all long-form docs live under `docs/` (architecture, deployment, security, operations, development).
- **Memory bank for “now”** – `memory-bank/*.md` is intentionally short and operational (current context, Stripe local setup, progress, decisions), and should link back to canonical docs instead of duplicating them.
