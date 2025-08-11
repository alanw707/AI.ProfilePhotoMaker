import { test, expect } from '@playwright/test';
import { FRONTEND_APP_URL } from './test-data';

test.describe('Production homepage network - no style-preview API calls', () => {
  test('does not call /api/style-preview/* on load', async ({ page }) => {
    test.setTimeout(60_000);

    const requestedUrls: string[] = [];
    page.on('request', req => {
      requestedUrls.push(req.url());
    });

    await page.goto(FRONTEND_APP_URL, { waitUntil: 'networkidle' });
    // Allow any late network to settle
    await page.waitForTimeout(2000);

    const offending = requestedUrls.filter(u => /\/api\/style-preview(\/|$)/i.test(u));
    expect(offending, 'No /api/style-preview calls should occur in production').toHaveLength(0);
  });
});


