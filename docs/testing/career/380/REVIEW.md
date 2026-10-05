# #380 career agent run (profile summary): live-stack review

Simulated review on the LocalDev stack (in-memory DB, `FakeCareerTextModel`, career flag on, background worker on), not target-user evidence. No API mocks. Script: `AI.ProfilePhotoMaker.UI/tests/ux/career-agent-review.mjs`; raw results in `checks.json`; screenshots at desktop 1280, mobile 390 and 320.

## Results

| Check | Result |
|---|---|
| Start without a confirmed profile | 409 `CareerProfileRequired` |
| Start with `Idempotency-Key` | 202 |
| Same key, same body | 202, same run id, one reservation |
| Same key, different body | 409 `CareerIdempotencyMismatch` |
| Missing key | 400 |
| Another owner's run: GET / cancel | 404 / 404 |
| Reads (list, status) change the allowance | no |
| No goal → question → answer → completed | steps "Read your confirmed profile", "Read your career goal", "Asked who the summary is for", "Drafted a summary", "Saved the draft for your review" |
| Proposal | source `agent`, one summary item; profile facts unchanged (summary still null, version 1) |
| Reconnect | reload with `?run=` resumes and shows the finished state |
| Profile changed after the run | "Your profile changed after this draft started…" shown; `profileChanged: true`; accepting the proposal → 412 `CareerVersionConflict` |
| Cancel while waiting for an answer | "Stopped. Nothing was changed on your profile."; second cancel 200 `cancelled`; no proposal; reserved draft released (used 0, reserved 0) |
| Expired session | `/auth/login?…&returnUrl=/app/career/summary` |
| Percentages / progress bars in the page | none on any screen |
| Overflow at 1280/390/320 | 0 on every screen |
| axe (WCAG 2.2 AA) | 0 violations on every screen |
| Page errors | 0 |

Concurrency invariants (two workers, lease expiry, crash after provider response, cancel vs late completion, quota race, lost idempotency race, retry/step/time/cost ceilings) are covered by `CareerAgentRuntimeTests` and `CareerAgentRunApiTests`.

## Findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | P2 | An assistant draft opened on the review page under "Import from a resume" with upload copy. | Fixed: agent proposals show "Review your drafted summary" (Playwright assertion added). |
| 2 | P3 | Completed-steps list ran into the buttons; two filled primary buttons competed. | Fixed: list spacing; "Start a new draft" is secondary. |

## Remaining gates (not P0/P1 for this slice)

- **Real SQL Server.** Unique-index violations (2601/2627), row-version races under real isolation and atomic multi-row saves are simulated on the in-memory provider. Proving them against SQL Server needs a SQL Server test host; none is available locally (no Docker access).
- **Model provider.** Only the deterministic fake exists. Production registers no model, so starting a run answers 503 `CareerModelUnavailable` until an owner picks a provider (retention/privacy terms, latency/cost evidence). Human gate, noted on #380.

## /code-review (Standards + Spec, PR #405)

| # | Axis | Severity | Finding | Status |
|---|---|---|---|---|
| R1 | Standards | P1 | Terminal writes (retry-limit failure, completion, ceilings) did not bump the fencing token, so a worker whose lease expired mid-call could overwrite a failed run and settle the allowance twice. | Fixed: every terminal write bumps the token; fence check compares with the loaded token. Test `AWorkerThatLostItsLeaseCannotCompleteARunTheRetryLimitFailed`. |
| R2 | Both | P2 | `ModelCalled` saved only after the provider answered, so a cancel/crash/lost lease mid-call refunded a billed call. | Fixed: recorded (fenced, lease renewed) before the call; cancel-during-call test now expects the unit spent. Test `TheModelCallIsRecordedBeforeTheProviderIsAsked`. |
| R3 | Standards | P2 | Model call not bounded by the lease, so a second worker could re-call the provider. | Fixed: call cancelled at `LeaseSeconds - 5`, then retried. Test `AModelCallThatWouldOutliveTheLeaseIsAbandonedAndRetried`. |
| R4 | Standards | P2 | No backoff between model retries. | Fixed: `RetryBackoffSeconds x attempts` via `LeaseExpiresAt` as not-before. Test `AFailedModelCallIsRetriedOnlyAfterABackoff`. |
| R5 | Standards | P2 | Unanswered questions held an allowance unit forever. | Fixed: `QuestionExpiryHours` (72) fails the run with `CareerQuestionExpired` and releases the unit; UI copy added. Test `AnUnansweredQuestionExpiresAndReleasesTheUnit`. |
| R6 | Standards | P2 | N+1 in `ListAsync` (3 queries per run). | Fixed: steps, profile version and allowance loaded once. |
| R7 | Spec | P1 -> gate | Concurrency proven only on in-memory EF, not real SQL Server. | Remaining gate (allowed by the verification contract): no SQL Server host locally; simulated invariants listed in the `CareerAgentRuntimeTests` doc comment and ADR 0009. |
| R8 | Spec | P1 | Retention rule for runs/steps/allowances not stated. | Fixed: ADR 0009 states retention (kept while the account exists, removed with it or by career deletion; export with #392). |
| R9 | Spec | P2 | Structured tool use only nominal; no adapter evaluation. | Recorded in ADR 0009: fixed tool steps for this task; provider evaluation is the human gate on #380. |
| R10 | Standards | P3 | A slow poll could overwrite a newer answer/cancel state. | Fixed: `latestRun` keeps the newer `updatedAt` (Karma spec). |
| R11 | Spec | P3 | Contract omitted `CareerRunBusy`. | Fixed: documented as 503 with `Retry-After`. |
| R12 | Standards | P3 | Smells (long runner, tuple return, repeated retry loops, polling in a hidden tab). | Accepted judgement calls for this slice. |

Open P0/P1: **none** (R7 is the documented SQL Server gate).
