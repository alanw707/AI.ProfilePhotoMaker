import { test, expect } from '@playwright/test';

const BASE_URL = 'http://localhost:4200';
const STUB_USER = {
  token: '',
  email: 'playwright@example.com',
  firstName: 'Play',
  lastName: 'Wright',
};

async function bootstrapSession(page) {
  await page.addInitScript(user => {
    localStorage.setItem('currentUser', JSON.stringify(user));
  }, STUB_USER);

  await page.route('**/api/**', route => {
    const url = route.request().url();

    if (url.includes('/api/auth/validate-session')) {
      route.fulfill({ status: 200, contentType: 'text/plain', body: 'OK' });
      return;
    }

    if (url.includes('/api/auth/account-status')) {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: { emailConfirmed: true }, error: null }),
      });
      return;
    }

    if (url.includes('/api/auth/profile-completion-status')) {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          isCompleted: true,
          hasFirstName: true,
          hasLastName: true,
          hasGender: true,
          hasEthnicity: true,
        }),
      });
      return;
    }

    const method = route.request().method();
    const body = method === 'GET' ? '{}' : '{"success":true}';
    route.fulfill({ status: 200, contentType: 'application/json', body });
  });
}

test.describe('Photo Enhancement Experience', () => {
  test.beforeEach(async ({ page }) => {
    await bootstrapSession(page);
  });

  test('renders photo enhancement workspace with seeded session', async ({ page }) => {
    await page.goto(BASE_URL + '/app/enhance');
    await page.waitForLoadState('domcontentloaded');

    await expect(page.locator('app-photo-enhancement')).toBeVisible({ timeout: 5000 });
  });

  test('exposes debug services while on enhancement route', async ({ page }) => {
    await page.goto(BASE_URL + '/app/enhance');
    await page.waitForLoadState('domcontentloaded');

    const debugSummary = await page.evaluate(() => {
      const debug = (window as any).__APP_DEBUG__;
      return {
        auth: !!debug?.services?.authService,
        fileUpload: !!debug?.services?.fileUploadService,
        replicate: !!debug?.services?.replicateService,
      };
    });

    expect(debugSummary.auth).toBe(true);
    expect(debugSummary.fileUpload).toBe(true);
    expect(debugSummary.replicate).toBe(true);
  });
});
