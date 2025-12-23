# Evidence Log: AC-5 Enhancement Credits (Production)

## Evidence Metadata
- Evidence ID: prod-ac5-enhancement
- Requirement ID (AC/PC): AC-5
- Environment: production
- Date: 2025-12-23T13:50:25Z
- Operator: Alan

## Inputs
- Request payloads: JSON (imageUrl, enhancementType)
- Test user/account: production test account (non-PII)
- Preconditions: authenticated session token

## Commands Executed
- See `docs/deployment/evidence/enhancement-production.log`

## Output Summary
- Expected result: enhancement runs and deducts credits.
- Actual result: `BotVerificationFailed` (Turnstile token required).
- Pass/Fail: Blocked (Turnstile token required).

## Evidence Files
- `docs/deployment/evidence/enhancement-production.json`
- `docs/deployment/evidence/enhancement-production.log`

## Redactions
- Tokens/PII removed: yes

## Notes
- Retry with a valid Turnstile token to validate credit deduction and output.
