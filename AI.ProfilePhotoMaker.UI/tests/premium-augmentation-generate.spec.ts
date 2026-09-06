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

async function installWorkflowMocks(page: import('@playwright/test').Page) {
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
    storagePath: 'dev/generated/test-user/candidate.png',
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
      const refinement = request.postDataJSON().refinementCode;
      if (refinement) entitlement.remainingRefinements--;
      entitlement.remainingCandidates = 0;
      entitlement.remainingPackageUses = 0;
      body = {
        success: true,
        data: {
          ...candidate,
          imageUrl: resultImage,
          candidates: refinement ? [{ ...candidate, processedImageId: 501 }] : Array.from({ length: 9 }, (_, index) => ({ ...candidate, processedImageId: 101 + index })),
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
  return entitlement;
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
  await page.goto('/app/enhance?e2eAuthBypass=1');
  const rejectCookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await rejectCookies.isVisible().catch(() => false)) {
    await rejectCookies.click();
  }

  await page.locator('input[type="file"]').setInputFiles(sourceImage);
  await expect(page.getByRole('heading', { name: 'Choose your portrait direction' })).toBeVisible();
  await page.getByRole('checkbox', { name: /I consent/ }).first().check();
  await page.getByRole('button', { name: 'Generate 9 photos', exact: true }).click();

  await expect(page.getByRole('heading', { name: 'Refine the selected proof' })).toBeVisible();
  const closeNotification = page.getByRole('button', { name: 'Close notification' });
  if (await closeNotification.isVisible().catch(() => false)) {
    await closeNotification.click();
  }
  await page.locator('.premium-edits summary').click();
  await page.getByRole('button', { name: /^Relighting/ }).click();
  const [premiumRequest] = await Promise.all([
    page.waitForRequest(request => new URL(request.url()).pathname.endsWith('/api/enhancement/enhance')),
    page.getByRole('button', { name: 'Apply relighting', exact: true }).click(),
  ]);
  expect(premiumRequest.postDataJSON().enhancementType).toBe('relighting');
  expect(premiumRequest.postDataJSON().customPrompt).toBeTruthy();
});

// Use the dev server for this state-transition check so Angular's debug API can
// simulate an entitlement refresh without spending a real premium allowance.
for (const width of [390, 1440]) {
  test(`exhausted premium picker disappears while guided refinements remain usable (${width})`, async ({ page }) => {
    await page.setViewportSize({ width, height: 950 });
    const entitlement = await installWorkflowMocks(page);
    await page.goto('/app/enhance?e2eAuthBypass=1');
    const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
    if (await cookies.isVisible()) await cookies.click();
    await page.locator('input[type="file"]').setInputFiles(sourceImage);
    await expect(page.getByRole('heading', { name: 'Choose your portrait direction' })).toBeVisible();
    await page.getByRole('checkbox', { name: /I consent/ }).first().check();
    await page.getByRole('button', { name: 'Generate 9 photos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Refine the selected proof' })).toBeVisible();
    const summary = page.locator('.premium-edits summary');
    if (await summary.isVisible()) await summary.click();
    await page.getByRole('button', { name: /^Relighting/ }).click();
    await expect(page.getByRole('button', { name: 'Apply relighting', exact: true })).toBeVisible();
    const premiumRequests: string[] = [];
    page.on('request', request => {
      if (new URL(request.url()).pathname.endsWith('/api/enhancement/enhance')) premiumRequests.push(request.url());
    });
    entitlement.remainingPremiumAugmentations = 0;
    entitlement.remainingRefinements = 4;
    await page.evaluate(() => {
      const ng = (window as any).ng;
      const component = ng.getComponent(document.querySelector('app-photo-enhancement'));
      component.packageEntitlements = component.packageEntitlements.map((item: any) => ({
        ...item, remainingPremiumAugmentations: 0, remainingRefinements: 4,
      }));
      ng.applyChanges(component);
    });
    await expect(page.getByRole('heading', { name: 'Choose the relighting direction' })).not.toBeVisible();
    await expect(page.getByRole('button', { name: 'Apply relighting', exact: true })).not.toBeVisible();
    await expect(page.getByText('No premium edits remain. Your refinement allowance is unchanged.')).toBeVisible();
    await expect(page.getByText('4 refinements remaining', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Choose a change', exact: true })).toBeDisabled();
    await page.getByRole('radio', { name: /^Subtle smile/ }).check();
    const apply = page.getByRole('button', { name: 'Apply subtle smile', exact: true });
    await expect(apply).toBeEnabled();
    await page.locator('.guided-refinement').screenshot({ path: `/tmp/aipm-guided-refinement-${width}.png` });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    const [request] = await Promise.all([
      page.waitForRequest(request => new URL(request.url()).pathname.endsWith('/api/headshots/generate')),
      apply.click(),
    ]);
    expect(request.postDataJSON()).toMatchObject({
      refinementCode: 'subtle_smile', isRegeneration: true, numOutputs: 1,
      imageStoragePath: 'dev/generated/test-user/candidate.png', replacesProcessedImageId: 101,
    });
    await expect(page.getByText('Subtle smile applied', { exact: true })).toBeVisible();
    await expect(page.getByText('3 refinements remaining', { exact: true })).toBeVisible();
    await expect(page.locator('.premium-edits summary')).toHaveText('Premium edits · 0 remaining');
    expect(premiumRequests).toHaveLength(0);
  });
}
