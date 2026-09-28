# 02: Save a manual career profile and goal behind the career flag

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

A signed-in user can enter, confirm and revisit professional facts and a goal in the approved shell without uploading a resume. This is the first real career tracer bullet.

## Acceptance criteria

- [ ] Add owner-scoped, versioned career profile/goal storage with additive migrations; do not reuse photo gender/ethnicity fields.
- [ ] Implement manual setup, Profile and saved-goal summary with direct editing, provenance, confirmation and accessible validation.
- [ ] Use authenticated APIs, ETag/If-Match conflict handling and explicit accepted versions; cross-user IDs return no data.
- [ ] Reload restores accepted facts; profile changes mark derived results stale; old versions remain recoverable.
- [ ] Feature-off preserves existing /app entry and photo routes; flags are enforced by the server.
- [ ] Provide API and browser tests for create/edit/reload, two-user isolation, stale edits and flag-off photo continuity; private data deletion hooks exist from first storage.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/377 — Validate the connected six-page career prototype

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
