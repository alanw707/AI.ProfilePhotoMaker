# Story 3.1: Style catalog & selection persistence

Status: done

## Story

As a user,
I want to browse and select styles,
so that training/generation uses my choices.

## Acceptance Criteria

1. `GET /api/style` and `GET /api/style/{id}` return catalog and details; `GET /api/style/name/{name}/template` returns prompt template.
2. `POST /api/style/select` saves selections per user; `GET /api/style/user-selected` returns current selections; validates IDs and ownership.
3. Data shape is consistent for UI (ids, names, previews, prompts); errors are clear for invalid styles.
4. Authorization required; responses scoped to current user.
5. UX: cards/list responsive; selection controls accessible; loading/empty/error states per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: Style endpoints (`/api/style`, `/api/style/{id}`, `/api/style/name/{name}/template`, `/api/style/select`, `/api/style/user-selected`).
- [ ] Service/Data: Style repository + user selection persistence; validation of style IDs; prevent duplicates.
- [ ] DTOs: Consistent response models with prompt templates; include previews/metadata.
- [ ] Tests: Integration tests for catalog/detail/select/retrieve; invalid style id handling; auth enforcement.

## Dev Notes

- Seed style catalog per PRD; ensure stable IDs; template retrieval by name.
- Selections stored per user; align with generation fan-out expectations (Epic 4).
- Logging: structured, user id; no PII.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/StyleController.cs
- Services: AI.ProfilePhotoMaker.API/Services/StyleService.cs, UserStyleSelectionService.cs
- Data: style catalog table/seed; user selections table.

### References

- bdocs/epics.md (E3 Story 3.1)
- docs/product/PRD.md (Style catalog)
- docs/architecture/ARCHITECTURE_OVERVIEW.md
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Requires auth foundation from Epic 1; uses uploads later for training/generation.

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/3-1-style-catalog-selection-persistence.md
- Epic source: bdocs/epics.md (E3)
