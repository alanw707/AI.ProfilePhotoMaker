# Career API: occupation matches (#381)

Same envelope, auth, flag (403 `CareerWorkspaceDisabled`) and error shape as `api-profile-goal.md`. Design: ADR 0010. Runs: `api-agent-runs.md`.

## Start a match

`POST /api/career/runs` with `{ "task": "occupation_match" }` and an `Idempotency-Key` (same rules as `profile_summary`). Needs a confirmed profile (409 `CareerProfileRequired`). No model is needed and no allowance is spent (the reserved unit is released when the run ends).

Run steps and labels: `read_profile` "Read your confirmed profile", `read_goal` "Read your career goal", `match_occupations` "Compared your duties with occupation tasks", `ask_occupation` "Asked which occupation is closest", `save_match` "Saved the matches for your review".

When ambiguous the run waits in `needs_input` with:

```json
"question": { "id": "occupation", "text": "Which of these is closest to the work you want analysed?", "maxLength": 20,
              "choices": [ { "value": "15-1252.00", "label": "Software Developers" }, { "value": "29-1141.00", "label": "Registered Nurses" },
                           { "value": "none", "label": "None of these" } ] }
```

`POST /api/career/runs/{id}/answers` with `{ "questionId": "occupation", "answer": "15-1252.00" }`; an answer that is not a listed choice → 400 `ValidationError` (`fieldErrors.answer`). `question.choices` is omitted for free-text questions.

A completed run's DTO has `occupationMatchId` (null for other tasks).

## Read

`GET /api/career/occupation-matches/{id}` → 200 (404 `CareerOccupationMatchNotFound` when missing or another owner's):

```json
{
  "id": "guid", "runId": "guid",
  "status": "proposed | confirmed | dismissed | unsupported",
  "pinnedProfileVersion": 3, "pinnedGoalVersion": 2, "profileChanged": false,
  "reference": { "name": "O*NET 30.0 Database", "release": "30.0", "releaseDate": "2025-08", "taxonomy": "O*NET-SOC 2019",
                 "license": "CC BY 4.0", "licenseUrl": "…", "url": "…", "attribution": "…" },
  "matcherVersion": "duty-overlap-1",
  "candidates": [
    {
      "code": "15-1252.00", "title": "Software Developers", "description": "…",
      "strength": "strong | moderate | weak",
      "evidence": [
        { "kind": "duty", "profileField": "highlights", "profileIndex": 0, "profileText": "Built REST APIs…",
          "referenceKind": "task", "referenceId": "16987", "referenceText": "Modify existing software…" },
        { "kind": "skill", "profileField": "skills", "profileIndex": 1, "profileText": "Python",
          "referenceKind": "technology", "referenceId": null, "referenceText": "Python" }
      ],
      "titleMatched": false,
      "missingEvidence": ["Analyze user needs and software requirements…"],
      "knownGaps": ["Systems Analysis"],
      "unsupportedSkills": ["Phlebotomy"]
    }
  ],
  "clarification": null,                       // { "question": "…", "answer": "15-1252.00" } when the run asked
  "guidance": null,                            // unsupported: "Describe the work you do in your profile highlights…"
  "confirmedCode": null, "confirmedIntoGoalVersion": null, "createdAt": "…", "decidedAt": null
}
```

Candidates: at most 5. No percentages, no completeness score.

## Confirm / dismiss

`POST /api/career/occupation-matches/{id}/confirm` with `{ "occupationCode": "15-1252.00" }` and `If-Match: "goal-vN"` → 200 goal DTO (new version, `occupation` set) + `ETag`.

| Status | Code | When |
|---|---|---|
| 400 | `ValidationError` | missing or malformed code |
| 404 | `CareerOccupationMatchNotFound` | missing or another owner's |
| 409 | `CareerGoalRequired` | no goal |
| 409 | `CareerMatchStale` | profile changed since the match |
| 409 | `CareerMatchNotConfirmable` | code not a candidate, result unsupported, or already decided |
| 412 | `CareerVersionConflict` | stale `If-Match` |
| 428 | `CareerPreconditionRequired` | no `If-Match` |

`POST /api/career/occupation-matches/{id}/dismiss` → 200 match (idempotent; a confirmed match cannot be dismissed → 409 `CareerMatchNotConfirmable`).

## Goal DTO addition

`occupation`: `{ "code": "15-1252.00", "title": "Software Developers", "referenceRelease": "30.0", "matchId": "guid" }` or null. Carried forward by later goal edits; restored with goal versions.

## Reference

`GET /api/career/occupations/reference` → the `reference` object above plus `occupationCount`. Free, no allowance.
