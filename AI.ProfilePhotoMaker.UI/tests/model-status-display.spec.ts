import { test, expect } from '@playwright/test';

const BASE_URL = 'http://localhost:4200';

const jsonResponse = (data: unknown) => ({
  status: 200,
  contentType: 'application/json',
  body: JSON.stringify(data),
});

const stubUser = {
  token: '',
  email: 'playwright@example.com',
  firstName: 'Play',
  lastName: 'Wright',
};

test.describe('Dashboard Model Status Display', () => {
  test('renders "Ready for training" when unified status returns ReadyForTraining', async ({ page }) => {
    // Seed a mock authenticated session before the app initializes
    await page.addInitScript(user => {
      localStorage.setItem('currentUser', JSON.stringify(user));
    }, stubUser);

    await page.route('**/api/**', async route => {
      const url = route.request().url();
      const nowIso = new Date().toISOString();

      if (url.includes('/api/auth/validate-session')) {
        await route.fulfill({ status: 200, contentType: 'text/plain', body: 'OK' });
        return;
      }

      if (url.includes('/api/auth/account-status')) {
        await route.fulfill(
          jsonResponse({
            data: {
              emailConfirmed: true,
            },
          })
        );
        return;
      }

      if (url.includes('/api/auth/profile-completion-status')) {
        await route.fulfill(
          jsonResponse({
            isCompleted: true,
            hasFirstName: true,
            hasLastName: true,
            hasGender: true,
            hasEthnicity: true,
          })
        );
        return;
      }

      if (url.includes('/api/model-status')) {
        await route.fulfill(
          jsonResponse({
            statusCode: 'ReadyForTraining',
            hasTrainedModel: false,
            trainedModelId: null,
            trainedModelVersion: null,
            totalUploadedImages: 12,
            canStartTraining: true,
            reason: 'Sufficient uploads available',
            lastUpdated: nowIso,
            currentRequest: null,
          })
        );
        return;
      }

      if (url.includes('/api/profile/training-status')) {
        await route.fulfill(
          jsonResponse({
            ProfileId: 1,
            HasTrainedModel: false,
            TrainedModelId: null,
            ModelTrainedAt: null,
            TotalUploadedImages: 12,
            LatestZipFile: 'dev/training-zips/sample.zip',
            CanStartTraining: true,
            Status: 'ReadyForTraining',
          })
        );
        return;
      }

      if (url.includes('/api/model-creation/user/current')) {
        await route.fulfill(
          jsonResponse({
            success: true,
            message: 'Mocked model creation data',
            data: {
              totalRequests: 0,
              hasTrainedModel: false,
              latestTrainedModel: null,
              allRequests: [],
            },
          })
        );
        return;
      }

      if (url.includes('/api/profile/check-model-status')) {
        await route.fulfill(
          jsonResponse({
            success: true,
            data: {
              modelExists: false,
              modelStatus: 'ReadyForTraining',
              message: 'Mocked check result',
            },
            error: null,
          })
        );
        return;
      }

      if (/\/api\/profile(?:\?|$)/.test(url)) {
        await route.fulfill(
          jsonResponse({
            id: 1,
            userId: 'mock-user-id',
            firstName: 'Play',
            lastName: 'Wright',
            gender: null,
            ethnicity: null,
            trainedModelId: null,
            trainedModelVersionId: null,
            modelTrainedAt: null,
            subscriptionTier: 'free',
            basicCredits: 0,
            lastCreditReset: nowIso,
            createdAt: nowIso,
            updatedAt: nowIso,
          })
        );
        return;
      }

      if (url.includes('/api/image/images') || url.includes('/api/image/user-images')) {
        await route.fulfill(
          jsonResponse({
            success: true,
            data: {
              totalImages: 12,
              generatedImages: 0,
              images: [],
            },
          })
        );
        return;
      }

      if (url.includes('/api/image/training-zips') || url.includes('/api/image/latest-training-zip')) {
        await route.fulfill(jsonResponse({ success: true, data: [] }));
        return;
      }

      if (url.includes('/api/style/user-selected') || url.includes('/api/style/select')) {
        if (route.request().method() === 'POST') {
          await route.fulfill(jsonResponse({ success: true, message: 'Selection saved', error: null }));
        } else {
          await route.fulfill(jsonResponse({ success: true, data: [], error: null }));
        }
        return;
      }

      if (url.includes('/api/style')) {
        await route.fulfill(jsonResponse({ success: true, data: [], error: null }));
        return;
      }

      if (url.includes('/api/credit/status')) {
        await route.fulfill(
          jsonResponse({
            success: true,
            data: {
              basicCredits: 10,
              bonusCredits: 0,
              purchasedCredits: 0,
              remainingCredits: 10,
            },
            error: null,
          })
        );
        return;
      }

      if (url.includes('/api/notification')) {
        await route.fulfill(jsonResponse({ success: true, data: [] }));
        return;
      }

      if (url.includes('/api/replicate') || url.includes('/api/model-discovery')) {
        await route.fulfill(jsonResponse({ success: true, data: {} }));
        return;
      }

      // Default stub for any other API call accessed during dashboard bootstrap
      await route.fulfill(jsonResponse({ success: true }));
    });

    await page.goto(`${BASE_URL}/app/dashboard`);

    await expect(page.getByText('Ready for Training')).toBeVisible({ timeout: 15000 });
  });
});
