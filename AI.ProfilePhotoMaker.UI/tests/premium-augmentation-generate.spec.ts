import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

test.use({
  viewport:
    process.env.PREMIUM_VIEWPORT === 'mobile'
      ? { width: 390, height: 844 }
      : { width: 1304, height: 716 },
});

const browserRoot = path.resolve(__dirname, '../dist/ai.profile-photo-maker.ui/browser');
const sourceImage = path.resolve(__dirname, '../cypress/fixtures/test-image.jpg');

async function installBuiltBundle(page: import('@playwright/test').Page): Promise<void> {
  await page.route('**/app/enhance*', route =>
    route.fulfill({ path: path.join(browserRoot, 'index.html') })
  );

  for (const extension of ['js', 'css']) {
    const pattern = '**/*.' + extension;
    await page.route(pattern, async route => {
      const filePath = path.join(browserRoot, path.basename(new URL(route.request().url()).pathname));
      if (fs.existsSync(filePath)) {
        await route.fulfill({ path: filePath });
      } else {
        await route.continue();
      }
    });
  }
}
const resultImage =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';

const score = {
  overallScore: 86,
  ratingLabel: 'Ready to improve',
  subscores: [],
  strengths: ['Clear face'],
  improvements: [],
  guidance: 'Good starting point.',
  qualityGate: { status: 'pass', reasons: [], recommendations: [] },
};

async function installWorkflowMocks(page: import('@playwright/test').Page): Promise<void> {
  const packages = [
    {
      id: 1,
      code: 'free_preview',
      name: 'Free Preview',
      description: 'See the direction first.',
      price: 0,
      currency: 'USD',
      includedCandidateCount: 1,
      includedRefinementCount: 0,
      includedPremiumAugmentationCount: 0,
      includesPlatformExportKit: false,
      includesScoreDelta: false,
      displayOrder: 1,
      highlights: [],
    },
    {
      id: 2,
      code: 'pro_package',
      name: 'Pro Package',
      description: 'More candidates and finishing tools.',
      price: 39,
      currency: 'USD',
      internalCreditPackageId: 2,
      includedCandidateCount: 9,
      includedRefinementCount: 3,
      includedPremiumAugmentationCount: 3,
      includesPlatformExportKit: true,
      includesScoreDelta: true,
      displayOrder: 3,
      highlights: [],
    },
  ];
  const entitlement = {
    id: 10,
    packageCode: 'pro_package',
    packageName: 'Pro Package',
    status: 'Active',
    remainingPackageUses: 1,
    remainingCandidates: 9,
    remainingRefinements: 3,
    remainingPremiumAugmentations: 3,
    platformExportKitAvailable: true,
  };
  const style = {
    id: 1,
    name: 'linkedin',
    description: 'Clean and credible professional framing.',
    promptTemplate: 'professional portrait',
    negativePromptTemplate: '',
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  };
  const candidate = {
    imageUrl: resultImage,
    storagePath: '',
    processedImageId: 101,
    provider: 'openai',
    model: 'gpt-image-2',
    correlationId: 'test-correlation',
    useCaseCode: 'linkedin_executive',
  };

  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    let body: unknown = { success: true, data: {}, error: null };

    if (path.endsWith('/config/client')) {
      body = {
        success: true,
        data: {
          features: {
            openAIHeadshotMvp: true,
            profilePhotoWorkflowOverhaul: true,
            outcomePackagesVisible: true,
            profilePhotoScoreVisible: true,
            premiumAugmentationsVisible: true,
          },
        },
      };
    } else if (path.endsWith('/auth/account-status')) {
      body = { success: true, data: { emailConfirmed: true }, error: null };
    } else if (path.endsWith('/auth/validate-session')) {
      body = { success: true, isSuccess: true, isAuthenticated: true, data: {} };
    } else if (path.endsWith('/auth/profile-completion-status')) {
      body = { isCompleted: true };
    } else if (path.endsWith('/auth/user-roles')) {
      body = { success: true, data: [] };
    } else if (path.endsWith('/profile')) {
      body = { success: true, data: { firstName: 'Test', lastName: 'User' } };
    } else if (path.endsWith('/credit/status')) {
      body = {
        success: true,
        data: { credits: 50, lastCreditReset: '2026-01-01T00:00:00Z', nextResetDate: '2026-02-01T00:00:00Z' },
      };
    } else if (path.endsWith('/style-preview/list')) {
      body = { success: true, count: 0, previews: [] };
    } else if (path.endsWith('/style')) {
      body = { success: true, data: [style], error: null };
    } else if (path.endsWith('/profilephotoworkflow/packages')) {
      body = { success: true, data: packages, error: null };
    } else if (path.endsWith('/profilephotoworkflow/entitlements')) {
      body = { success: true, data: [entitlement], error: null };
    } else if (path.endsWith('/profilephotoworkflow/export-options')) {
      body = { success: true, data: [], error: null };
    } else if (path.endsWith('/headshots/resumable-preview')) {
      body = { success: true, data: null, error: null };
    } else if (path.endsWith('/profilephotoworkflow/score')) {
      body = { success: true, data: score, error: null };
    } else if (path.includes('/profilephotoworkflow/score-image/')) {
      body = { success: true, data: score, error: null };
    } else if (path.endsWith('/image/upload')) {
      body = {
        success: true,
        data: {
          uploadedFiles: [{ Url: resultImage, FileName: 'source.jpg', StoragePath: 'source/source.jpg' }],
        },
      };
    } else if (path.endsWith('/headshots/generate')) {
      body = {
        success: true,
        data: {
          ...candidate,
          imageUrl: resultImage,
          candidates: [candidate, { ...candidate, processedImageId: 102 }, { ...candidate, processedImageId: 103 }],
          style: 'linkedin',
          background: 'auto',
          creditsCost: 0,
          remainingCredits: 50,
        },
        error: null,
      };
    } else if (path.endsWith('/enhancement/enhance')) {
      body = {
        success: true,
        data: {
          dataUrl: resultImage,
          processedImageId: 201,
          storagePath: '',
        },
        error: null,
      };
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(body),
    });
  });
}

