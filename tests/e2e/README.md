# End-to-End Test Suite

This directory contains Playwright-based E2E coverage that targets the combined Angular UI and ASP.NET Core API.

## Project Layout

- `image-upload-validation.spec.js` – production/staging smoke validation for storage uploads
- `stripe-checkout.spec.js` – local Stripe checkout flow validation (runs only when Stripe keys and test credentials are configured)
- `playwright.config.js` – multi-environment configuration targeting hosted environments
- `playwright.local.config.js` – local configuration that points to the dev stack on `http://localhost:4200`
- `test-images/` – shared assets for upload scenarios

## Local Execution

1. Ensure the local stack is running (`./dev-start.sh` or equivalent) with the API available on `http://localhost:5032` and the UI on `http://localhost:4200`.
2. Provide Playwright with test credentials and Stripe test card details via environment variables:
   ```bash
   export STRIPE_E2E_EMAIL="stripe.test@example.com"
   export STRIPE_E2E_PASSWORD="YourTestPassword1!"
   # Optional: override Stripe test card details if needed
   # export STRIPE_E2E_CARD_NUMBER="4242424242424242"
   # export STRIPE_E2E_CARD_EXP="1034"
   # export STRIPE_E2E_CARD_CVC="123"
   # export STRIPE_E2E_CARD_POSTAL="94107"
   ```
3. Verify the API is configured with real Stripe keys (`STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY`, and `STRIPE_WEBHOOK_SECRET`). The Stripe checkout spec skips automatically when simulation mode is enabled.
4. Execute the local suite:
   ```bash
   npx playwright test --config tests/e2e/playwright.local.config.js
   ```

### Test Image Setup

The checkout and upload flows expect `tests/e2e/test-images/sample-selfie.jpg`. A placeholder image can be installed with:
```bash
curl -o "tests/e2e/test-images/sample-selfie.jpg" "https://via.placeholder.com/400x400/4A90E2/FFFFFF?text=Test+Image"
```

## CI / Hosted Execution

For hosted environments (staging/production) continue using `tests/e2e/playwright.config.js` which keeps the upload validation spec pointed at deployed instances. The Stripe checkout spec is local-only by default because it depends on interactive Stripe Elements and test card flows.
