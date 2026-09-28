# 19: Calibrate sustainable free usage and operator controls

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

Use measured end-to-end costs to configure transparent free allowances, operational ceilings and abuse protection without introducing a paid career offer.

## Acceptance criteria

- [ ] Measure model, extraction, source, storage and export costs/latency across representative completed and failed journeys; report per-action and per-active-user distributions.
- [ ] Obtain owner approval for operating budget and actual quota values; encode versioned policy, account window and exact reset time server-side.
- [ ] Show remaining/reserved allowance and reset behavior; allow reading/editing/export of saved work when generation is exhausted.
- [ ] Exercise reservation settlement, retries, cancellation, abandoned runs, concurrency and cost-cap enforcement without double spending allowances.
- [ ] Add rate limits, queue/backpressure and operator kill switches; collect redacted costs/errors/source-age and completion metrics without resume bodies.
- [ ] Rerun model/source quality gates for the selected configuration; prove manual intake and useful first journey fit the selected allowance; no photo-to-career ledger conversion.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/393 — Connect the complete contextual career-agent journey

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
