import { test, expect } from '@playwright/test';

test('package load failure leaves a retryable state', async ({ page }) => {
  let failRequest = true;
  await page.route('**/api/profilephotoworkflow/packages', route =>
    route.fulfill({
      status: failRequest ? 503 : 200,
      contentType: 'application/json',
      body: JSON.stringify(failRequest
        ? { success: false }
        : { success: true, data: [{ id: 1, code: 'starter_package', name: 'Starter Package', price: 9.99, currency: 'USD', includedCandidateCount: 3, includedRefinementCount: 2, includedExportCount: 3, highlights: [] }] }),
    })
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

test('legacy dashboard links preserve payment-return query parameters', async ({ page }) => {
  await page.goto('/dashboard?e2eAuthBypass=1&payment=success&package=pro');
  await expect(page).toHaveURL(url =>
    url.pathname === '/app/enhance' &&
    url.searchParams.get('payment') === 'success' &&
    url.searchParams.get('package') === 'pro'
  );
});
