import { test, expect } from '@playwright/test';

test.describe('Marketing selfie count copy', () => {
  test('how-it-works uses at least 10 copy', async ({ page }) => {
    await page.goto('/how-it-works');

    await expect(page.getByText('Upload at least 10 clear selfies')).toBeVisible();

    const recommendedHighlight = page.locator('.highlight', { hasText: 'Recommended selfies' });
    await expect(recommendedHighlight.locator('.highlight-value')).toHaveText('At least 10');
  });

  test('ai-headshot-generator uses at least 10 copy', async ({ page }) => {
    await page.goto('/ai-headshot-generator');

    await expect(
      page.getByText('Provide at least 10 clear images to train a personalized model.')
    ).toBeVisible();
    await expect(
      page.getByText('We recommend at least 10 clear selfies with varied angles and lighting.')
    ).toBeVisible();
  });
});
