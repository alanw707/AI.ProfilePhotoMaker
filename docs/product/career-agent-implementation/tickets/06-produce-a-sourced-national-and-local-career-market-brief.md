# 06: Produce a sourced national and local career market brief

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

A bounded research task saves a readable career brief with occupational wages and local/national context, source definitions and clear unavailable states.

## Acceptance criteria

- [ ] Validate versioned BLS wage/employment and projection imports, release dates, occupation/geography crosswalks and immutable normalized evidence.
- [ ] Generate a saved, owner-scoped brief with input versions, role alternatives, source references, wage definition, units, geography and next action.
- [ ] Keep benchmark wages, employment stock and projected openings separate; no live-vacancy or personalized-salary claim from these aggregates.
- [ ] Render Analytics and report detail with national/local comparison, source drawer and honest suppressed/missing/stale/failure states.
- [ ] A profile correction preserves the old report but marks it stale; provider/import failure retains completed sections and a recovery action.
- [ ] Fixture tests verify mapping and every displayed number; API/browser tests verify saved report, source inspection, partial failure and reload.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/381 — Confirm evidence-based occupation matches

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
