
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
