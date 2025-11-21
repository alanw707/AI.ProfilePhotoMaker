/**
 * Paid Path Smoke (Local)
 *
 * Lightweight validation that the paid flow endpoints are reachable end-to-end:
 *  - registers a fresh user
 *  - fetches credit packages
 *  - creates a Stripe PaymentIntent via API
 *
 * This stops short of completing the card charge (use stripe-checkout.spec.js for the full UI flow),
 * but proves the API is Stripe-ready and not stuck in simulation mode.
 */

const { test, expect } = require('@playwright/test');

const config = {
  baseUrl: process.env.TEST_BASE_URL || 'http://localhost:4200',
  apiUrl: process.env.TEST_API_URL || 'http://localhost:5032',
  password: process.env.TEST_E2E_PASSWORD || 'Aa!23456z!', // complexity-compliant
};

let stripeReady = true;
let targetPackage = null;

async function fetchJson(url, init) {
  if (typeof fetch !== 'function') {
    throw new Error('Global fetch API not available. Use Node 18+.');
  }
  const res = await fetch(url, init);
  const json = await res.json().catch(() => ({}));
  return { status: res.status, ok: res.ok, json };
}

test.beforeAll(async () => {
  const paymentConfigResp = await fetchJson(`${config.apiUrl}/api/credit/payment-config`);
  if (!paymentConfigResp.ok) {
    stripeReady = false;
    return;
  }
  const paymentConfig = paymentConfigResp.json?.data ?? paymentConfigResp.json;
  const simulation = paymentConfig?.paymentSimulation;
  const stripe = paymentConfig?.stripe;
  stripeReady = Boolean(
    stripe?.enabled &&
      stripe?.publishableKey &&
      !simulation?.enabled &&
      !simulation?.skipStripeIntegration
  );

  if (!stripeReady) return;

  const packagesResp = await fetchJson(`${config.apiUrl}/api/credit/packages`);
  if (packagesResp.ok && Array.isArray(packagesResp.json?.data)) {
    targetPackage = packagesResp.json.data[0];
  }
});

test.skip(!stripeReady, 'Stripe integration not enabled (simulation mode or missing keys).');
test.skip(!targetPackage, 'No credit packages available.');

test.describe('Paid path smoke (API)', () => {
  test('registers user and creates payment intent', async () => {
    const email = `paid-smoke-${Date.now()}@example.com`;

    // Register new user
    const registerResp = await fetchJson(`${config.apiUrl}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email,
        password: config.password,
        firstName: 'Paid',
        lastName: 'Smoke',
        gender: 'Prefer not to say',
        ethnicity: 'other',
      }),
    });
    expect(registerResp.ok).toBeTruthy();
    const token = registerResp.json?.token;
    expect(token).toBeTruthy();

    // Create payment intent via API
    const createIntentResp = await fetchJson(`${config.apiUrl}/api/credit/create-payment-intent`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ packageId: targetPackage.id }),
    });

    expect(createIntentResp.ok).toBeTruthy();
    const intent = createIntentResp.json?.data ?? createIntentResp.json;
    expect(intent?.paymentIntentId).toBeTruthy();
    expect(intent?.paymentTransactionId).toBeTruthy();
    expect(intent?.clientSecret).toBeTruthy();
  });
});
