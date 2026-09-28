# 04: Run one bounded career-agent task with durable recovery and quotas

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

From the career page, the user asks for a profile summary and receives a reviewable saved proposal through a resumable, cancellable task. This narrow task proves the runtime before market tools are added.

## Acceptance criteria

- [ ] Evaluate and configure a text-model adapter with structured tool use, retention/privacy terms, latency/cost evidence and deterministic provider fakes; do not hardcode unverified model availability.
- [ ] Persist queued/working/needs-input/completed/failed/cancelled states, pinned versions, task checkpoints, lease/fencing and stable operation IDs.
- [ ] Show observable progress, question continuation, reconnect and cancellation through polling; no fake percent or hidden reasoning transcript.
- [ ] Enforce a tool allowlist, server ownership and action scope; profile summary is a proposal, never a silent fact overwrite.
- [ ] Implement configurable step/time/retry/monetary ceilings and separate career allowance reservation/settlement; reads are free and old results remain accessible.
- [ ] Test duplicate requests, changed idempotency payload, quota races, two workers, lease expiry, crash after provider response, cancel/late completion and stale profile with real SQL Server where concurrency matters.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/378 — Save a manual career profile and goal behind the career flag

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
