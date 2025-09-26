# End-to-End Test Suite

This directory contains Playwright-based E2E coverage that targets the combined Angular UI and ASP.NET Core API.

## Project Layout

- `image-upload-validation.spec.js` – production/staging smoke validation for storage uploads
- `stripe-checkout.spec.js` – local Stripe checkout flow validation (runs only when Stripe keys and test credentials are configured)
- `playwright.config.js` – multi-environment configuration targeting hosted environments
- `playwright.local.config.js` – local configuration that points to the dev stack on `http://localhost:4200`
- `test-images/` – shared assets for upload scenarios

## Local Execution

1. Start the local stack via `./dev-start.sh`. The script now:
   - boots the API (`http://localhost:5032`) and UI (`http://localhost:4200`)
   - exports `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY`, and `STRIPE_WEBHOOK_SECRET` from `dotnet user-secrets` when the variables are not already set
   - seeds default Playwright credentials (`STRIPE_E2E_EMAIL=testuser@example.com`, `STRIPE_E2E_PASSWORD=TestPassword123!`) unless you override them beforehand

2. (Optional) Override Playwright test credentials or Stripe card data if you need different values:
   ```bash
   export STRIPE_E2E_EMAIL="your.test.user@example.com"
   export STRIPE_E2E_PASSWORD="YourTestPassword1!"
   # export STRIPE_E2E_CARD_NUMBER="4242424242424242"
   # export STRIPE_E2E_CARD_EXP="1034"
   # export STRIPE_E2E_CARD_CVC="123"
   # export STRIPE_E2E_CARD_POSTAL="94107"
   ```

3. Verify the API reports real Stripe keys via `GET /api/credit/payment-config`. When simulation mode is disabled the `stripe-checkout` spec will execute; otherwise Playwright skips it automatically.

4. Run the local suite:
   ```bash
   npx playwright test stripe-checkout.spec.js --config tests/e2e/playwright.local.config.js
   ```

### Stripe CLI (Webhooks)

Use the Stripe CLI to forward webhook events to the API when validating webhook behavior end-to-end:

```bash
./dev-start.sh
npx stripe login               # once per machine
npx stripe listen --forward-to localhost:5032/api/hooks/stripe
```

The checkout flow will complete and Stripe CLI will deliver `payment_intent.*` events to the local API. Keep the CLI session running while exercising the UI so the webhook service can reconcile transactions.

### Test Image Setup

The checkout and upload flows expect `tests/e2e/test-images/sample-selfie.jpg`. A placeholder image can be installed with:
```bash
curl -o "tests/e2e/test-images/sample-selfie.jpg" "https://via.placeholder.com/400x400/4A90E2/FFFFFF?text=Test+Image"
```

## CI / Hosted Execution

For hosted environments (staging/production) continue using `tests/e2e/playwright.config.js` which keeps the upload validation spec pointed at deployed instances. The Stripe checkout spec is local-only by default because it depends on interactive Stripe Elements and test card flows.
