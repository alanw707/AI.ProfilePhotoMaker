# 15: Connect optional photos without changing purchased rights

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

From Career materials, a user can reuse an owned photo or continue in the existing photo editor and return to the same career goal, with purchases and allowances intact.

## Acceptance criteria

- [ ] Show optional use/improve/create photo choices and clear existing entitlements; the career journey never requires a photo.
- [ ] Use owner-checked assets and allowlisted relative return routes; preserve existing gallery, workspace and checkout-return behavior.
- [ ] Keep explicit user confirmation and exact package/allowance disclosure before any paid action; the agent cannot initiate spending or generation silently.
- [ ] Retries and returns reuse existing photo-operation idempotency and fulfillment, not career quota.
- [ ] Preserve unfinished package continuation and selected goal on return; declining purchase leaves career data unchanged.
- [ ] Run existing purchase/preview/recovery regressions plus career handoff, expired session, cross-user asset and open-redirect tests.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/378 — Save a manual career profile and goal behind the career flag

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
