# ADR 0009: Career agent runs are durable, leased, fenced step machines

Status: accepted (2026-10-04, ticket #380, spec #376)

## Context

#380 proves the career-agent runtime with one narrow task: draft a profile summary and save it as a reviewable proposal. The task must survive restarts and crashes, run on more than one worker, be cancellable, never spend twice, and never change profile facts by itself. No text-model provider has been chosen; provider choice (model, retention/privacy terms, latency/cost evidence) is a human gate recorded on #380.

## Decision

- **Model seam, fail closed.** `ICareerTextModel` takes a structured request (task, pinned facts, allowed tool names) and returns either a final text or a tool call, plus usage and an estimated cost. This slice ships only `FakeCareerTextModel` (deterministic, no network), registered for Development, LocalDev and Testing. **Production registers no model, so `POST /api/career/runs` answers 503 `CareerModelUnavailable`** until an owner configures a real adapter. Nothing names or assumes a specific vendor model.
- **Persistence.** `CareerAgentRun` (one per request) and `CareerAgentStep` (one per executed step). Run status: `queued → working → (needs_input ↔ working) → completed | failed | cancelled`. A run pins the profile version and goal version it read at creation. Steps have a stable operation id `"{runId}:{ordinal}"` (unique), a kind (`tool`, `model`, `question`, `save`), and their output. The run stores a checkpoint (next ordinal + small JSON state).
- **Lease and fencing.** A worker claims a run by setting `LeaseOwner`, `LeaseExpiresAt` and incrementing `FencingToken`, which is an EF concurrency token. Every later write by that worker carries the token it claimed with; if another worker re-claimed after lease expiry, or the user cancelled, the stale write fails as a concurrency conflict and is discarded. Expired leases (`working` with `LeaseExpiresAt` in the past) are claimable again.
- **Crash after provider response.** The model step's output is saved (with the step) before anything uses it. On resume, a step that already has saved output is replayed from storage, never called again, so a crash between the provider answer and the proposal save costs exactly one provider call.
- **Idempotency.** `POST /api/career/runs` requires an `Idempotency-Key` header (8–100 chars). Key + owner is unique; the server stores a SHA-256 of the normalized request body. Same key and same body → the existing run (202, same id, no second reservation). Same key, different body → 409 `CareerIdempotencyMismatch`. A lost race on the unique index reloads the winner.
- **Tool allowlist and owner scope.** The task definition lists the tools it may use (`read_profile`, `read_goal`). Tools resolve data only for the run's owner and pinned versions. A model asking for any other tool fails the run with `CareerToolNotAllowed` and no side effect. Document and profile text are data, never instructions.
- **Proposal, never a fact.** The final `save` step creates a `CareerProfileProposal` with `Source = "agent"` and one `summary` item, pinned to the run's profile version, in the same save that completes the run. Accepting it uses the existing proposal flow; if the profile changed since the run pinned it, acceptance answers 412 and the run status reports `profileChanged: true`.
- **Questions.** If the owner has no goal, the task asks one question ("Who should this summary speak to?") and waits in `needs_input`; `POST /runs/{id}/answers` resumes it. A needs-input run does not hold a lease.
- **Cancel.** `POST /runs/{id}/cancel` is idempotent. It sets `cancelled` and bumps the fencing token, so a worker that finishes late cannot complete the run or create a proposal. Cancelling a finished run returns its final state unchanged.
- **Ceilings (config `Career:Agent`).** `MaxSteps` 8, `MaxRunSeconds` 120, `MaxAttempts` 3 (claims per run; retries after transient failures or lease expiry), `LeaseSeconds` 30, `MaxCostCents` 5, `MonthlyRunAllowance` 20. Exceeding any ceiling fails the run with a stable code (`CareerStepLimit`, `CareerTimeLimit`, `CareerRetryLimit`, `CareerCostLimit`).
- **Career allowance, separate from photo credits.** `CareerAllowance` (owner + UTC month, `Reserved`, `Used`, concurrency token). Creating a run reserves one unit; it is settled (reserved → used) once a model call has been made, and released if the run ends before any model call (cancel, failure, ceiling). No reservation when the month is full: 429 `CareerAllowanceExhausted`. Reads (status, list, proposals) are free; old runs and proposals stay readable.
- **Worker.** `CareerAgentWorker` (hosted service, `Career:Agent:WorkerEnabled`, off in Testing) polls for claimable runs; tests drive `ICareerAgentRunner.RunOnceAsync(workerId)` directly with a controllable clock.
- **Privacy.** Runs, steps and allowances carry `OwnerId`, cascade on account deletion and join `CareerPrivateDataService` deletion coverage. Logs carry ids and codes only. Export arrives with #392.

## Consequences

- SQL Server-specific guarantees (unique index violations 2601/2627, row-version races under real isolation) are simulated in tests on the in-memory provider; proving them against a real SQL Server remains a gate until a SQL Server test host exists.
- Progress shown to users is the list of real completed steps and the current status; there is no percentage and no model reasoning transcript.
