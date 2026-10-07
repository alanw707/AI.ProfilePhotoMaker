# Career API: agent runs (#380)

Same envelope, auth, flag (403 `CareerWorkspaceDisabled`) and error shape as `api-profile-goal.md`. Design: ADR 0009.

## Run DTO

```json
{
  "id": "guid",
  "task": "profile_summary",
  "status": "queued | working | needs_input | completed | failed | cancelled",
  "createdAt": "…", "updatedAt": "…", "completedAt": null,
  "pinnedProfileVersion": 3,               // null when no profile
  "pinnedGoalVersion": 1,                   // null when no goal
  "steps": [                                // real executed steps, oldest first; no percentages
    { "ordinal": 1, "kind": "tool", "name": "read_profile", "label": "Read your confirmed profile",
      "status": "completed", "completedAt": "…" }
  ],
  "question": null,                         // when needs_input: { "id": "audience", "text": "Who should this summary speak to?", "maxLength": 200 }
  "proposalId": null,                       // set when completed
  "profileChanged": false,                  // true when the profile moved past pinnedProfileVersion
  "errorCode": null,                        // failed: CareerStepLimit | CareerTimeLimit | CareerRetryLimit | CareerCostLimit | CareerToolNotAllowed | CareerModelFailed | CareerQuestionExpired
  "allowance": { "used": 1, "reserved": 0, "limit": 20, "periodStart": "2026-10-01T00:00:00Z" }
}
```

Step labels: `read_profile` "Read your confirmed profile", `read_goal` "Read your career goal", `ask_audience` "Asked who the summary is for", `draft_summary` "Drafted a summary", `save_proposal` "Saved the draft for your review".

## Endpoints

| Method | Path | Success | Errors |
|---|---|---|---|
| POST | `/api/career/runs` body `{ "task": "profile_summary" }`, header `Idempotency-Key` | 202 run (same run for a replay) + `Location` | 400 `ValidationError` (`fieldErrors.task`, `fieldErrors.idempotencyKey`), 409 `CareerIdempotencyMismatch`, 409 `CareerProfileRequired` (no confirmed profile), 429 `CareerAllowanceExhausted`, 503 `CareerModelUnavailable` |
| GET | `/api/career/runs` | 200 `{ runs: [run…] }` newest first, max 20, plus `allowance` | — |
| GET | `/api/career/runs/{id}` | 200 run | 404 `CareerRunNotFound` (missing or another owner's) |
| POST | `/api/career/runs/{id}/answers` body `{ "questionId": "audience", "answer": "…" }` | 202 run (back to `queued`) | 400 `ValidationError`, 404, 409 `CareerRunNotWaiting` |
| POST | `/api/career/runs/{id}/cancel` | 200 run (idempotent; finished runs unchanged) | 404 |

Any write may also answer 503 `CareerRunBusy` with `Retry-After: 1` when it keeps losing a race under heavy contention; retry with the same `Idempotency-Key`.

Reads never touch the allowance. A run uses at most one unit: it is spent once a model call has started (even if the call failed or the run was cancelled during it) and released if the run ends before any call. A question left unanswered for 72 hours fails the run with `CareerQuestionExpired` and releases the unit. Polling clients should wait 1.5–2 s between status reads and back off on errors.

## Proposal

A completed run's `proposalId` is a normal `CareerProfileProposal` (`source: "agent"`, one `summary` item). Review/accept it with the existing proposal endpoints; acceptance needs the current profile `If-Match` and answers 412 if the profile moved on.
