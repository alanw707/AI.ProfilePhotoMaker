import { test, expect } from '@playwright/test';

test('package load failure leaves a retryable state', async ({ page }) => {
  let failRequest = true;
  await page.route('**/api/profilephotoworkflow/packages', route =>
    failRequest
      ? route.fulfill({ status: 503, contentType: 'application/json', body: '{"success":false}' })
      : route.continue()
  );
  await page.goto('/pricing');
  await expect(page.getByRole('button', { name: 'Retry' })).toBeVisible();
  failRequest = false;
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(page.getByRole('heading', { name: 'Starter Package', exact: true })).toBeVisible();
});

for (const path of ['/pricing', '/packages']) {
  test(`${path} loads an interactive package page directly and after refresh`, async ({ page }) => {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: 'Profile Photo Packages', exact: true })).toBeVisible();
    await expect(page.locator('app-credit-packages')).toBeVisible();
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Profile Photo Packages', exact: true })).toBeVisible();
    await expect(page.locator('app-credit-packages')).toBeVisible();
  });
}
