# Evidence Log: AC-6 Retention Background Service (Production)

## Evidence Metadata
- Evidence ID: prod-ac6-retention-background-service
- Requirement ID (AC/PC): AC-6
- Environment: production
- Date: 2025-12-23T15:29:14Z
- Operator: Alan

## Inputs
- Request payloads: none (log analytics query)
- Test user/account: not applicable
- Preconditions: API container app running; log analytics enabled.

## Commands Executed
- See `docs/deployment/evidence/retention-background-production.log`

## Output Summary
- Expected result: background retention job runs and performs cleanup.
- Actual result: background service logs show retention policy check completion with cleanup actions.
- Pass/Fail: Pass (background job evidence captured)

## Evidence Files
- `docs/deployment/evidence/retention-background-production.json`
- `docs/deployment/evidence/retention-background-production.log`

## Redactions
- Tokens/PII removed: not applicable (log analytics query only)

## Notes
- Retention policy validated at 30/30 days (see policy evidence).
