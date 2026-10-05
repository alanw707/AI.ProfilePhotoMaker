# Career API: roadmap tracking and replans (#388)

Same envelope/auth/flag as `api-roadmap.md`. Design: ADR 0017.

- `GET /api/career/roadmaps/{id}/progress` → `{ roadmapId, version, tasks: [{ taskId, title, origin: "generated|human", milestoneDay: 0|30|60|90, dependsOn, status, effectiveStatus, blockedBy: [title], effortHours, outputNote, linkedMaterialId, linkedMaterialMissing, help, etag: "task-vN" }] }`. 409 `CareerRoadmapNotAccepted`.
- `PUT /api/career/roadmaps/{id}/progress/{taskId}` `{ status?, effortHours?, outputNote?, linkedMaterialId? }` + `If-Match` → 200 task. 400 invalid values, 404 linked material not the caller's, 412/428.
- `POST /api/career/roadmaps/{id}/tasks` `{ title, effortHours, milestoneDay, dependsOn }` → 201 human task. 409 `CareerRoadmapCycle`, 400 unknown dependency.
- `POST /api/career/roadmaps/{id}/replan` → 201 `{ id, baseVersion, changes: [{ id, kind: "added|removed|changed", taskId, title, fields: [{ field, before, after }], rationale }], preserved: [{ taskId, title, reason: "done|has_output|human" }] }`. An empty `changes` list is valid.
- `GET /api/career/replans/{rid}`; `POST /api/career/replans/{rid}/apply` `{ acceptedChangeIds: [] }` → 200 new roadmap version (progress carried); `POST /api/career/replans/{rid}/reject` → 200. 409 `CareerReplanStale` or `CareerReplanClosed`, 400 unknown change id.
