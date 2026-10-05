# #392 Career privacy controls — review

Branch `career/392-privacy-controls` off `feature/career-workspace`. Design: ADR 0020; contract: `docs/career/api-privacy.md`. Independent review by openai-codex/gpt-5.5 (spec + standards).

## Gates (after fixes)
- API `dotnet test` (excl. Performance): 1264 passed, 1 skipped, 0 failed. Release `-warnaserror` clean. EF: no pending model changes; `AddCareerPrivacy` and `CareerPrivacyHardening` only add (drops are in `Down`).
- UI: lint 0 errors; Karma 701; `build:mvp-v1` succeeds. Playwright privacy 12 passed, 0 axe WCAG 2.2 AA violations at 1280/390/320, no overflow at 320.

## Acceptance criteria → evidence
| Criterion | Evidence |
|---|---|
| Retention/processor disclosure | `GET /privacy/retention` (exports 24 h, raw uploads 30 days or on delete, backups up to 35 days, OpenAI processor) rendered on the privacy page |
| Owner-scoped export, documented format | `career-export/v1`; `CareerPrivacyCoverageTests` seeds every covered type and asserts each appears as a section; other owner's rows absent; blobs metadata only, no StorageKey |
| Recent auth, revoke, cancel runs, retried purge with status | 401 `CareerReauthRequired` (>10 min); tombstone + run cancellation; 5-attempt blob retry, `failed` with reason + retry endpoint; UI shows "Deleted" only when completed |
| Scopes; billing and photo data untouched | raw_documents keeps profile; career_profile keeps UserProfile/images/payments; account deletion purges career data first, aborts on failure (required dependency) |
| Tombstones on restore and late workers | Runner discards results after a newer tombstone; upload fence (PRIV-1); startup replay fails closed with 503 until clean (PRIV-2); allowance `CreatedAt` cutoff (PRIV-3) |
| Every aggregate registered | Reflection test over DbContext entities vs `CoveredEntityTypes` (audit records exempted explicitly) |
| Failure/retry/race/restore/cross-user/excerpts tests | `CareerPrivacyPurgeTests`, `CareerPrivacyHardeningTests`, `CareerPrivacyApiTests` |

## Review findings

| ID | Sev | Finding | Status |
|----|-----|---------|--------|
| PRIV-1 | P0 | Concurrent resume upload could write its blob after a deletion | fixed: post-write fence (row + newer tombstone), blob deleted, 410 `CareerResumeDeleted` |
| PRIV-2 | P1 | Tombstone replay did not fail closed | fixed: `ReplayFailedAt` per tombstone, 503 `CareerPrivacyReplayPending`, hosted-service backoff retry |
| PRIV-3 | P1 | `CareerAllowance` dated by `PeriodStart` | fixed: `CreatedAt` column (backfill = PeriodStart), used for cutoffs |
| PRIV-4 | P2 | Docs disagreed on raw upload retention | fixed: ADR 0020 + api-privacy.md |
| PRIV-5 | P2 | `ICareerPrivateDataService` optional in `ProfileController`/`AdminService` | fixed: required dependency, null guards removed |
| PRIV-6 | P3 | Replay re-ran every tombstone | fixed: one purge per owner+scope, `ReplayedAt` stored, idempotence tested |

Open P0/P1: **none**
