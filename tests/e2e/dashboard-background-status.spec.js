/**
 * Dashboard Background Status (Local)
 *
 * Validates that /app/enhance surfaces persistent progress/status when a job is running,
 * including after navigating away and back ("Continue in Background" flow).
 *
 * This spec intentionally stubs /api/model-status to avoid calling Replicate.
 *
 * Opt-in:
 *   RUN_DASHBOARD_STATUS_TESTS=true
 * Optional credentials:
 *   DASHBOARD_E2E_EMAIL / DASHBOARD_E2E_PASSWORD
 *   (falls back to STRIPE_E2E_EMAIL / STRIPE_E2E_PASSWORD when present)
 */

const { test, expect } = require('@playwright/test');

const config = {
  baseUrl: process.env.TEST_BASE_URL || 'http://localhost:4200',
  email:
    process.env.DASHBOARD_E2E_EMAIL ||
    process.env.STRIPE_E2E_EMAIL ||
    'testuser@example.com',
  password:
    process.env.DASHBOARD_E2E_PASSWORD ||
    process.env.STRIPE_E2E_PASSWORD ||
    'TestPassword123!',
};

async function login(page) {
  await page.goto(`${config.baseUrl}/auth/login`);
  await page.fill('input#email', config.email);
  await page.fill('input#password', config.password);
  await page.click('button[type="submit"]');
  await page.waitForURL('**/app/enhance', { timeout: 30000 });
  await expect(page.locator('.gallery-page-container')).toBeVisible({ timeout: 15000 });
}

function interceptModelStatus(page, handler) {
  const matcher = /\/(api\/)?model-status(\?.*)?$/i;
  return page.route(matcher, async route => {
    const payload = handler();
    await route.fulfill({
      status: 200,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
  });
}

test.describe('Dashboard background status banner', () => {
  test.skip(
    process.env.RUN_DASHBOARD_STATUS_TESTS !== 'true',
    'Set RUN_DASHBOARD_STATUS_TESTS=true to enable dashboard background status checks.'
  );

  test('shows training progress after navigating away and back', async ({ page }) => {
    const createdAt = new Date(Date.now() - 2 * 60 * 1000).toISOString();
    await interceptModelStatus(page, () => ({
      statusCode: 'Training',
      hasTrainedModel: false,
      trainedModelId: null,
      trainedModelVersion: null,
      totalUploadedImages: 10,
      canStartTraining: true,
      reason: null,
      lastUpdated: new Date().toISOString(),
      currentRequest: {
        id: 'req-1',
        status: 'creating',
        trainingRequestId: 'training-123',
        createdAt,
        completedAt: null,
        errorMessage: null,
      },
      generationStatus: null,
      creditImpact: null,
    }));

    const statusUrlRegex = /\/(api\/)?model-status(\?.*)?$/i;
    const statusResponse = page.waitForResponse(res => statusUrlRegex.test(res.url()) && res.status() === 200);
    await login(page);
    await statusResponse;

    const banner = page.locator('.active-job-section .active-job-card');
    await expect(banner).toBeVisible({ timeout: 15000 });
    await expect(banner).toContainText('Training in progress');

    const continueBtn = page.locator('button.continue-background-btn');
    if (await continueBtn.isVisible()) {
      await continueBtn.click();
      await expect(banner).toBeVisible({ timeout: 15000 });
      await expect(banner).toContainText('Training in progress');
    }

    // Navigate away and back - should still show (server-driven)
    await page.goto(`${config.baseUrl}/app/gallery`);
    await page.waitForURL('**/app/gallery', { timeout: 15000 });

    const statusResponse2 = page.waitForResponse(res => statusUrlRegex.test(res.url()) && res.status() === 200);
    await page.goto(`${config.baseUrl}/app/enhance`);
    await page.waitForURL('**/app/enhance', { timeout: 15000 });
    await statusResponse2;

    await expect(banner).toBeVisible({ timeout: 15000 });
    await expect(banner).toContainText('Training in progress');
  });

  test('shows generation progress after navigating away and back', async ({ page }) => {
    const startedAt = new Date(Date.now() - 60 * 1000).toISOString();
    await interceptModelStatus(page, () => ({
      statusCode: 'Generating',
      hasTrainedModel: true,
      trainedModelId: 'user/model',
      trainedModelVersion: 'version-1',
      totalUploadedImages: 10,
      canStartTraining: true,
      reason: null,
      lastUpdated: new Date().toISOString(),
      currentRequest: null,
      generationStatus: {
        predictionId: 'pred-123',
        status: 'processing',
        style: 'corporate',
        startedAt,
        completedAt: null,
        outputCount: null,
        error: null,
      },
      creditImpact: null,
    }));

    const statusUrlRegex = /\/(api\/)?model-status(\?.*)?$/i;
    const statusResponse = page.waitForResponse(res => statusUrlRegex.test(res.url()) && res.status() === 200);
    await login(page);
    await statusResponse;

    const banner = page.locator('.active-job-section .active-job-card');
    await expect(banner).toBeVisible({ timeout: 15000 });
    await expect(banner).toContainText(/Generating photos|Generating/i);

    const continueBtn = page.locator('button.continue-background-btn');
    if (await continueBtn.isVisible()) {
      await continueBtn.click();
      await expect(banner).toBeVisible({ timeout: 15000 });
      await expect(banner).toContainText(/Generating photos|Generating/i);
    }

    await page.goto(`${config.baseUrl}/app/settings`);
    await page.waitForURL('**/app/settings', { timeout: 15000 });

    const statusResponse2 = page.waitForResponse(res => statusUrlRegex.test(res.url()) && res.status() === 200);
    await page.goto(`${config.baseUrl}/app/enhance`);
    await page.waitForURL('**/app/enhance', { timeout: 15000 });
    await statusResponse2;

    await expect(banner).toBeVisible({ timeout: 15000 });
    await expect(banner).toContainText(/Generating photos|Generating/i);
  });

  test('shows failure state with credit impact and purchase CTA', async ({ page }) => {
    await interceptModelStatus(page, () => ({
      statusCode: 'Failed',
      hasTrainedModel: false,
      trainedModelId: null,
      trainedModelVersion: null,
      totalUploadedImages: 10,
      canStartTraining: true,
      reason: 'Training failed due to an upstream error.',
      lastUpdated: new Date().toISOString(),
      currentRequest: {
        id: 'req-2',
        status: 'failed',
        trainingRequestId: 'training-999',
        createdAt: new Date(Date.now() - 10 * 60 * 1000).toISOString(),
        completedAt: new Date().toISOString(),
        errorMessage: 'Training failed due to an upstream error.',
      },
      generationStatus: null,
      creditImpact: {
        chargedCredits: 15,
        refundedCredits: 15,
        netCredits: 0,
      },
    }));

    const statusUrlRegex = /\/(api\/)?model-status(\?.*)?$/i;
    const statusResponse = page.waitForResponse(res => statusUrlRegex.test(res.url()) && res.status() === 200);
    await login(page);
    await statusResponse;

    const banner = page.locator('.active-job-section .active-job-card.error');
    await expect(banner).toBeVisible({ timeout: 15000 });
    await expect(banner).toContainText('Training failed');
    await expect(banner).toContainText('Credits charged:');
    await expect(banner).toContainText('Credits refunded:');
    await expect(banner).toContainText('Net credits spent:');
    await expect(banner.getByRole('button', { name: 'Purchase Credits' })).toBeVisible();
  });
});
