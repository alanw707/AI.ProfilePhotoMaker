import { test, expect } from '@playwright/test';

// A user with no photos sees one empty state with actions, not the grid's own empty message too.
test('gallery shows a single empty state when the user has no photos', async ({ page }) => {
  await page.route('**/*', route => {
    const host = new URL(route.request().url()).hostname;
    return host === 'localhost' || host === '127.0.0.1' ? route.continue() : route.abort();
  });
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    const responses: Record<string, unknown> = {
      '/config/client': { features: {} },
      '/auth/account-status': { emailConfirmed: true },
      '/auth/user-roles': [],
      '/credit/status': { credits: 0, lastCreditReset: '2026-01-01', nextResetDate: '2026-02-01' },
      '/profile': { firstName: 'Test', lastName: 'User' },
    };
    const key = Object.keys(responses).find(k => url.endsWith(k));
    const data = key ? responses[key] : [];
    return route.fulfill({ json: { success: true, isAuthenticated: true, data, images: [], error: null } });
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/app/gallery?e2eAuthBypass=1');
  await expect(page.getByRole('heading', { name: 'No Photos Yet' })).toBeVisible();
  await expect(page.getByText('No images yet')).toHaveCount(0);
  await expect(page.locator('.empty-state')).toHaveCount(1);
});
