import { test, expect, Page, Route } from '@playwright/test';

const baseOrigin = process.env.BASE_URL || 'http://localhost:4200';
const jwtPayload = Buffer.from(JSON.stringify({ sub: 'user-1', email: 'user@example.com', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64');
const mockToken = `mock.${jwtPayload}.signature`;

async function fulfillJson(route: Route, data: unknown) {
  await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
}

async function installUiReviewRoutes(page: Page) {
  await page.route('**/api/config/client', route => fulfillJson(route, {
    success: true,
    data: {
      appBaseUrl: 'http://localhost:5032',
      apiBaseUrl: 'http://localhost:5032/api',
      environment: 'development',
      isDevelopment: true,
      isProduction: false,
      features: {
        openAIHeadshotMvp: true,
        profilePhotoWorkflowOverhaul: true,
        outcomePackagesVisible: true,
        profilePhotoScoreVisible: true,
        creativeStylePackVisible: true,
        premiumAugmentationsVisible: true,
        replicateTrainingFlowVisible: false,
      },
    },
  }));

  await page.route('**/api/auth/**', route => {
    const url = route.request().url();
    const body = url.includes('profile-completion-status')
      ? { isCompleted: true }
      : { success: true, data: { valid: true, emailConfirmed: true }, error: null };
    return fulfillJson(route, body);
  });

  await page.route('**/api/credit/status', route => fulfillJson(route, {
    success: true,
    data: { credits: 5, lastCreditReset: new Date().toISOString(), nextResetDate: new Date().toISOString() },
    error: null,
  }));

  await page.route('**/api/profilephotoworkflow/packages', route => fulfillJson(route, {
    success: true,
    data: [
      { id: 1, code: 'free_preview', name: 'Free Preview', description: 'Preview one candidate', price: 0, currency: 'USD', includedCandidateCount: 1, includedRefinementCount: 0, includedPremiumAugmentationCount: 0, includesPlatformExportKit: false, includesScoreDelta: true, displayOrder: 1, highlights: ['Score before you pay'] },
      { id: 2, code: 'starter_package', name: 'Starter Package', description: 'Profile-ready set', price: 900, currency: 'USD', includedCandidateCount: 3, includedRefinementCount: 1, includedPremiumAugmentationCount: 1, includesPlatformExportKit: true, includesScoreDelta: true, displayOrder: 2, highlights: ['3 candidates', 'Platform exports'] },
      { id: 3, code: 'pro_package', name: 'Pro Package', description: 'Full professional set', price: 1900, currency: 'USD', includedCandidateCount: 9, includedRefinementCount: 3, includedPremiumAugmentationCount: 2, includesPlatformExportKit: true, includesScoreDelta: true, displayOrder: 3, highlights: ['9 candidates', 'More refinements'] },
    ],
    error: null,
  }));

  await page.route('**/api/profilephotoworkflow/entitlements', route => fulfillJson(route, { success: true, data: [], error: null }));
  await page.route('**/api/profilephotoworkflow/export-options', route => fulfillJson(route, { success: true, data: [], error: null }));
  await page.route('**/api/profile/data-stats', route => fulfillJson(route, {
    success: true,
    data: { inputPhotos: 0, generatedPhotos: 0, hasTrainedModel: false, totalDataSize: 0, accountAge: 1 },
    error: null,
  }));
  await page.route('**/api/image/**', route => fulfillJson(route, {
    success: true,
    data: { images: [], totalImages: 0, uploadedImages: 0, generatedImages: 0, totalProcessedImages: 0 },
    error: null,
  }));
}

test.use({
  storageState: {
    cookies: [],
    origins: [
      ...['http://127.0.0.1:4300', 'http://localhost:4300', baseOrigin].map(origin => ({
        origin,
        localStorage: [
          { name: 'auth_token', value: mockToken },
          { name: 'currentUser', value: JSON.stringify({ token: mockToken, email: 'user@example.com', firstName: 'Test', lastName: 'User' }) },
          { name: 'biometricConsent', value: JSON.stringify({ accepted: true, acceptedAt: new Date().toISOString() }) },
          { name: 'e2eAuthBypass', value: 'true' },
        ],
      })),
    ],
  },
});

test.describe('UI/UX frontend review smoke checks', () => {
  test('landing and packages communicate outcome-focused workflow without legacy training prominence', async ({ page }) => {
    await installUiReviewRoutes(page);
    const consoleErrors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });

    await page.goto('/');
    await expect(page.getByRole('button', { name: /Pricing/i })).toBeVisible({ timeout: 20_000 });
    await expect(page.getByRole('button', { name: /Go to Photo Workspace/i })).toBeVisible();
    await expect(page.getByText(/professional|profile|headshot/i).first()).toBeVisible();
    await expect(page.locator('main').getByText(/model training|training your ai model/i)).toHaveCount(0);

    await page.goto('/pricing');
    await expect(page.getByText(/Free Preview|Starter Package|Pro Package/i).first()).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/candidate|export|package/i).first()).toBeVisible();
    await expect(page.getByText(/raw credits|buy credits/i)).toHaveCount(0);

    await page.goto('/app/enhance?e2eAuthBypass=1');
    await expect(page.locator('main').getByText(/create an instant headshot|your free headshots|photo workspace/i).first()).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('main').getByText(/model training|training your ai model/i)).toHaveCount(0);

    await page.goto('/app/settings?e2eAuthBypass=1');
    await expect(page.locator('main').getByText(/account|settings|data|privacy/i).first()).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('main').getByText(/model training|training your ai model/i)).toHaveCount(0);
    expect(consoleErrors.filter(error => !/favicon|ResizeObserver|Load Images failed|401 \(Unauthorized\)/i.test(error))).toEqual([]);
  });

  test('workspace remains usable and scannable on mobile viewport', async ({ page }) => {
    await installUiReviewRoutes(page);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/app/enhance?e2eAuthBypass=1');

    await expect(page.getByRole('heading', { name: 'Photo Workspace' })).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText('Upload one photo to score')).toBeVisible();

    const fileChooserPromise = page.waitForEvent('filechooser');
    await page.getByText(/Upload one photo to score/i).click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({ name: 'source.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgo=', 'base64') });

    await expect(page.getByRole('heading', { name: 'Professional Role' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Package Scope' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Workflow Options' })).toBeVisible();
    await expect(page.getByText('Professional Profile Photo')).toBeVisible();
    await expect(page.getByText('Cartoon Mode')).toBeVisible();
    const generateButton = page.getByRole('button', { name: /Generate Candidate|Transform Photo|Package Entitlement Needed/i });
    await generateButton.scrollIntoViewIfNeeded();
    await expect(generateButton).toBeVisible();

    const horizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2);
    expect(horizontalOverflow).toBeFalsy();
  });
});
