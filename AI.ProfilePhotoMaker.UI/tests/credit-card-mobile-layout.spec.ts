import { test, expect, Page, Route } from '@playwright/test';

const iPhone13DeviceOptions = {
  viewport: { width: 390, height: 664 },
  userAgent:
    'Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Mobile/15E148 Safari/604.1',
  deviceScaleFactor: 3,
  isMobile: true,
  hasTouch: true,
};

const iPadPro11DeviceOptions = {
  viewport: { width: 834, height: 1194 },
  userAgent:
    'Mozilla/5.0 (iPad; CPU OS 12_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Mobile/15E148 Safari/604.1',
  deviceScaleFactor: 2,
  isMobile: true,
  hasTouch: true,
};

const pixel5DeviceOptions = {
  viewport: { width: 393, height: 727 },
  userAgent:
    'Mozilla/5.0 (Linux; Android 11; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.7258.5 Mobile Safari/537.36',
  deviceScaleFactor: 2.75,
  isMobile: true,
  hasTouch: true,
};

interface ApiResponse<T> {
  success: boolean;
  data: T;
  error?: { code: string; message: string } | null;
}

interface LayoutMetrics {
  columnCount: number;
  gridTemplateColumns: string;
  cardRect: { top: number; left: number; bottom: number; width: number };
  billingRect: { top: number; left: number; bottom: number; width: number };
  viewportWidth: number;
}

const mockPackages: ApiResponse<any[]> = {
  success: true,
  data: [
    {
      id: 2,
      code: 'starter_package',
      name: 'Starter Package',
      description:
        'Three profile-photo candidates, best shot selector, basic adjustment, and selected platform exports.',
      price: 9.99,
      currency: 'USD',
      internalCreditPackageId: 101,
      includedCandidateCount: 3,
      includedRefinementCount: 2,
      includedPremiumAugmentationCount: 0,
      includesPlatformExportKit: true,
      includesScoreDelta: false,
      displayOrder: 2,
      highlights: ['3 candidate photos', 'Platform export kit included', '2 guided refinements'],
    },
  ],
  error: null,
};

const mockStatus: ApiResponse<any> = {
  success: true,
  data: {
    totalCredits: 0,
    weeklyCredits: 5,
    purchasedCredits: 0,
    lastCreditReset: new Date().toISOString(),
    nextResetDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
  },
  error: null,
};

const mockPaymentConfig: ApiResponse<any> = {
  success: true,
  data: {
    paymentSimulation: {
      enabled: false,
      skipStripeIntegration: false,
      reason: null,
    },
  },
  error: null,
};

const mockPaymentIntent = {
  success: true,
  data: {
    paymentIntentId: 'pi_mock_123',
    clientSecret: 'cs_test_mock_secret',
    publishableKey: 'pk_test_mock_key',
    isSimulation: false,
  },
};

async function stubStripe(page: Page) {
  await page.route('https://js.stripe.com/v3', async (route: Route) => {
    const body = `(() => {
      window.Stripe = function() {
        return {
          elements() {
            return {
              create() {
                return {
                  mount(target) {
                    const el = typeof target === 'string' ? document.querySelector(target) : target;
                    if (el && !el.querySelector('.mock-stripe-element')) {
                      const mock = document.createElement('div');
                      mock.className = 'mock-stripe-element';
                      mock.textContent = 'Mock Stripe Element';
                      el.appendChild(mock);
                    }
                  },
                  destroy() {},
                  on() {},
                };
              },
            };
          },
          async confirmCardPayment() {
            return { paymentIntent: { id: 'pi_mock_confirmed' } };
          },
        };
      };
    })();`;

    await route.fulfill({
      status: 200,
      headers: { 'content-type': 'application/javascript' },
      body,
    });
  });
}

async function mockCreditApis(page: Page) {
  await stubStripe(page);

  await page.route('**/api/profilephotoworkflow/packages', route => {
    route.fulfill({
      status: 200,
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(mockPackages),
    });
  });

  await page.route('**/api/credit/status', route => {
    route.fulfill({
      status: 200,
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(mockStatus),
    });
  });

  await page.route('**/api/credit/payment-config', route => {
    route.fulfill({
      status: 200,
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(mockPaymentConfig),
    });
  });

  await page.route('**/api/credit/create-payment-intent', route => {
    route.fulfill({
      status: 200,
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(mockPaymentIntent),
    });
  });
}

async function seedCookieConsent(page: Page) {
  await page.addInitScript(() => {
    window.localStorage.setItem(
      'cookie-consent-v1',
      JSON.stringify({
        version: 1,
        updatedAt: new Date().toISOString(),
        preferences: true,
        analytics: true,
        marketing: true,
      })
    );
  });
}

