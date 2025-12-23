# Evidence Log: PC-2 Replicate Configuration (Production)

## Evidence Metadata
- Evidence ID: prod-pc2-replicate-health
- Requirement ID (AC/PC): PC-2
- Environment: production
- Date: 2025-12-23T13:51:15Z
- Operator: Alan

## Inputs
- Request: GET /api/replicate/health

## Commands Executed
- curl -s -H "Authorization: Bearer ***REDACTED***" https://api.aiprofilephotomaker.com/api/replicate/health

## Output Summary
- Expected result: token configured and Replicate API reachable.
- Actual result: apiConnected=true, tokenValid=true, canCreateModels=true, externalUrlAccessible=false.
- Pass/Fail: Partial (token valid; external URL accessibility needs follow-up).

## Evidence Files
- `docs/deployment/evidence/replicate-health-production.json`

## Redactions
- Tokens/PII removed: yes
