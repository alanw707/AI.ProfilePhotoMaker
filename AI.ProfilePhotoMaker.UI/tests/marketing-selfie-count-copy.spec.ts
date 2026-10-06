import { test, expect } from '@playwright/test';

// The photo product needs one clear photo (since 15120119). These checks keep the marketing
// pages consistent with that and catch a regression to the old "at least 5 selfies" copy.
test.describe('Marketing photo count copy', () => {
  test('how-it-works says one clear photo', async ({ page }) => {
    await page.goto('/how-it-works');

    await expect(page.getByText('Upload one clear photo', { exact: true })).toBeVisible();

    const minimumHighlight = page.locator('.highlight', { hasText: 'Minimum input' });
    await expect(minimumHighlight.locator('.highlight-value')).toHaveText('One photo');
    await expect(page.locator('body')).not.toContainText(/at least 5/i);
  });

  test('ai-headshot-generator says one clear photo', async ({ page }) => {
    await page.goto('/ai-headshot-generator');

    await expect(page.getByText('Upload one clear photo', { exact: true })).toBeVisible();
    const faqItem = page.locator('details', { hasText: 'How many photos should I upload?' });
    await faqItem.locator('summary').click();
    await expect(
      faqItem.getByText('We recommend one clear photo with varied angles and lighting.')
    ).toBeVisible();
    await expect(page.locator('body')).not.toContainText(/at least 5/i);
  });
});
