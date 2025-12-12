const { test, expect } = require('@playwright/test');

const email =
  process.env.CREDIT_TEST_EMAIL || process.env.E2E_TEST_EMAIL || process.env.TEST_E2E_EMAIL;
const password =
  process.env.CREDIT_TEST_PASSWORD ||
  process.env.E2E_TEST_PASSWORD ||
  process.env.TEST_E2E_PASSWORD ||
  'Aa!23456z!';

const config = {
  apiUrl: process.env.TEST_API_URL || 'http://localhost:5032',
};

const selectors = {
  loginEmail: '#email',
  loginPassword: '#password',
  loginSubmit: 'button[type="submit"]',
  creditsValue: '.credits-section .credits-card h3',
};

async function fetchJson(request, url, init) {
  const res = await request.fetch(url, init);
  let json = {};
  try {
    json = await res.json();
  } catch (_err) {
    json = {};
  }
  return { status: res.status(), ok: res.ok(), json };
}

function extractCredits(text) {
  if (!text) throw new Error('Credits element missing text');
  const match = text.match(/(\d+)/);
  if (!match) throw new Error(`Unable to parse credits from "${text}"`);
  return parseInt(match[1], 10);
}

test.describe('Credit purchase idempotency (UI-assisted)', () => {
  test.skip(!email || !password, 'Set CREDIT_TEST_EMAIL and CREDIT_TEST_PASSWORD to run this suite.');

  test('shows only one award when purchase is called twice with same transaction id', async ({
    page,
    request,
  }) => {
    // API login to get token for credit/status calls
    const loginResp = await fetchJson(request, `${config.apiUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: JSON.stringify({ email, password }),
    });
    expect(loginResp.ok).toBeTruthy();
    const token = loginResp.json?.token;
    expect(token).toBeTruthy();
    const authHeaders = {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    };

    // Payment config: require simulation mode to avoid real charges
    const paymentConfigResp = await fetchJson(request, `${config.apiUrl}/api/credit/payment-config`);
    expect(paymentConfigResp.ok).toBeTruthy();
    const paymentConfig = paymentConfigResp.json?.data ?? paymentConfigResp.json;
    const simulation = paymentConfig?.paymentSimulation || {};
    const stripe = paymentConfig?.stripe || {};
    const simulationAllowed = simulation.enabled && simulation.skipStripeIntegration;
    const stripeMandatory = stripe.enabled && !simulationAllowed;
    test.skip(stripeMandatory, 'Stripe live mode enabled; skipping idempotency simulation UI test.');

    // Package info
    const packagesResp = await fetchJson(request, `${config.apiUrl}/api/credit/packages`);
    expect(packagesResp.ok).toBeTruthy();
    const packages = packagesResp.json?.data ?? packagesResp.json;
    expect(Array.isArray(packages) && packages.length > 0).toBeTruthy();
    const starter = packages.find(p => p.name?.toLowerCase().includes('starter')) ?? packages[0];
    const packageId = starter.id;
    const packageCredits = starter.totalCredits ?? starter.credits ?? 0;

    const statusBeforeResp = await fetchJson(request, `${config.apiUrl}/api/credit/status`, {
      method: 'GET',
      headers: authHeaders,
    });
    expect(statusBeforeResp.ok).toBeTruthy();
    const statusBefore = statusBeforeResp.json?.data ?? statusBeforeResp.json;
    const purchasedBefore = statusBefore.purchasedCredits ?? 0;

    // Perform purchase twice with same transaction id
    const paymentTransactionId = `sim_ui_idem_${Date.now()}`;
    const purchaseBody = JSON.stringify({ packageId, paymentTransactionId });

    const firstPurchase = await fetchJson(request, `${config.apiUrl}/api/credit/purchase`, {
      method: 'POST',
      headers: authHeaders,
      data: purchaseBody,
    });
    expect(firstPurchase.ok).toBeTruthy();

    const secondPurchase = await fetchJson(request, `${config.apiUrl}/api/credit/purchase`, {
      method: 'POST',
      headers: authHeaders,
      data: purchaseBody,
    });
    expect(secondPurchase.status).toBeLessThan(500);

    const statusAfterResp = await fetchJson(request, `${config.apiUrl}/api/credit/status`, {
      method: 'GET',
      headers: authHeaders,
    });
    expect(statusAfterResp.ok).toBeTruthy();
    const statusAfter = statusAfterResp.json?.data ?? statusAfterResp.json;
    const purchasedAfter = statusAfter.purchasedCredits ?? 0;
    const totalAfter = statusAfter.totalCredits ?? purchasedAfter;

    expect(purchasedAfter).toBe(purchasedBefore + packageCredits);

    // Login through UI and verify displayed credits align with API
    await page.goto('/auth/login');
    await page.fill(selectors.loginEmail, email);
    await page.fill(selectors.loginPassword, password);
    await Promise.all([
      page.waitForURL('**/app/**', { timeout: 45000 }),
      page.click(selectors.loginSubmit),
    ]);

    await page.goto('/app/enhance');
    const locator = page.locator(selectors.creditsValue).first();
    await locator.waitFor({ timeout: 20000 });
    const displayedCredits = extractCredits(await locator.textContent());
    expect(displayedCredits).toBe(totalAfter);
  });
});
