# ADR 0017: Roadmap progress is tracked per task; replans are reviewed diffs

Status: accepted (2026-10-05, ticket #388, spec #376). Builds on ADR 0016.

## Decision
- **Progress lives on the accepted roadmap's tasks**, in a `CareerRoadmapTaskProgress` row per (roadmap, taskId): `status` (`not_started|in_progress|done|blocked`), optional `effortHours`, `outputNote` (plain text, ≤ 2000), optional `linkedMaterialId` (must belong to the caller; validated on write, and reads report `linkedMaterialMissing` when the material was later deleted), and a `RowVersion`. Writes need `If-Match: "task-vN"` (428 missing, 412 stale) so simultaneous edits never silently overwrite. Only accepted roadmaps are trackable (409 `CareerRoadmapNotAccepted`).
- **Human-authored tasks** may be added (`origin: human`) with dependencies; dependency changes are DAG-validated (409 `CareerRoadmapCycle`). A task whose dependencies are not done reads as `blocked` with the blocking task titles named.
- **Completing a task changes nothing else**: no salary estimate, pay analysis or goal is touched (test asserts pay analyses and goal versions are unchanged).
- **Replan** (`POST /roadmaps/{id}/replan`) is deterministic, rebuilds generated tasks from current evidence via `RoadmapBuilder`, and stores a `CareerRoadmapReplan` proposal: a list of changes (`added|removed|changed` with field-level before/after for title, effort, milestone day and dependencies) plus a rationale per change. Done tasks, tasks with output or linked material, and human tasks are **preserved** (never in removed; listed as `preserved`). The user applies a subset by change id (`POST /replans/{rid}/apply {acceptedChangeIds}`); everything else is rejected; reject-all is `/reject`. Applying writes a new roadmap version that carries progress over; a replan older than the roadmap's current version is 409 `CareerReplanStale`.
- **Help and recovery**: each task has deterministic contextual help text from the template library. The UI keeps the selected task and unsaved draft edits in `sessionStorage` keyed by roadmap and restores them after reload.
- **Privacy**: owner cascade and `CareerPrivateDataService` coverage, like ADR 0016.
