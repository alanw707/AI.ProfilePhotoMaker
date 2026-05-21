const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { getHostAndPort, getHostname, isStagingAppBaseUrl, tryParseUrl } = require('./setup/url-utils');

const envFilePath = path.resolve(__dirname, '..', '..', '.env');

function readEnvValue(key) {
  if (!fs.existsSync(envFilePath)) {
    return undefined;
  }

  const raw = fs.readFileSync(envFilePath, 'utf8');
  const lines = raw.split(/\r?\n/);
  const line = lines.find(entry => entry.trim().startsWith(`${key}=`));
  if (!line) {
    return undefined;
  }

  const value = line.slice(key.length + 1).trim();
  return value.replace(/^"(.*)"$/, '$1');
}

const defaultEmail = process.env.STRIPE_E2E_EMAIL || 'testuser@example.com';
const defaultPassword = process.env.STRIPE_E2E_PASSWORD || 'TestPassword123!';

const testAccount = {
  email:
    process.env.TEST_ACCOUNT_EMAIL ||
    process.env.TEST_ACCOUNT ||
    process.env.DASHBOARD_E2E_EMAIL ||
    readEnvValue('Test_Account') ||
    defaultEmail,
  password:
    process.env.TEST_ACCOUNT_PASSWORD ||
    process.env.TEST_ACCOUNT_PW ||
    process.env.DASHBOARD_E2E_PASSWORD ||
    readEnvValue('Test_Account_Password') ||
    defaultPassword,
};

const appBaseUrl = process.env.TEST_BASE_URL || 'http://localhost:4200';
const parsedAppBaseUrl = tryParseUrl(appBaseUrl);
const appBaseUrlOrigin = parsedAppBaseUrl ? parsedAppBaseUrl.origin : appBaseUrl;
const appBaseUrlRoot = appBaseUrlOrigin.replace(/\/+$/, '');
const apiBaseUrl = resolveApiBaseUrl(appBaseUrl);
const hasConfiguredCredentials = Boolean(
  process.env.TEST_ACCOUNT_EMAIL ||
    process.env.TEST_ACCOUNT ||
    process.env.DASHBOARD_E2E_EMAIL ||
    readEnvValue('Test_Account')
);
const hasConfiguredPassword = Boolean(
  process.env.TEST_ACCOUNT_PASSWORD ||
    process.env.TEST_ACCOUNT_PW ||
    process.env.DASHBOARD_E2E_PASSWORD ||
    readEnvValue('Test_Account_Password')
);

function buildAppUrl(pathname = '/') {
  const normalized = pathname.startsWith('/') ? pathname : `/${pathname}`;
  return `${appBaseUrlRoot}${normalized}`;
}

function resolveApiBaseUrl(appBaseUrlValue) {
  const explicit = process.env.TEST_API_BASE_URL || process.env.API_BASE_URL;
  if (explicit) {
    return explicit.replace(/\/+$/, '');
  }

  const { hostname, port } = getHostAndPort(appBaseUrlValue);

  if ((hostname === 'localhost' || hostname === '127.0.0.1') && port === '4200') {
    return 'http://localhost:5032';
  }

  if (hostname === 'aiprofilephotomaker.com' || hostname === 'aiprofilephotomaker.com') {
    return 'https://api.aiprofilephotomaker.com';
  }

  if (isStagingAppBaseUrl(appBaseUrlValue)) {
    return 'https://staging-api.aiprofilephotomaker.com';
  }

  if (getHostname(appBaseUrlValue) === 'api.aiprofilephotomaker.com') {
    return appBaseUrlValue.replace(/\/+$/, '');
  }

  return 'https://api.aiprofilephotomaker.com';
}

async function waitForSectionInView(page, sectionId) {
  await expect
    .poll(
      async () =>
        page.evaluate(id => {
          const el = document.getElementById(id);
          if (!el) {
            return { exists: false, inView: false };
          }
          const rect = el.getBoundingClientRect();
          const viewHeight = window.innerHeight || document.documentElement.clientHeight;
          const viewWidth = window.innerWidth || document.documentElement.clientWidth;
          const inView = rect.top < viewHeight && rect.bottom > 0 && rect.left < viewWidth && rect.right > 0;
          return { exists: true, inView };
        }, sectionId),
      { timeout: 5000 }
    )
    .toEqual({ exists: true, inView: true });
}

