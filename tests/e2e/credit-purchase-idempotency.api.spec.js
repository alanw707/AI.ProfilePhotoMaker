const { test, expect } = require('@playwright/test');

const config = {
  apiUrl: process.env.TEST_API_URL || 'http://localhost:5032',
  password: process.env.TEST_E2E_PASSWORD || 'Aa!23456z!',
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

test.describe('Credit purchase idempotency (API)', () => {
  test('awards credits only once for the same transaction id', async ({ request }) => {
    // 1) Check payment config; skip if Stripe required (no simulation fallback)
    const paymentConfigResp = await fetchJson(request, `${config.apiUrl}/api/credit/payment-config`);
    expect(paymentConfigResp.ok).toBeTruthy();
    const paymentConfig = paymentConfigResp.json?.data ?? paymentConfigResp.json;
    const simulation = paymentConfig?.paymentSimulation || {};
    const stripe = paymentConfig?.stripe || {};
    const simulationAllowed = simulation.enabled && simulation.skipStripeIntegration;
    const stripeMandatory = stripe.enabled && !simulationAllowed;
    test.skip(stripeMandatory, 'Stripe live mode requires real payments; skipping idempotency simulation test.');

    // 2) Get a package (use Starter/first available)
    const packagesResp = await fetchJson(request, `${config.apiUrl}/api/credit/packages`);
    expect(packagesResp.ok).toBeTruthy();
    const packages = packagesResp.json?.data ?? packagesResp.json;
    expect(Array.isArray(packages) && packages.length > 0).toBeTruthy();
    const starter = packages.find(p => p.name?.toLowerCase().includes('starter')) ?? packages[0];
    const packageId = starter.id;
    const packageCredits = starter.totalCredits ?? starter.credits ?? 0;
    expect(packageId).toBeTruthy();
    expect(packageCredits).toBeGreaterThan(0);

    // 3) Register a fresh user to isolate balance
    const email = `idempotency-${Date.now()}@example.com`;
    const registerResp = await fetchJson(request, `${config.apiUrl}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: JSON.stringify({
        email,
        password: config.password,
        firstName: 'Idempotency',
        lastName: 'Test',
        gender: 'prefer-not-to-say',
        ethnicity: 'other',
        ageConfirmed: true,
      }),
    });
    expect(registerResp.ok).toBeTruthy();
    const token = registerResp.json?.token;
    expect(token).toBeTruthy();
    const authHeaders = {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    };

    // Helper to fetch credit status
    const getStatus = () =>
      fetchJson(request, `${config.apiUrl}/api/credit/status`, { method: 'GET', headers: authHeaders });

    const statusBeforeResp = await getStatus();
    expect(statusBeforeResp.ok).toBeTruthy();
    const statusBefore = statusBeforeResp.json?.data ?? statusBeforeResp.json;
    const creditsBefore = statusBefore.credits ?? 0;

    // 4) Execute purchase twice with the same transaction id
    const paymentTransactionId = `sim_idem_${Date.now()}`;
    const purchaseBody = JSON.stringify({ packageId, paymentTransactionId });

    const firstPurchase = await fetchJson(request, `${config.apiUrl}/api/credit/purchase`, {
      method: 'POST',
      headers: authHeaders,
      data: purchaseBody,
    });
    expect(firstPurchase.ok).toBeTruthy();
    expect(firstPurchase.json?.success ?? firstPurchase.json?.success === undefined).toBeTruthy();

    const secondPurchase = await fetchJson(request, `${config.apiUrl}/api/credit/purchase`, {
      method: 'POST',
      headers: authHeaders,
      data: purchaseBody,
    });
    // Should return success or pending, but not double-add credits
    expect(secondPurchase.status).toBeLessThan(500);

    // 5) Validate balance increased only once
    const statusAfterResp = await getStatus();
    expect(statusAfterResp.ok).toBeTruthy();
    const statusAfter = statusAfterResp.json?.data ?? statusAfterResp.json;
    const creditsAfter = statusAfter.credits ?? 0;
    expect(creditsAfter).toBe(creditsBefore + packageCredits);

    // 6) Validate history has single entry (API does not expose transaction ids)
    const historyResp = await fetchJson(request, `${config.apiUrl}/api/credit/history`, {
      method: 'GET',
      headers: authHeaders,
    });
    expect(historyResp.ok).toBeTruthy();
    const history = historyResp.json?.data ?? historyResp.json ?? [];
    expect(history.length).toBe(1);
    expect(history[0].creditsAwarded).toBe(packageCredits);
  });
});
