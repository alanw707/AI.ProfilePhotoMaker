/**
 * E2E Test: Stripe Checkout Flow (Local)
 *
 * Validates that the premium credit purchase flow integrates with Stripe when
 * real API keys are configured. The test intentionally skips itself when the
 * environment is running in simulation mode or when test credentials are not
 * provided, so it is safe to keep in the suite even during development.
 */

const { test, expect } = require('@playwright/test');

const config = {
  baseUrl: process.env.TEST_BASE_URL || 'http://localhost:4200',
  apiUrl: process.env.TEST_API_URL || 'http://localhost:5032',
  testUser: {
    email: process.env.STRIPE_E2E_EMAIL,
    password: process.env.STRIPE_E2E_PASSWORD,
  },
  stripe: {
    cardNumber: process.env.STRIPE_E2E_CARD_NUMBER || '4242424242424242',
    exp: process.env.STRIPE_E2E_CARD_EXP || '1034', // MMYY
    cvc: process.env.STRIPE_E2E_CARD_CVC || '123',
    postalCode: process.env.STRIPE_E2E_CARD_POSTAL || '94107',
  },
};

let stripeReady = true;
let targetPackageName = null;

async function fetchJson(url, init) {
  if (typeof fetch !== 'function') {
    throw new Error('Global fetch API not available. Run Playwright with Node.js 18 or newer.');
  }

  const response = await fetch(url, init);
  if (!response.ok) {
    throw new Error(`Request to ${url} failed: ${response.status} ${response.statusText}`);
  }

  return response.json();
}

test.beforeAll(async () => {
  try {
    const paymentConfigResponse = await fetchJson(
      `${config.apiUrl}/api/credit/payment-config`
    );

    const paymentConfig = paymentConfigResponse?.data ?? paymentConfigResponse;
    const simulation = paymentConfig?.paymentSimulation;
    const stripe = paymentConfig?.stripe;

    stripeReady = Boolean(
      stripe?.enabled &&
        stripe?.publishableKey &&
        !simulation?.enabled &&
        !simulation?.skipStripeIntegration
    );
  } catch (error) {
    console.warn('⚠️ Unable to fetch payment configuration. Skipping Stripe checkout test.', error);
    stripeReady = false;
    return;
  }

  if (!stripeReady) {
    return;
  }

  try {
    const packagesResponse = await fetchJson(`${config.apiUrl}/api/credit/packages`);
    const packages = packagesResponse?.data ?? packagesResponse ?? [];
    targetPackageName = Array.isArray(packages) && packages.length > 0 ? packages[0].name : null;
  } catch (error) {
    console.warn('⚠️ Unable to load credit packages. Stripe checkout test will be skipped.', error);
    stripeReady = false;
  }
});

test.beforeEach(() => {
  test.skip(!config.testUser.email || !config.testUser.password, 'Stripe E2E credentials not configured.');
  test.skip(!stripeReady, 'Stripe integration not enabled (simulation mode or configuration missing).');
  test.skip(!targetPackageName, 'No credit packages available for checkout test.');
});

test.describe('Stripe Checkout Flow', () => {
  test.describe.configure({ timeout: 120000 });
  test('completes purchase up to post-payment reconciliation', async ({ page }) => {
    await login(page);

    await page.goto(`${config.baseUrl}/pricing`);
    await expect(page.locator('.packages-grid'), 'Credit packages should be visible').toBeVisible();

    const packageCard = page.locator('.package-card', {
      has: page.locator('.package-name', { hasText: targetPackageName }),
    });

    await expect(packageCard, `Package "${targetPackageName}" should exist`).toBeVisible();
    await packageCard.locator('button.purchase-btn').click();

    // Wait for Stripe card element to mount
    await expect(page.locator('#card-element')).toBeVisible();
    await waitForStripeCardMount(page);

    await enterCardDetails(page);
    await enterBillingDetails(page);

    const purchaseResponsePromise = page.waitForResponse(response =>
      response.url().includes('/api/credit/purchase') && response.request().method() === 'POST'
    );

    await page.locator('button.confirm-btn').click();

    const purchaseResponse = await purchaseResponsePromise;
    expect([200, 202]).toContain(purchaseResponse.status());

    const notification = page.locator('.notification-title', { hasText: 'Payment Submitted' });
    await expect(notification, 'Payment confirmation toast should appear').toBeVisible({ timeout: 15000 });

    const infoToast = page.locator('.notification-message', {
      hasText: /Payment is still processing|Stripe is still (finalizing|confirming) your payment/i,
    });
    await expect(infoToast, 'Pending reconciliation toast should appear').toBeVisible({ timeout: 15000 });

    // After processing we land back on the dashboard and surface a success toast
    await page.waitForURL('**/app/dashboard', { timeout: 15000 });
    await expect(
      page.locator('.notification-title', { hasText: 'Credits Added' })
    ).toBeVisible({ timeout: 15000 });
  });
});

async function login(page) {
  await page.goto(`${config.baseUrl}/auth/login`);

  await page.fill('input#email', config.testUser.email);
  await page.fill('input#password', config.testUser.password);
  await page.click('button[type="submit"]');

  await page.waitForURL('**/app/dashboard', { timeout: 30000 });
  await expect(page.locator('.dashboard-container')).toBeVisible({ timeout: 15000 });
}

async function waitForStripeCardMount(page) {
  const stripeCardFrame = page.frameLocator('iframe[name^="__privateStripeFrame"]').first();

  await expect(
    stripeCardFrame.locator('input[name="cardnumber"]'),
    'Stripe card number input should mount'
  ).toBeVisible({ timeout: 30000 });

  await expect(
    stripeCardFrame.locator('input[name="exp-date"]'),
    'Stripe expiry input should mount'
  ).toBeVisible({ timeout: 30000 });

  await expect(
    stripeCardFrame.locator('input[name="cvc"]'),
    'Stripe CVC input should mount'
  ).toBeVisible({ timeout: 30000 });
}

async function enterCardDetails(page) {
  const stripeCardFrame = page.frameLocator('iframe[name^="__privateStripeFrame"]').first();

  await stripeCardFrame.locator('input[name="cardnumber"]').fill(config.stripe.cardNumber);
  await stripeCardFrame.locator('input[name="exp-date"]').fill(config.stripe.exp);
  await stripeCardFrame.locator('input[name="cvc"]').fill(config.stripe.cvc);
}

async function enterBillingDetails(page) {
  await page.fill('input[name="billingName"]', 'Stripe E2E Tester');
  await page.fill('input[name="billingEmail"]', `stripe-e2e-${Date.now()}@example.com`);
  await page.fill('input[name="billingAddress1"]', '123 Market Street');
  await page.fill('input[name="billingCity"]', 'San Francisco');
  await page.fill('input[name="billingState"]', 'CA');
  await page.fill('input[name="billingPostal"]', config.stripe.postalCode);
  await page.fill('input[name="billingCountry"]', 'US');
}

