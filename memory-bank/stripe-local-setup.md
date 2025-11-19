# Stripe Local Development Setup

## Overview
Complete setup guide for testing Stripe payments locally with webhook support.

## Prerequisites
- Stripe account (test mode)
- dotnet CLI
- Stripe CLI installed at `~/.local/bin/stripe`

## Installed Components

### Stripe CLI
- **Version**: 1.32.0
- **Location**: `~/.local/bin/stripe`
- **PATH**: Added to `~/.bashrc`

## Configuration Status

### User Secrets (dotnet user-secrets)
All three Stripe secrets are configured in the API project:

```bash
Stripe:SecretKey = sk_test_51SAU... (test mode)
Stripe:PublishableKey = pk_test_51SAU... (test mode)
Stripe:WebhookSecret = whsec_b235f... (local webhook signing secret)
```

### Verification
```bash
cd AI.ProfilePhotoMaker.API
dotnet user-secrets list | grep -i stripe
```

## Local Webhook Testing

### Quick Start
```bash
# Start webhook listener (recommended)
./scripts/stripe-webhook-listen.sh
```

### Manual Setup
```bash
# Get your Stripe secret key from user-secrets
cd AI.ProfilePhotoMaker.API
STRIPE_KEY=$(dotnet user-secrets list | grep "Stripe:SecretKey" | awk '{print $3}')

# Start webhook forwarder
stripe listen \
    --forward-to localhost:5032/api/stripe/webhook \
    --api-key "$STRIPE_KEY"
```

### How It Works
1. Stripe CLI creates a temporary webhook endpoint on Stripe's servers
2. When events occur (e.g., payment succeeded), Stripe sends them to the CLI
3. CLI forwards events to your local API at `http://localhost:5032/api/stripe/webhook`
4. Your API validates the webhook signature using `Stripe:WebhookSecret`

## Development Workflow

### Option 1: Docker Dev Containers (Recommended)
```bash
# The dev-up.sh script automatically loads Stripe secrets
./scripts/dev-up.sh

# In a separate terminal, start webhook listener
./scripts/stripe-webhook-listen.sh
```

The `dev-up.sh` script automatically:
- Exports Stripe secrets from user-secrets to environment variables
- Passes them to the Docker containers
- Enables full payment processing in dev containers

### Option 2: Direct dotnet run
```bash
# Terminal 1: Start API
cd AI.ProfilePhotoMaker.API
dotnet run

# Terminal 2: Start webhook listener
./scripts/stripe-webhook-listen.sh

# Terminal 3: Start UI
cd AI.ProfilePhotoMaker.UI
npm start
```

## Testing Payments

### Test Card Numbers
```
Success: 4242 4242 4242 4242
Decline: 4000 0000 0000 0002
3D Secure: 4000 0025 0000 3155

Expiry: Any future date
CVC: Any 3 digits
ZIP: Any 5 digits
```

### Monitoring Webhooks
When the webhook listener is running, you'll see real-time events:
```
[200] POST /api/stripe/webhook [evt_1ABC...]
[200] POST /api/stripe/webhook [evt_2DEF...]
```

## Troubleshooting

### Webhook Secret Mismatch
If you see signature validation errors:
1. Stop the webhook listener
2. Start it again with `./scripts/stripe-webhook-listen.sh`
3. Copy the new `whsec_...` secret from the output
4. Update user-secrets:
   ```bash
   dotnet user-secrets set "Stripe:WebhookSecret" "whsec_new_secret_here"
   ```
5. Restart your API

### Stripe CLI Not Found
If `stripe` command is not found after installation:
```bash
# Reload bash configuration
source ~/.bashrc

# Or use full path
~/.local/bin/stripe --version
```

### Port Conflicts
If port 5032 is already in use:
```bash
# Change API port
export API_HTTP_PORT=5033
./scripts/dev-up.sh

# Update webhook listener (it reads API_HTTP_PORT automatically)
./scripts/stripe-webhook-listen.sh
```

## Security Notes

### Local Development Secrets
- All secrets in user-secrets are **test mode** only
- Never commit test secrets to git
- Webhook signing secret changes each time you start `stripe listen`

### Production Secrets
Production uses different secrets stored in:
- **GitHub Secrets**: For CI/CD
- **Azure Key Vault**: For runtime

## Related Files
- Script: `scripts/stripe-webhook-listen.sh`
- Config: `docker-compose.dev.yml` (Stripe env vars)
- Loader: `scripts/dev-up.sh` (exports Stripe secrets)
- Docs: `docs/setup/ENVIRONMENT_SETUP.md`

## Next Steps
1. ✅ Stripe CLI installed
2. ✅ Secrets configured
3. ✅ Webhook listener script created
4. 🔄 Test full payment flow with dev containers

## Useful Commands
```bash
# List all user secrets
dotnet user-secrets list

# Test webhook listener
./scripts/stripe-webhook-listen.sh

# Start dev environment with Stripe
./scripts/dev-up.sh

# Check Stripe CLI version
stripe --version

# Trigger test events manually
stripe trigger payment_intent.succeeded
```
