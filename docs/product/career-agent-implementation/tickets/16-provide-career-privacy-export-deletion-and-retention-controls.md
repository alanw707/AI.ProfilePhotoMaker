# 16: Provide career privacy export, deletion and retention controls

## Parent

https://github.com/alanw707/AI.ProfilePhotoMaker/issues/376

## What to build

A user can inspect the assistant's saved knowledge, export career information, remove a resume or delete career data while retaining independent account/photo/billing rights unless they request full account deletion.

## Acceptance criteria

- [ ] Publish actual retention/provider-processing disclosures and controls; review raw uploads/proposals/conversation defaults and actual backup expiry before beta.
- [ ] Export owner-scoped career facts, goals, conversations retained under policy, reports, roadmaps and materials with provenance in a documented format.
- [ ] Deletion requires recent authentication, revokes access immediately, cancels related runs and retries blob/derived-row purges with a visible status.
- [ ] Deletion scope distinguishes raw document, career profile and entire account; required billing records remain separate and photo-only data is not unexpectedly deleted.
- [ ] Enforce tombstones during backup restore and prevent late worker completion from resurrecting deleted artifacts; every new career aggregate registers purge/export coverage.
- [ ] Test partial storage failure, retry, worker race, restored backup, cross-user requests and removal of source excerpts/indexes; never claim deletion while purge is incomplete.

## Blocked by

- https://github.com/alanw707/AI.ProfilePhotoMaker/issues/379 — Import a private resume and review extracted profile changes

## Delivery boundary

Keep this slice demoable or independently verifiable. Extend existing API/browser testing seams and preserve unrelated work. Use synthetic fixtures, not reference-screenshot personal data. Every private entity must participate in ownership, export/deletion and retention rules. No production rollout or paid service purchase is authorized by this issue alone.
