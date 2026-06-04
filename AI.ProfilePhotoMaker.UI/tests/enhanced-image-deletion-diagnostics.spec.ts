import { test, expect } from '@playwright/test';

const BASE_URL = 'http://localhost:4200';
const STUB_USER = {
  token: '',
  email: 'playwright@example.com',
  firstName: 'Play',
  lastName: 'Wright',
};

async function prepareSession(page) {
  await page.addInitScript(user => {
    localStorage.setItem('currentUser', JSON.stringify(user));
  }, STUB_USER);

  await page.route('**/api/auth/validate-session', route => {
    route.fulfill({ status: 200, contentType: 'text/plain', body: 'OK' });
  });

  await page.route('**/api/**', route => {
    if (!route.request().url().includes('/api/auth/validate-session')) {
      const method = route.request().method();
      const body = method === 'GET' ? '{}' : '{"success":true}';
      route.fulfill({ status: 200, contentType: 'application/json', body });
    }
  });
}

test.describe('Enhanced Image Diagnostics', () => {
  test.beforeEach(async ({ page }) => {
    await prepareSession(page);
    await page.goto(BASE_URL + '/app/enhance');
    await page.waitForLoadState('domcontentloaded');
  });

  test('reflects authenticated state when session is seeded', async ({ page }) => {
    const authState = await page.evaluate(() => {
      const debug = (window as any).__APP_DEBUG__;
      const authService = debug?.services?.authService;
      return {
        hasAuthService: !!authService,
        isAuthenticated: authService?.isAuthenticated?.() ?? false,
      };
    });

    expect(authState.hasAuthService).toBe(true);
    expect(authState.isAuthenticated).toBe(true);
  });

  test('returns normalized error details when saving enhanced image fails', async ({ page }) => {
    await page.unroute('**/api/image/save-enhanced').catch(() => {});
    await page.route('**/api/image/save-enhanced', async route => {
      await route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({
          error: {
            message: 'Simulated failure',
          },
        }),
      });
    });

    const errorResult: any = await page.evaluate(async () => {
      const debug = (window as any).__APP_DEBUG__;
      const fileUploadService = debug?.services?.fileUploadService;
      if (!fileUploadService) {
        return { success: false, error: { message: 'FileUploadService unavailable' } };
      }

      return await fileUploadService.saveEnhancedImage(
        'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIW2P4//8/AwAI/AL+g0BGVAAAAABJRU5ErkJggg==',
        'chibi'
      );
    });

    expect(errorResult.success).toBe(false);
    expect(errorResult.error?.code).toBe('SAVE_FAILED');
  });
});