async function openCardForm(page: Page) {
  await mockCreditApis(page);
  await seedCookieConsent(page);

  await page.goto('/pricing');
  await page.waitForLoadState('networkidle');

  const purchaseButton = page.getByRole('button', { name: 'Choose Package' }).first();
  await expect(purchaseButton).toBeVisible();
  await purchaseButton.click();

  await expect(page.getByRole('heading', { name: /Complete Payment/ })).toBeVisible();
  await expect(page.locator('.payment-form .card-block')).toBeVisible();
  await expect(page.locator('.payment-form .billing-block')).toBeVisible();
}

async function getLayoutMetrics(page: Page): Promise<LayoutMetrics> {
  const paymentForm = page.locator('.payment-form');
  const cardBlock = paymentForm.locator('.card-block');
  const billingBlock = paymentForm.locator('.billing-block');

  const gridTemplateColumns = await paymentForm.evaluate(el =>
    getComputedStyle(el).getPropertyValue('grid-template-columns')
  );
  const columnCount = gridTemplateColumns
    .split(' ')
    .map(part => part.trim())
    .filter(Boolean).length;

  const cardRect = await cardBlock.evaluate(el => {
    const rect = el.getBoundingClientRect();
    return { top: rect.top, left: rect.left, bottom: rect.bottom, width: rect.width };
  });

  const billingRect = await billingBlock.evaluate(el => {
    const rect = el.getBoundingClientRect();
    return { top: rect.top, left: rect.left, bottom: rect.bottom, width: rect.width };
  });

  const viewportWidth = await page.evaluate(() => window.innerWidth);

  return { columnCount, gridTemplateColumns, cardRect, billingRect, viewportWidth };
}

function expectStackedLayout(metrics: LayoutMetrics) {
  // Billing section should appear below the card section when stacked.
  expect(metrics.columnCount).toBe(1);
  expect(Math.abs(metrics.billingRect.left - metrics.cardRect.left)).toBeLessThan(4);
  expect(metrics.billingRect.top).toBeGreaterThanOrEqual(metrics.cardRect.bottom - 1);
}

async function forceDarkTheme(page: Page) {
  await page.addInitScript(() => {
    window.localStorage.setItem('theme', 'dark');
    document.documentElement.setAttribute('data-theme', 'dark');
  });
}

test.describe('Credit purchase layout', () => {
  test('uses dim placeholder text for dark-theme billing inputs', async ({ page }) => {
    await forceDarkTheme(page);
    await openCardForm(page);

    const billingName = page.locator('input[name="billingName"]');
    const emptyStyle = await billingName.evaluate(el => {
      const inputStyle = getComputedStyle(el);
      const placeholderStyle = getComputedStyle(el, '::placeholder');
      return {
        isPlaceholderShown: el.matches(':placeholder-shown'),
        inputTextFillColor: inputStyle.webkitTextFillColor,
        placeholderColor: placeholderStyle.color,
        placeholderOpacity: placeholderStyle.opacity,
      };
    });

    await billingName.fill('Typed Name');
    const typedStyle = await billingName.evaluate(el => {
      const inputStyle = getComputedStyle(el);
      return {
        isPlaceholderShown: el.matches(':placeholder-shown'),
        inputColor: inputStyle.color,
        inputTextFillColor: inputStyle.webkitTextFillColor,
      };
    });

    expect(emptyStyle.isPlaceholderShown).toBe(true);
    expect(emptyStyle.placeholderColor).toBe('rgba(148, 163, 184, 0.38)');
    expect(emptyStyle.placeholderOpacity).toBe('1');
    expect(emptyStyle.inputTextFillColor).toBe('rgba(148, 163, 184, 0.38)');
    expect(typedStyle.isPlaceholderShown).toBe(false);
    expect(typedStyle.inputColor).toBe('rgba(248, 250, 252, 0.94)');
    expect(typedStyle.inputTextFillColor).toBe('rgba(248, 250, 252, 0.94)');
  });

  test.describe('iPhone 13 viewport', () => {
    test.use({ ...iPhone13DeviceOptions });

    test('stacks card and billing blocks vertically', async ({ page }) => {
      await openCardForm(page);
      const metrics = await getLayoutMetrics(page);

      expect(metrics.viewportWidth).toBeLessThanOrEqual(430);
      expectStackedLayout(metrics);
    });
  });

  test.describe('iPad Air viewport', () => {
    test.use({ ...iPadPro11DeviceOptions });

    test('stacks card and billing blocks vertically', async ({ page }) => {
      await openCardForm(page);
      const metrics = await getLayoutMetrics(page);

      expect(metrics.viewportWidth).toBeLessThanOrEqual(1100);
      expectStackedLayout(metrics);
    });
  });

  test.describe('Pixel 5 viewport', () => {
    test.use({ ...pixel5DeviceOptions });

    test('stacks card and billing blocks vertically', async ({ page }) => {
      await openCardForm(page);
      const metrics = await getLayoutMetrics(page);

      expect(metrics.viewportWidth).toBeLessThanOrEqual(430);
      expectStackedLayout(metrics);
    });
  });
});
