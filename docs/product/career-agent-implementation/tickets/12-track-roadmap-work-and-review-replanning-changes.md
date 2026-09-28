# 12: Track roadmap work and review replanning changes

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

A user completes tasks, attaches prepared work, changes their available time and accepts or rejects an agent's proposed replan without losing progress.

## Acceptance criteria

- [ ] Support task status/effort/output edits with concurrency checks and owner validation on linked artifacts.
- [ ] Preserve completed tasks, supporting evidence and human-authored tasks across replanning.
- [ ] Present a versioned diff with changed dates, dependencies and rationale; apply only accepted changes.
- [ ] Reject cyclic dependencies and explain blocked tasks; completing a task does not alter salary estimates automatically.
- [ ] Offer contextual help for the selected task and save recovery state across browser reload/failure.
- [ ] Test simultaneous edits, rejecting/partially accepting a replan, missing/deleted linked material and faithful progress restoration.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/387 — Create a target-specific career roadmap

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
