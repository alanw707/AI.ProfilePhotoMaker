# Code Review Checklist

Use this checklist when reviewing a story implementation.

## Story vs Git
- Story includes a clear file list of changes
- Git diff matches the story’s claimed file list
- Tasks marked `[x]` have concrete evidence in code/tests

## Acceptance Criteria
- Each AC is explicitly verified in code or tests
- Edge cases are covered (null/empty, auth, error paths)

## Security
- No anonymous debug endpoints in non-development
- No trust of user-controlled forwarded headers for URL generation
- Proper auth/ownership checks on all user-scoped endpoints
- No secrets in logs

## Performance
- No buffering of large blobs in memory (stream responses)
- Background polling is bounded and stops when idle

## Production Safety
- Startup validations run in non-development environments
- External integrations fail fast when required configuration is missing

## Tests
- Unit tests updated/added for critical logic
- E2E/Playwright coverage exists for key user flows (when applicable)

