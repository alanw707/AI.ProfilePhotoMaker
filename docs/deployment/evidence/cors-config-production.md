# Evidence Log: PC-4 CORS Origins (Production)

## Evidence Metadata
- Evidence ID: prod-pc4-cors-config
- Requirement ID (AC/PC): PC-4
- Environment: production
- Date: 2025-12-23T13:29:31Z
- Operator: Alan

## Inputs
- Request: OPTIONS /api/credit/packages with Origin header

## Commands Executed
- curl -s -D docs/deployment/evidence/cors-config-production.txt -o /dev/null \
  -H "Origin: https://app.aiprofilephotomaker.com" \
  -H "Access-Control-Request-Method: GET" \
  -X OPTIONS https://api.aiprofilephotomaker.com/api/credit/packages

## Output Summary
- Expected result: Access-Control-Allow-Origin matches production UI domain.
- Actual result: `access-control-allow-origin: https://app.aiprofilephotomaker.com`.
- Pass/Fail: Pass (production)

## Evidence Files
- `docs/deployment/evidence/cors-config-production.txt`

## Redactions
- Tokens/PII removed: yes
