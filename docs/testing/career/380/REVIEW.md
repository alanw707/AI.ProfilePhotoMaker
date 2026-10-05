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

Open P0/P1: **none**.
