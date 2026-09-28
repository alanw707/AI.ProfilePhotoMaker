# 05: Confirm evidence-based occupation matches

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

The assistant uses confirmed responsibilities to suggest a small set of occupation matches, explains each with supporting facts, and asks the user to resolve ambiguity.

## Acceptance criteria

- [ ] Import an attributable, versioned occupation/task/skill reference snapshot with validated source license and taxonomy codes.
- [ ] Show candidate occupations with actual duty/skill evidence; title alone does not decide the result.
- [ ] Distinguish unsupported skills, missing evidence and known gaps; do not score profile completeness as professional quality.
- [ ] User confirms the match into the goal version; ambiguous matches needing clarification pause the run.
- [ ] Exclude contact details, protected/photo attributes, current salary and desired pay from role matching.
- [ ] Exercise three occupation families, nonstandard titles, contradictory duties, unsupported match and cross-user goal updates through API/browser fixtures.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/380 — Run one bounded career-agent task with durable recovery and quotas

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
