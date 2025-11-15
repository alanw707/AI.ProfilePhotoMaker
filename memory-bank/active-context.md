# Current Context

## Ongoing Tasks

- ✅ Stripe CLI v1.32.0 installed and authenticated locally
- ✅ Stripe secrets (secret key, publishable key, webhook secret) stored in API user-secrets
- ✅ SQL Server + Azurite containers healthy; MSSQL password now quoted-friendly (`"Vg7pKr42#Local!"`)
- ✅ scripts/stripe-webhook-listen.sh streams events and auto-updates Stripe webhook secret
- ✅ API boots via `./dev-start.sh --api-only` after syncing ConnectionStrings secrets (DB + Azure Storage)
- 🔄 Angular unit/integration suites still fail to compile (guard exports, duplicate identifiers, strict typing)
- 🔄 Backend service/background layers remain untested (AuthService, CreditService, background jobs)

## Known Issues

- Angular Karma suite blocked by incorrect guard exports and duplicate variable declarations
- Low automated coverage (1.6%); no tests on service/business logic or integration flows
- Data retention/background job implementation still incomplete

## Next Steps

- Fix Angular guard exports (`authGuard`, `guestGuard`) and duplicate declarations so `npm run test` compiles
- Add targeted service/background tests (AuthService, CreditService, Polling/Retention services)
- Implement/data-validate retention jobs and document deletion policy
- Run Stripe end-to-end checks periodically to keep webhook secrets in sync with CLI sessions

## Current Session Notes

- [2025-11-14 18:20 PT] Verified SQL password escaping, updated docker-compose health check, and synced SA password & secrets
- [2025-11-14 18:20 PT] API started via `./dev-start.sh --api-only`; confirmed storage/Azure settings via user-secrets override
- [2025-11-14 18:20 PT] Ran Stripe CLI listener script, confirmed webhook secret auto-rotates, purchased Starter Pack (50 credits) via API, webhook succeeded

## Canonical References

- High-level docs index: `docs/INDEX.md`
- Product requirements: `docs/product/PRD.md`
- Architecture overview: `docs/architecture/ARCHITECTURE_OVERVIEW.md`
- Project plan & roadmap: `docs/development/PROJECT_PLAN.md`, `docs/development/SPRINT_ROADMAP.md`
- Test strategy: `docs/development/TEST_ANALYSIS_REPORT.md`
