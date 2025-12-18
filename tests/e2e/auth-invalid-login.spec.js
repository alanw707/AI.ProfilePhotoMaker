/**
 * E2E: Invalid login should surface an error and stop loading.
 *
 * Regression coverage for the "Signing In..." infinite loading state when the API
 * returns an auth failure (non-existent user / wrong credentials).
 */

const { test, expect } = require('@playwright/test');

test.describe('Auth invalid login', () => {
  test.setTimeout(60000);

  test('shows error and re-enables submit', async ({ page }) => {
    const timestamp = Date.now();
    const email = `nonexistent+${timestamp}@example.com`;
    const password = 'Test1234!';

    await page.goto('/auth/login');
    await expect(page.getByRole('heading', { name: /Sign In/i })).toBeVisible();

    await page.fill('#email', email);
    await page.fill('#password', password);

    const submitButton = page.locator('button[type="submit"]');
    await expect(submitButton).toBeEnabled();

    await submitButton.click();

    // An auth failure should surface an error.
    const errorAlert = page.locator('.alert.alert-danger');
    await expect(errorAlert).toBeVisible({ timeout: 30000 });
    await expect(errorAlert).not.toHaveText(/^\s*$/);

    // Loading state should be cleared (no infinite "Signing In...").
    await expect(submitButton).toBeEnabled({ timeout: 30000 });
    await expect(submitButton.locator('.spinner')).toHaveCount(0);

    // Should remain on the login page.
    await expect(page).toHaveURL(/\/auth\/login/);
  });
});

