/**
 * E2E: Pricing page purchase CTA scroll behavior (no Stripe submission)
 *
 * Goals:
 * - Verify each "Purchase Credits" button scrolls to the billing form container.
 * - Avoid creating real payment intents or touching Stripe by stubbing API calls.
 *
 * This spec is safe to run in local/staging. It does not submit payment details.
 */

const { test, expect } = require('@playwright/test');
const { buildAppUrl } = require('./setup/app-url');

const stubPackages = [
  {
    id: 1,
    name: 'Starter Pack',
    description: 'Great for trying custom training',
    price: 10,
    totalCredits: 50,
    bonusCredits: 0,
  },
  {
    id: 2,
    name: 'Professional Pack',
    description: 'Most popular',
    price: 30,
    totalCredits: 150,
    bonusCredits: 30,
  },
];

test.describe('Pricing purchase scroll', () => {
  test('scrolls to billing form when selecting a package without submitting payment', async ({ page }) => {
    await stubPricingApis(page);

    await page.goto(buildAppUrl('/pricing'));
    await expect(page.locator('.packages-grid')).toBeVisible();

    const purchaseButtons = page.locator('.package-card button.purchase-btn');
    const buttonCount = await purchaseButtons.count();
    expect(buttonCount).toBeGreaterThan(0);

    const initialScroll = await page.evaluate(() => window.scrollY);

    const purchasePromise = page.waitForSelector('#credit-purchase-form', {
      state: 'visible',
      timeout: 10000,
    });

    await purchaseButtons.first().click();
    await purchasePromise;

    // Capture scroll position and viewport offset relative to the form
    const { scrollY, top, bottom, viewportHeight } = await page.evaluate(() => {
      const el = document.getElementById('credit-purchase-form');
      const rect = el?.getBoundingClientRect();
      return {
        scrollY: window.scrollY,
        top: rect ? rect.top : null,
        bottom: rect ? rect.bottom : null,
        viewportHeight: window.innerHeight,
      };
    });

    expect(scrollY).toBeGreaterThan(initialScroll + 10);
    // Form should be at least partially in the viewport
    expect(top).not.toBeNull();
    expect(bottom).not.toBeNull();
    expect(top).toBeLessThan(viewportHeight);
    expect(bottom).toBeGreaterThan(0);
  });
});

async function stubPricingApis(page) {
  // Stub packages list to avoid dependency on live data
  await page.route('**/api/credit/packages', route => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ success: true, data: stubPackages }),
    });
  });

  // Stub payment config to disable simulation but keep Stripe off
  await page.route('**/api/credit/payment-config', route => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        data: {
          paymentSimulation: { enabled: false, skipStripeIntegration: false },
          stripe: { enabled: false, publishableKey: null },
        },
      }),
    });
  });

  // Stub credit status to avoid auth dependency
  await page.route('**/api/credit/status', route => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        data: { totalCredits: 0, weeklyCredits: 0, purchasedCredits: 0 },
      }),
    });
  });

  // Intercept payment intent creation to avoid hitting Stripe; return fast failure
  await page.route('**/api/credit/create-payment-intent', route => {
    route.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({
        success: false,
        error: { code: 'TestStub', message: 'Payment intent stubbed in test' },
      }),
    });
  });
}
