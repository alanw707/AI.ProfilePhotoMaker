/**
 * E2E: Support form responsive smoke test (authenticated).
 *
 * Creates a throwaway user, navigates to `/app/support`, and captures screenshots
 * across common mobile/tablet/desktop viewports. Also asserts no horizontal overflow
 * and that the primary actions are visible.
 *
 * Enable via: RUN_SUPPORT_RESPONSIVE=true
 */

const { test, expect } = require('@playwright/test');

test.describe('Support form responsive', () => {
  test('renders correctly across viewports', async ({ page }, testInfo) => {
    const timestamp = Date.now();
    const email = `support.responsive+${timestamp}@example.com`;
    const password = 'Test1234!';

    await page.goto('/auth/register');
    await expect(page.getByRole('heading', { name: /Create Account/i })).toBeVisible();

    await page.fill('#firstName', 'Local');
    await page.fill('#lastName', 'Tester');
    await page.selectOption('#gender', 'prefer-not-to-say');
    await page.selectOption('#ethnicity', 'other');
    await page.fill('#email', email);
    await page.fill('#password', password);
    await page.fill('#confirmPassword', password);

    await page.click('button[type="submit"]');
    await page.waitForURL('**/app/**', { timeout: 30000 });

    await page.goto('/app/support');
    await expect(page.locator('.page-header h1')).toHaveText(/Support/i);
    await expect(page.locator('.support-card')).toBeVisible();

    const viewports = [
      { name: 'iphone-se', width: 375, height: 667 },
      { name: 'iphone-13', width: 390, height: 844 },
      { name: 'pixel-5', width: 393, height: 851 },
      { name: 'ipad-mini', width: 768, height: 1024 },
      { name: 'desktop', width: 1280, height: 720 },
    ];

    for (const vp of viewports) {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.waitForTimeout(150);

      const hasHorizontalOverflow = await page.evaluate(() => {
        const doc = document.documentElement;
        return doc.scrollWidth > window.innerWidth + 1;
      });
      expect(hasHorizontalOverflow).toBeFalsy();

      await expect(page.locator('#category')).toBeVisible();
      await expect(page.locator('#message')).toBeVisible();
      await expect(page.getByRole('button', { name: /Back/i })).toBeVisible();
      await expect(page.getByRole('button', { name: /Send/i })).toBeVisible();

      const screenshotPath = testInfo.outputPath(`support-${vp.name}.png`);
      await page.screenshot({ path: screenshotPath, fullPage: true });
    }
  });
});
