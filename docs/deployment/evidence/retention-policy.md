# Evidence Log: AC-6 Retention Policy (Local Preflight)

## Evidence Metadata
- Evidence ID: local-preflight-ac6-retention-policy
- Requirement ID (AC/PC): AC-6
- Environment: local
- Status: Deprecated (production evidence is authoritative)
- Date: 2025-12-22T07:02:20-08:00
- Operator: Alan

## Inputs
- Request payloads: GET retention policy endpoint (no body)
- Test user/account: local test account (non-PII)
- Preconditions: authenticated session cookie

## Commands Executed
- Not captured (response stored in JSON evidence).

## Output Summary
- Expected result: retention schedule returned for uploads and generated images.
- Actual result: policy returned with 7-day uploads, 30-day generated images, background interval listed.
- Pass/Fail: Partial (policy endpoint validated; deletion behavior not exercised)

## Evidence Files
- `docs/deployment/evidence/retention-policy.json`
- `docs/deployment/evidence/retention-policy-production.json`
- `docs/deployment/evidence/retention-policy-production.log`

## Redactions
- Tokens/PII removed: not applicable

## Notes
- Policy updated to 30/30 days; refresh local evidence if needed.
- Production evidence is authoritative; local preflight evidence retained for history.
