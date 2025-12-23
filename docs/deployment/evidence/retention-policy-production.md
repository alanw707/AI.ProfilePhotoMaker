# Evidence Log: AC-6 Retention Policy (Production)

## Evidence Metadata
- Evidence ID: prod-ac6-retention-policy
- Requirement ID (AC/PC): AC-6
- Environment: production
- Date: 2025-12-23T15:27:58Z
- Operator: Alan

## Inputs
- Request payloads: GET retention policy endpoint (no body)
- Test user/account: production test account (non-PII)
- Preconditions: authenticated session token

## Commands Executed
- See `docs/deployment/evidence/retention-policy-production.log`

## Output Summary
- Expected result: retention schedule returned for uploads and generated images (30/30 days).
- Actual result: policy returned with 7-day uploads, 30-day generated images.
- Pass/Fail: Fail (policy mismatch; deployment pending).

## Evidence Files
- `docs/deployment/evidence/retention-policy-production.json`
- `docs/deployment/evidence/retention-policy-production.log`

## Redactions
- Tokens/PII removed: yes (auth token redacted)

## Notes
- Production still reports 7-day retention for original uploads.
