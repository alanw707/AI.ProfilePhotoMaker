# Evidence Log: AC-3 Training Credits Deduction (Production)

## Evidence Metadata
- Evidence ID: prod-ac3-training-start
- Requirement ID (AC/PC): AC-3
- Environment: production
- Date: 2025-12-23T14:16:32Z
- Operator: Alan

## Inputs
- Request payloads: JSON (userId, imageZipUrl)
- Test user/account: production test account (non-PII)
- Preconditions: authenticated session token; training ZIP available; credits >= 15

## Commands Executed
- See `docs/deployment/evidence/training-start-production.log`

## Output Summary
- Expected result: training starts and consumes 15 credits.
- Actual result: training started (prediction status=starting) and credits reduced (50 -> 35).
- Pass/Fail: Pass (production)

## Evidence Files
- `docs/deployment/evidence/training-start-production.json`
- `docs/deployment/evidence/training-start-production.log`

## Redactions
- Tokens/PII removed: yes (userId and SAS token redacted)

## Notes
- READY-model block still needs evidence if account already has a READY model.
