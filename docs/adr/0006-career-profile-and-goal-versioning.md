# ADR 0006: Career profile and goal are versioned, owner-scoped aggregates behind a server flag

Status: accepted (2026-09-30, ticket #378, spec #376)

## Context

Ticket #378 is the first durable career storage. The spec requires owner-scoped private aggregates, immutable accepted versions, ETag/If-Match concurrency, recoverable history, stale marking of derived results, server-enforced feature flags, and deletion hooks from first storage.

## Decision

- **Aggregates.** `CareerProfile` (one per owner) points at its active `CareerProfileVersion`; `CareerGoal` (one per owner for this slice) points at its active `CareerGoalVersion`. Versions are immutable rows; every accepted write appends a new version and moves the active pointer in the same `SaveChanges`.
- **Owner identity** always comes from the authenticated `NameIdentifier` claim. Every query filters by `OwnerId`; another owner's goal ID returns 404, never 403.
- **Concurrency.** The ETag is the active version number (`"profile-v3"`, `"goal-v2"`). `ActiveVersionNumber` is an EF concurrency token and `(aggregateId, versionNumber)` is unique, so two writers racing from the same base cannot both win. First creation needs no `If-Match`; later writes without it return **428**, stale ones **412**.
- **Provenance.** This slice only accepts manual facts. Each version records `Source = "manual"` and the user's explicit confirmation time; unconfirmed saves are rejected (400). Resume-derived proposals (#379) will add other sources.
- **Recovery.** Old versions are readable, and restoring one creates a new version that copies it (history is never rewritten).
- **Staleness.** A goal version records the profile version it was based on. When the profile moves past it (or a profile is first saved after a goal created without one), the goal is reported `isStale: true` until the user re-confirms the goal. Later derived artifacts (reports, roadmaps) follow the same rule.
- **Feature flag.** `Features:CareerWorkspace` (default `false` in every environment). When off, every career endpoint returns **403** with code `CareerWorkspaceDisabled` (spec #376 status table), and the Angular career routes redirect to `/app`. Existing `/app` defaults and photo routes are untouched.
- **Deletion hook.** `ICareerPrivateDataService.DeleteAllForOwnerAsync` removes every career row for an owner. Each new private career entity must be added there (a test enumerates the career DbSets to enforce it). #392 wires it to user-facing controls.
- **Not reused:** photo-profile fields (`UserProfile` gender/ethnicity, credits) stay separate from career data.

## Consequences

- In-memory EF tests prove API behaviour, not SQL uniqueness/isolation; real SQL Server tests are a later gate (spec testing decisions).
- One goal per owner is a simplification for #378; multiple target options can extend `CareerGoalVersion` later without changing the API family.