async function login(page) {
  const candidates = [
    { email: testAccount.email, password: testAccount.password, label: 'configured' },
  ];
  if (testAccount.email !== defaultEmail || testAccount.password !== defaultPassword) {
    candidates.push({ email: defaultEmail, password: defaultPassword, label: 'default' });
  }

  let loginBody = null;
  let token = '';

  for (const candidate of candidates) {
    const loginResponse = await page.request.post(`${apiBaseUrl}/api/auth/login`, {
      data: {
        email: candidate.email,
        password: candidate.password,
        ageConfirmed: true,
      },
    });

    if (!loginResponse.ok()) {
      continue;
    }

    const body = await loginResponse.json();
    if (body?.isSuccess && body?.token) {
      loginBody = body;
      token = body.token;
      break;
    }
  }

  if (!loginBody || !token) {
    throw new Error('Unable to authenticate with configured or default test credentials.');
  }

  await page.context().addCookies([
    {
      name: 'AuthToken',
      value: token,
      url: appBaseUrlOrigin,
      httpOnly: true,
      sameSite: 'Lax',
    },
  ]);

  await page.goto(buildAppUrl('/'));
  await page.evaluate(user => {
    localStorage.setItem('currentUser', JSON.stringify(user));
  }, {
    token,
    email: loginBody.email || testAccount.email,
    firstName: loginBody.firstName || '',
    lastName: loginBody.lastName || '',
  });

  const confirmResponse = await page.request.post(`${apiBaseUrl}/api/auth/dev/confirm-email`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!confirmResponse.ok()) {
    console.warn('Dev confirm email failed:', confirmResponse.status());
  }

  await page.goto(buildAppUrl('/app/enhance'));
  await expect(page.locator('.gallery-page-container')).toBeVisible({ timeout: 15000 });
}

test.describe('UI navigation smoke - public', () => {
  test('landing nav and hero actions scroll to the right sections', async ({ page }) => {
    await page.goto(buildAppUrl('/'));

    const pricingButton = page.getByRole('button', { name: /^Pricing$/i }).first();
    await pricingButton.click();
    await waitForSectionInView(page, 'pricing');

    const reviewsButton = page.getByRole('button', { name: /^Reviews$/i }).first();
    await reviewsButton.click();
    await waitForSectionInView(page, 'testimonials');

    const viewExamples = page.getByRole('button', { name: /view examples/i }).first();
    await viewExamples.click();
    await waitForSectionInView(page, 'examples');
  });

  test('enhancement CTAs route guests to login', async ({ page }) => {
    await page.context().clearCookies();
    await page.goto(buildAppUrl('/'));
    await page.evaluate(() => localStorage.clear());
    const ctaButtons = page.getByRole('button', { name: /^Start with enhancements$/i });
    const count = await ctaButtons.count();
    expect(count).toBeGreaterThan(0);

    for (let i = 0; i < count; i += 1) {
      await page.goto(buildAppUrl('/'));
      const button = page.getByRole('button', { name: /^Start with enhancements$/i }).nth(i);
      await button.scrollIntoViewIfNeeded();
      await Promise.all([
        page.waitForURL(/\/auth\/login/, { timeout: 15000 }),
        button.click(),
      ]);
      const returnUrl = new URL(page.url()).searchParams.get('returnUrl');
      expect(returnUrl).toBe('/app/enhance');
    }
  });

  test('footer links land on the right pages or sections', async ({ page }) => {
    await page.goto(buildAppUrl('/'));
    const footer = page.locator('footer');
    await footer.scrollIntoViewIfNeeded();

    await Promise.all([
      page.waitForURL('**/features', { timeout: 15000 }),
      footer.getByRole('link', { name: /features/i }).click(),
    ]);
    await expect(page.getByRole('heading', { name: /AI Headshot Features/i })).toBeVisible({
      timeout: 15000,
    });

    await page.goto(buildAppUrl('/'));
    await footer.scrollIntoViewIfNeeded();
    await Promise.all([
      page.waitForURL('**/examples', { timeout: 15000 }),
      footer.getByRole('link', { name: /examples/i }).click(),
    ]);
    await expect(page.getByRole('heading', { name: /AI Headshot Examples/i })).toBeVisible({
      timeout: 15000,
    });

    await page.goto(buildAppUrl('/'));
    await footer.scrollIntoViewIfNeeded();
    await Promise.all([
      page.waitForURL('**/help', { timeout: 15000 }),
      footer.getByRole('link', { name: /help/i }).click(),
    ]);
    await expect(page.getByRole('heading', { name: /help and faq/i })).toBeVisible({
      timeout: 15000,
    });

    await page.goto(buildAppUrl('/'));
    await footer.scrollIntoViewIfNeeded();
    await Promise.all([
      page.waitForURL('**/pricing**', { timeout: 15000 }),
      footer.getByRole('link', { name: /pricing/i }).click(),
    ]);
    await expect(page.getByRole('heading', { name: /pricing plans/i })).toBeVisible({
      timeout: 15000,
    });

    await page.goto(buildAppUrl('/'));
    await footer.scrollIntoViewIfNeeded();
    await footer.getByRole('button', { name: /cookie preferences/i }).click();
    await expect(page.getByRole('heading', { name: /cookie preferences/i })).toBeVisible({
      timeout: 10000,
    });
  });
});

