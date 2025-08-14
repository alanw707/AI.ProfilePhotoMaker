- remember never create a new deployment script, stick with simple-deployment and build images locally
- Don't preserve legacy old code, always remove them
- Always apply YAGNI software principle, you ain't gonna need it
- rememeber we're build a MVP production, doesn't need enterprise grade solutions yet
- use Playwright tests instead of curl for web applications whenever possible

## Development Environment

### Ngrok Setup
- **Reserved domain**: `clear-anteater-usually.ngrok-free.app`
- **Always use**: `ngrok http 5032 --domain=clear-anteater-usually.ngrok-free.app`
- **Never use**: `ngrok http 5032` (creates random URLs that break the config)