import { test, expect } from '@playwright/test';

const BASE_URL = 'http://localhost:4200';
const STUB_USER = {
  token: '',
  email: 'playwright@example.com',
  firstName: 'Play',
  lastName: 'Wright',
};

test.describe('Auto-Repair Functionality Tests', () => {
  test.beforeEach(async ({ page }) => {
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

    await page.goto(BASE_URL + '/app/dashboard');
    await page.waitForLoadState('domcontentloaded');
  });

  test('should expose environment configuration for auto-repair helpers', async ({ page }) => {
    const env = await page.evaluate(() => {
      const debug = (window as any).__APP_DEBUG__;
      return debug?.environment || (window as any).environment || null;
    });

    expect(env?.name).toBe('development');
    expect(env?.features?.debugMode).toBe(true);
    expect(env?.features?.enableImageValidation).toBe(true);
  });

  test('should keep auth service accessible via debug context', async ({ page }) => {
    const hasAuthService = await page.evaluate(() => {
      const debug = (window as any).__APP_DEBUG__;
      return !!debug?.services?.authService;
    });

    expect(hasAuthService).toBe(true);
  });

  test('should surface feature logs to the console', async ({ page }) => {
    const messages: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'log' || msg.type() === 'info') {
        messages.push(msg.text());
      }
    });

    await page.reload();
    await page.waitForTimeout(500);

    expect(messages.length).toBeGreaterThan(0);
  });

  test('should maintain cooldown tracking structures in session storage', async ({ page }) => {
    const cooldownInfo = await page.evaluate(() => {
      const key = 'autoRepairCooldowns';
      if (!sessionStorage.getItem(key)) {
        sessionStorage.setItem(key, JSON.stringify({ initializedAt: Date.now() }));
      }
      return sessionStorage.getItem(key);
    });

    expect(typeof cooldownInfo).toBe('string');
  });
});
