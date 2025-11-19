# ClaudeDocs Index

> Status: Historical/generative reports index. For canonical engineering documentation, always start from `docs/INDEX.md`.

The `ClaudeDocs/` directory contains analysis and generated reports (performance audits, deployment investigations, historical/config-drift snapshots). These are auxiliary artifacts and are not the canonical product documentation.

Primary locations:
- `ClaudeDocs/Analysis/Performance/` – performance audit reports
- `ClaudeDocs/Report/` – one-off investigation writeups
- `ClaudeDocs/Config-Drift/` – historical drift monitoring snapshots and dashboards (legacy tooling)

Current reports in repo:

- `ClaudeDocs/Report/deployment-investigation-production-2025-08-29-034851.md`
- Performance audits (selected):
  - `ClaudeDocs/Analysis/Performance/database-performance-audit-2025-09-09-125340.md`
  - `ClaudeDocs/Analysis/Performance/database-performance-audit-2025-09-04-095417.md`
  - `ClaudeDocs/Analysis/Performance/database-performance-audit-2025-09-03-190926.md`
  - `ClaudeDocs/Analysis/Performance/database-performance-audit-2025-09-03-190850.md`
  - `ClaudeDocs/Analysis/Performance/database-performance-audit-2025-09-03-183914.md`
  - `ClaudeDocs/Analysis/Performance/database-performance-audit-2025-09-03-171352.md`
  - `ClaudeDocs/Analysis/Performance/database-performance-audit-2025-09-03-171017.md`
  - `ClaudeDocs/Analysis/Performance/database-performance-audit-2025-08-28-195619.md`

Notes:
- Treat ClaudeDocs as historical/generative output. The canonical engineering documentation lives under `docs/` and in code comments.
- Scripts may reference `ClaudeDocs/` for report output; that’s expected.
