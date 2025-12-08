# Story 5.2: Credit purchase, status, and simulation mode

Status: done

## Story

As a user,
I want to purchase credits and see my balances,
so that I can fund training/generation.

## Acceptance Criteria

1. `GET /api/credit/packages`, `GET /api/credit/costs`, `GET /api/credit/payment-config` provide catalog/config for credit purchase.
2. `POST /api/credit/create-payment-intent` creates Stripe PaymentIntent (simulation flag in dev/test); returns client secret.
3. Stripe webhook updates credits on successful payment; history available via `GET /api/credit/history`.
4. Weekly reset job runs per schedule to restore free credits; status reflects last reset and balances; no double-grant on retries.
5. UX: payment flows indicate simulation vs live; show credit deltas and errors clearly; accessible forms/buttons; loading states per `bdocs/ux-acceptance-addendum.md`.

## Tasks / Subtasks

- [ ] Controller: Credit endpoints (packages/costs/payment-config/create-payment-intent/history/status if shared with 1.3).
- [ ] Service: Stripe PaymentIntent creation (respect simulation flag), webhook handler to grant purchased credits idempotently, weekly reset job configuration.
- [ ] Data: Credit packages, purchases, ledger/history; last weekly reset.
- [ ] Security: Validate webhook signature/time; store secrets securely; never log secrets/PII.
- [ ] Tests: Integration tests for payment intent (simulated), webhook grant idempotency, history retrieval, weekly reset job effects.

## Dev Notes

- Align credit costs and packages with PRD; simulation flag for dev/test.
- Weekly reset job should not double-grant; track last reset per user.
- Logging: structured with user id/purchase id; no card data.

### Project Structure Notes

- Controller: AI.ProfilePhotoMaker.API/Controllers/CreditController.cs
- Services: AI.ProfilePhotoMaker.API/Services/CreditService.cs, PaymentService.cs, WeeklyResetService.cs
- Webhooks: AI.ProfilePhotoMaker.API/Controllers/Webhooks/StripeWebhookController.cs

### References

- bdocs/epics.md (E5 Story 5.2)
- docs/product/PRD.md (credits/payments)
- docs/architecture/ARCHITECTURE_OVERVIEW.md (payments/webhooks)
- docs/architecture/cloud-architecture.md (env expectations)
- bdocs/ux-acceptance-addendum.md

## Previous Story Intelligence

- Supports training/generation (Epics 3/4) and enhancement (5.1); depends on auth (Epic 1).

## Dev Agent Record

- Story file: bdocs/sprint-artifacts/5-2-credit-purchase-status-and-simulation-mode.md
- Epic source: bdocs/epics.md (E5)
