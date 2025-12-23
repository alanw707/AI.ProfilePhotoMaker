# Evidence Log: AC-1 Upload Validation (Local Preflight)

## Evidence Metadata
- Evidence ID: local-preflight-ac1-upload-validation
- Requirement ID (AC/PC): AC-1
- Environment: local
- Date: 2025-12-22T23:01:06-08:00
- Operator: Alan

## Inputs
- Request payloads: multipart/form-data uploads
- Test user/account: local test account (non-PII)
- Preconditions: authenticated session cookie

## Commands Executed
- See `docs/deployment/evidence/upload-validation.log`

## Output Summary
- Expected result: >20 images rejected; invalid file rejected; valid images accepted with absolute URLs.
- Actual result: matched expected responses.
- Pass/Fail: Pass (local preflight)

## Evidence Files
- `docs/deployment/evidence/upload-validation.json`
- `docs/deployment/evidence/upload-validation.log`

## Redactions
- Tokens/PII removed: yes (no credentials stored)

## Notes
- Replace with production evidence before Go/No-Go.
