# 17: Connect the complete contextual career-agent journey

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

Wire the already-built capabilities into one continuous six-page experience: confirmed profile to market brief, geography/target selection, roadmap, materials and optional photos.

## Acceptance criteria

- [ ] Keep one explicit active goal/conversation context across pages; changing visible context never silently changes saved goals.
- [ ] Route allowlisted tool actions to the existing tested services; contextual buttons pass actual artifact IDs and versions.
- [ ] Home shows current goal, next useful action, latest result and recoverable active work; connect all six pages without placeholder production flows.
- [ ] Structured edits and results remain usable without chat; proposed profile/material/roadmap writes use their acceptance workflows.
- [ ] Recover after browser disconnect, provider partial failure and stale inputs while preserving direct human edits and completed outputs.
- [ ] Run the no-purchase journey on desktop/mobile with covered and sparse pay cases, plus optional-photo continuation and malicious-context attempts.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/386 — Browse eligible job observations with honest coverage
- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/388 — Track roadmap work and review replanning changes
- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/390 — Prepare professional summaries and free document exports
- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/391 — Connect optional photos without changing purchased rights

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