test.describe('UI navigation smoke - authenticated', () => {
  test.skip(
    !hasConfiguredCredentials || !hasConfiguredPassword,
    'Test account credentials not configured.'
  );

  test('app header links route to the correct pages', async ({ page }) => {
    await login(page);

    await Promise.all([
      page.waitForURL('**/app/enhance', { timeout: 15000 }),
      page.getByRole('link', { name: /photo transform/i }).click(),
    ]);
    await expect(page.getByRole('heading', { name: /photo transform/i })).toBeVisible({
      timeout: 15000,
    });

    await Promise.all([
      page.waitForURL('**/app/gallery', { timeout: 15000 }),
      page.getByRole('link', { name: /gallery/i }).click(),
    ]);
    await expect(page.getByRole('heading', { name: /photo gallery/i })).toBeVisible({ timeout: 15000 });

    await Promise.all([
      page.waitForURL('**/app/settings', { timeout: 15000 }),
      page.getByRole('link', { name: /settings/i }).click(),
    ]);
    await expect(page.getByRole('heading', { name: /account settings/i })).toBeVisible({
      timeout: 15000,
    });

    await Promise.all([
      page.waitForURL('**/app/support', { timeout: 15000 }),
      page.getByRole('link', { name: /support/i }).click(),
    ]);
    await expect(page.getByRole('heading', { name: /support/i })).toBeVisible({ timeout: 15000 });

    await Promise.all([
      page.waitForURL('**/app/enhance', { timeout: 15000 }),
      page.getByRole('link', { name: /headshot studio/i }).click(),
    ]);
    await expect(page.locator('.gallery-page-container')).toBeVisible({ timeout: 15000 });
  });

  test('enhancement CTAs route authenticated users to enhance', async ({ page }) => {
    await login(page);
    await page.goto(buildAppUrl('/'));

    const ctaButtons = page.getByRole('button', { name: /^Start with enhancements$/i });
    const count = await ctaButtons.count();
    expect(count).toBeGreaterThan(0);

    for (let i = 0; i < count; i += 1) {
      await page.goto(buildAppUrl('/'));
      const button = page.getByRole('button', { name: /^Start with enhancements$/i }).nth(i);
      await button.scrollIntoViewIfNeeded();
      await Promise.all([
        page.waitForURL('**/app/enhance', { timeout: 15000 }),
        button.click(),
      ]);
    }
  });
});
