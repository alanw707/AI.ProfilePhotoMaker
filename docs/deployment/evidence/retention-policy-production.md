# Evidence Log: AC-6 Retention Policy (Production)

## Evidence Metadata
- Evidence ID: prod-ac6-retention-policy
- Requirement ID (AC/PC): AC-6
- Environment: production
- Date: 2025-12-23T17:24:01Z
- Operator: Alan

## Inputs
- Request payloads: GET retention policy endpoint (no body)
- Test user/account: production test account (non-PII)
- Preconditions: authenticated session token

## Commands Executed
- See `docs/deployment/evidence/retention-policy-production.log`

## Output Summary
- Expected result: retention schedule returned for uploads and generated images (30/30 days).
- Actual result: policy returned with 30-day uploads, 30-day generated images.
- Pass/Fail: Pass (policy matches 30/30 days).

## Evidence Files
- `docs/deployment/evidence/retention-policy-production.json`
- `docs/deployment/evidence/retention-policy-production.log`

## Redactions
- Tokens/PII removed: yes (auth token redacted)

## Notes
- Retention policy now matches 30/30 day schedule.
