import { test, expect, Page } from '@playwright/test';

type ImagesReply = { status: number; json: unknown };

const IMAGE = {
  id: 1,
  originalImageUrl: '/uploads/a.jpg',
  processedImageUrl: '/uploads/a.jpg',
  style: 'linkedin',
  isGenerated: true,
  createdAt: '2026-10-01T00:00:00Z',
};

async function mockApi(page: Page, images: ImagesReply) {
  await page.route('**/*', route => {
    const host = new URL(route.request().url()).hostname;
    return host === 'localhost' || host === '127.0.0.1' ? route.continue() : route.abort();
  });
  await page.route('**/uploads/**', route =>
    route.fulfill({
      contentType: 'image/svg+xml',
      body: '<svg xmlns="http://www.w3.org/2000/svg" width="8" height="8"/>',
    })
  );
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    if (url.endsWith('/image/images')) return route.fulfill(images);
    const responses: Record<string, unknown> = {
      '/config/client': { features: {} },
      '/auth/account-status': { emailConfirmed: true },
      '/auth/user-roles': [],
      '/credit/status': { credits: 0, lastCreditReset: '2026-01-01', nextResetDate: '2026-02-01' },
      '/profile': { firstName: 'Test', lastName: 'User' },
    };
    const key = Object.keys(responses).find(k => url.endsWith(k));
    return route.fulfill({
      json: { success: true, isAuthenticated: true, data: key ? responses[key] : {}, error: null },
    });
  });
  await page.setViewportSize({ width: 390, height: 844 });
  const loaded = page.waitForResponse(r => r.url().includes('/image/images'));
  await page.goto('/app/gallery?e2eAuthBypass=1');
  await loaded;
}

test('a user with no photos sees a single empty state', async ({ page }) => {
  await mockApi(page, { status: 200, json: { success: true, data: { images: [], totalImages: 0 } } });
  await expect(page.getByRole('heading', { name: 'No Photos Yet' })).toBeVisible();
  await expect(page.locator('app-photo-gallery')).toHaveCount(0);
  await expect.poll(() => page.locator('.empty-state').count()).toBe(1);
});

test('a user with photos sees the grid and no empty state', async ({ page }) => {
  await mockApi(page, {
    status: 200,
    json: { success: true, data: { images: [IMAGE], totalImages: 1 } },
  });
  await expect(page.locator('app-photo-gallery')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'No Photos Yet' })).toHaveCount(0);
});

test('a failed load shows an error with retry, not the empty state', async ({ page }) => {
  await mockApi(page, { status: 500, json: { success: false, error: { message: 'boom' } } });
  await expect(page.locator('[data-gallery-load-error]')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'No Photos Yet' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible();
});
