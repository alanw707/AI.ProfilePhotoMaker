# Evidence Log: AC-3 Training Gate (Production)

## Evidence Metadata
- Evidence ID: prod-ac3-training-gate
- Requirement ID (AC/PC): AC-3
- Environment: production
- Date: 2025-12-23T13:50:10Z
- Operator: Alan

## Inputs
- Request payloads: JSON (userId, imageZipUrl)
- Test user/account: production test account (non-PII)
- Preconditions: authenticated session token; training ZIP available

## Commands Executed
- See `docs/deployment/evidence/training-gate-production.log`

## Output Summary
- Expected result: training requires 15 purchased credits; block when insufficient.
- Actual result: `InsufficientCredits` with purchased credits = 0.
- Pass/Fail: Partial (credit gate validated; READY-model block not validated).

## Evidence Files
- `docs/deployment/evidence/training-gate-production.json`
- `docs/deployment/evidence/training-gate-production.log`

## Redactions
- Tokens/PII removed: yes (userId and SAS token redacted)

## Notes
- READY-model block still needs evidence if an account has a READY model.