test('Pro package exposes an explicit premium generation action', async ({ page }) => {
  await page.addInitScript(() => {
    (window as any).turnstile = {
      render: (_container: HTMLElement, options: { callback: (token: string) => void }) => {
        setTimeout(() => options.callback('e2e-turnstile-token'));
        return 'e2e-widget';
      },
      remove: () => undefined,
      reset: () => undefined,
    };
  });
  await installBuiltBundle(page);
  await installWorkflowMocks(page);
  await page.goto('http://127.0.0.1:4300/app/enhance?e2eAuthBypass=1');
  const rejectCookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await rejectCookies.isVisible().catch(() => false)) {
    await rejectCookies.click();
  }

  await page.locator('input[type="file"]').setInputFiles(sourceImage);
  await expect(page.getByRole('heading', { name: 'Review your source photo' })).toBeVisible();
  await page.locator('.consent-block input[type="checkbox"]').first().check();
  await page.getByRole('button', { name: /Generate paid candidates/ }).click();

  await expect(page.getByRole('heading', { name: 'Premium augmentations' })).toBeVisible();
  await expect(page.getByText('3 Pro premium add-ons available.')).toBeVisible();
  const closeNotification = page.getByRole('button', { name: 'Close notification' });
  if (await closeNotification.isVisible().catch(() => false)) {
    await closeNotification.click();
  }
  const premiumPanel = page.locator('.tool-panel').filter({ hasText: 'Premium augmentations' });
  await premiumPanel.scrollIntoViewIfNeeded();
  await premiumPanel.screenshot({ path: 'test-results/ux-local/premium-augmentation-cta.png' });
  const premiumRequest = page.waitForRequest(request =>
    new URL(request.url()).pathname.endsWith('/api/enhancement/enhance')
  );
  await page.getByRole('button', { name: /Generate .*premium add-on/i }).click();
  await premiumRequest;
  await expect(page.getByText('Relighting applied')).toBeVisible();
});
