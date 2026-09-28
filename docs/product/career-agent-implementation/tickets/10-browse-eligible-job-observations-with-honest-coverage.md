# 10: Browse eligible job observations with honest coverage

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

The Heatmap workspace can show licensed observed opportunities and their location/remote restrictions, linking users to original listings without submitting applications.

## Acceptance criteria

- [ ] Ingest/query approved posting observations with provider rights, expiry, attribution, deduplication and bounded pagination enforced.
- [ ] Separate observed-posting counts from employment stock, projections and all-market vacancy claims; show coverage and observation dates.
- [ ] Classify remote eligibility as eligible/ineligible/unknown against stated user location restrictions; unknown never passes an eligible-only filter.
- [ ] Show original source links safely with no automatic fetching of arbitrary URLs and no application/outreach tool.
- [ ] Expired or unavailable feed yields an honest unavailable state while benchmark map/report remain usable.
- [ ] Test duplicate reposts, multi-location listings, unknown restrictions, expired pay, source outage, stale preferences and malicious listing content.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/383 — Qualify licensed comparable-pay evidence and calculation rules
- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/385 — Compare U.S. markets through an accessible heatmap and table

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
