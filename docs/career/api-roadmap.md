# Career API: roadmap (#387)

Same envelope/auth/flag as `api-profile-goal.md`. Design: ADR 0016.

- `POST /api/career/runs` `{ "task": "roadmap" }` + `Idempotency-Key`. Steps: `read_goal` "Read your career goal", `read_evidence` "Read your market and pay evidence", `build_options` "Compared possible paths", `plan_tasks` "Planned tasks around your available time", `save_roadmap` "Saved your roadmap". Run DTO gains `roadmapId`.
- `GET /api/career/roadmaps` → `{ roadmaps: [{ id, version, status, stale, optionCount, createdAt }] }` newest first, max 20.
- `GET /api/career/roadmaps/{id}` → 
```json
{ "id": "guid", "version": 2, "status": "proposed|accepted|dismissed", "selectedOption": null,
  "pinned": { "profileVersion": 3, "goalVersion": 2, "occupationCode": "15-1252.00", "marketBriefId": null, "payAnalysisId": null },
  "stale": false, "staleReasons": [], "weeklyEffortHours": 6,
  "options": [ { "key": "closest_fit|higher_ambition|steadier_transition", "occupationCode": "…", "title": "…",
      "rationale": [ { "text": "…", "sourceId": "oews", "release": "2025-05" } ],
      "assumptions": ["…"], "missingEvidence": ["…"], "timelineNote": "A scenario, not a promise.",
      "thisWeek": [task], "milestones": [ { "day": 30, "tasks": [task] }, { "day": 60, "tasks": [] }, { "day": 90, "tasks": [] } ] } ],
  "omittedOptions": [ { "key": "higher_ambition", "reason": "no_supported_alternative" } ],
  "lowTimeNote": null }
```
  task = `{ "id": "t1", "title": "…", "effortHours": 2, "dependsOn": ["…"] }`.
- `POST /api/career/roadmaps/{id}/accept` `{ "optionKey": "closest_fit" }` + `If-Match: "goal-vN"` → 200 roadmap (`status: accepted`, `goalUnchanged: true`). 400 unknown option, 404 other owner, 409 `CareerRoadmapNotProposed`, 412/428 precondition.
- `POST /api/career/roadmaps/{id}/dismiss` → 200 (idempotent).
- `PUT /api/career/roadmaps/{id}/tasks/{taskId}` `{ "effortHours": 3 }` → 200 new version (400 for effort outside 0.5–40; dependency validation re-run, 409 `CareerRoadmapCycle`).
