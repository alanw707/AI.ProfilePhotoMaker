import { test, expect, Page } from '@playwright/test';
import { fakeBackend, setup } from './fixtures/career-profile';

test('setup saves profile and goal; reload restores both', async ({ page }) => {
  fakeBackend(page);
  await setup(page);
  await page.reload();
  await expect(page.locator('[data-goal]')).toContainText('Senior analyst');
  await page
    .getByRole('navigation', { name: 'Career steps' })
    .locator('a[href="/app/career/profile"]')
    .click();
  await expect(page.getByLabel('Current title')).toHaveValue('Data analyst');
});
test('editing profile marks goal stale', async ({ page }) => {
  fakeBackend(page);
  await setup(page);
  await page
    .getByRole('navigation', { name: 'Career steps' })
    .locator('a[href="/app/career/profile"]')
    .click();
  await page.getByLabel('Current title').fill('Principal analyst');
  await page.getByLabel('I confirm these facts are accurate').first().check();
  await page.getByRole('button', { name: 'Save professional facts' }).click();
  await expect(page.getByText('Professional facts saved.')).toBeVisible();
  await page.getByRole('link', { name: 'Back to career workspace' }).click();
  await expect(
    page
      .locator('main')
      .getByText(/Needs review/)
      .first()
  ).toBeVisible();
});
test('stale update keeps unsaved input until reload', async ({ page }) => {
  const backend = fakeBackend(page);
  await setup(page);
  await page.goto('/app/career/profile?e2eAuthBypass=1');
  await page.getByLabel('Current title').fill('My unsaved title');
  await page.getByLabel('I confirm these facts are accurate').first().check();
  backend.conflict();
  await page.getByRole('button', { name: 'Save professional facts' }).click();
  await expect(page.getByText(/changed in another tab or device/)).toBeVisible();
  await expect(page.getByLabel('Current title')).toHaveValue('My unsaved title');
  await expect(page.getByRole('button', { name: 'Reload the latest version' })).toBeVisible();
});
test('empty title has summary and invalid control', async ({ page }) => {
  fakeBackend(page);
  await page.goto('/app/career/setup?e2eAuthBypass=1');
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await page.getByRole('button', { name: 'Save facts and continue' }).click();
  await expect(page.locator('[role=alert]')).toContainText('Current title: Required.');
  await expect(page.locator('[role=alert]')).not.toContainText('currentTitle');
  await expect(page.getByLabel('Current title')).toHaveAttribute('aria-invalid', 'true');
});
test('disabled career redirects but photo routes remain available', async ({ page }) => {
  fakeBackend(page, false);
  await page.goto('/app/career?e2eAuthBypass=1');
  await expect(page).toHaveURL(/\/app\/enhance/);
  await page.goto('/app/gallery?e2eAuthBypass=1');
  await expect(page).toHaveURL(/\/app\/gallery/);
});
test('restore older version appends a new version', async ({ page }) => {
  fakeBackend(page);
  await setup(page);
  await page.goto('/app/career/profile?e2eAuthBypass=1');
  await page.getByLabel('Current title').fill('Second title');
  await page.getByLabel('I confirm these facts are accurate').first().check();
  await page.getByRole('button', { name: 'Save professional facts' }).click();
  await page.getByRole('button', { name: 'View version 1' }).click();
  await page.getByRole('button', { name: 'Restore this version' }).click();
  await expect(page.getByLabel('Current title')).toHaveValue('Data analyst');
  await expect(page.getByRole('button', { name: 'View version 3' })).toBeVisible();
});
test('profile fits a 320px viewport', async ({ page }) => {
  fakeBackend(page);
  await page.setViewportSize({ width: 320, height: 700 });
  await setup(page);
  await page.goto('/app/career/profile?e2eAuthBypass=1');
  await expect(page.getByRole('heading', { name: 'Your professional profile' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
});
