# 13: Draft and edit a fact-grounded targeted resume

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

The user opens Career materials, chooses the target scenario, and reviews an editable targeted resume whose claims are traceable to confirmed professional facts.

## Acceptance criteria

- [ ] Create private material/version records with target/profile/report versions and fact-level provenance.
- [ ] Draft only supported accomplishments/qualifications; missing metrics create separate questions and never invented final copy.
- [ ] Provide accessible editing, autosave status, version history and reviewable agent diffs; protect human edits with concurrency checks.
- [ ] Changing profile or target marks the resume stale without deleting it or overwriting accepted text.
- [ ] Default resume excludes photo and unnecessary private contact data; user controls contact fields.
- [ ] Test unsupported accomplishments, prompt injection, long histories, manual edits during generation, cross-user access, reload and recovery.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/384 — Show reproducible personalized comparable-pay analysis

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
