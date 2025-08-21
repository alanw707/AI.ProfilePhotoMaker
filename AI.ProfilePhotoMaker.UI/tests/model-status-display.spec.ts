import { test, expect } from '@playwright/test';

test.describe('Dashboard Model Status Display', () => {
  const BASE_URL = 'http://localhost:4200';

  test('shows "Ready for training" when API training-status says so, despite old creating requests', async ({ page }) => {
    // Intercept model requests to include an old creating entry
    await page.route('**/api/model-creation/user/current', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          message: 'Found 2 model creation requests for current user',
          data: {
            totalRequests: 2,
            hasTrainedModel: false,
            latestTrainedModel: null,
            allRequests: [
              {
                requestId: 'r2',
                status: 'creating',
                createdAt: new Date(Date.now() - 86400000).toISOString(), // older
                modelName: 'user-x-old',
                replicateModelId: 'mock/old',
                trainedModelVersion: null,
                completedAt: null,
                errorMessage: null,
              },
              {
                requestId: 'r3',
                status: 'failed',
                createdAt: new Date().toISOString(), // latest is failed
                modelName: 'user-x-latest',
                replicateModelId: 'mock/latest',
                trainedModelVersion: null,
                completedAt: new Date().toISOString(),
                errorMessage: 'Model was deleted from Replicate externally',
              },
            ],
          },
        }),
      });
    });

    // Intercept training-status to explicitly say Ready for training
    await page.route('**/api/profile/training-status', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ProfileId: 1,
          HasTrainedModel: false,
          TrainedModelId: null,
          ModelTrainedAt: null,
          TotalUploadedImages: 12,
          LatestZipFile: 'dev/training-zips/test-user.zip',
          CanStartTraining: true,
          Status: 'Ready for training',
        }),
      });
    });

    await page.goto(`${BASE_URL}/app/dashboard`);

    // Expect the dashboard to show the status text from training-status
    await expect(page.getByText('Ready for training')).toBeVisible({ timeout: 10000 });
  });
});

