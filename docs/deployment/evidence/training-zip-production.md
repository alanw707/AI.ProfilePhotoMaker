# Evidence Log: AC-2 Training ZIP (Production)

## Evidence Metadata
- Evidence ID: prod-ac2-training-zip
- Requirement ID (AC/PC): AC-2
- Environment: production
- Date: 2025-12-23T13:49:55Z
- Operator: Alan

## Inputs
- Request payloads: multipart/form-data uploads with ForTraining=true
- Test user/account: production test account (non-PII)
- Preconditions: authenticated session token; >=10 images uploaded

## Commands Executed
- See `docs/deployment/evidence/training-zip-production.log`

## Output Summary
- Expected result: ZIP created when >=10 images exist; returns public URL.
- Actual result: zipCreated=true with zipPath returned (SAS query redacted).
- Pass/Fail: Pass (production)

## Evidence Files
- `docs/deployment/evidence/training-zip-production.json`
- `docs/deployment/evidence/training-zip-production.log`

## Redactions
- Tokens/PII removed: yes (SAS token redacted)
