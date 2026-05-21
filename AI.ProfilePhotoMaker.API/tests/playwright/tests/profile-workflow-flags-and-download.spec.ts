import { test, expect, Page, Route } from '@playwright/test';

const baseOrigin = process.env.BASE_URL || 'http://localhost:4200';
const jwtPayload = Buffer.from(JSON.stringify({ sub: 'user-1', email: 'user@example.com', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64');
const mockToken = `mock.${jwtPayload}.signature`;

const score = {
  overallScore: 82,
  ratingLabel: 'Profile-ready',
  subscores: [
    { code: 'face_presence', label: 'Face presence', score: 84, feedback: 'Face visible.' },
    { code: 'lighting', label: 'Lighting', score: 80, feedback: 'Lighting OK.' },
    { code: 'background', label: 'Background', score: 78, feedback: 'Usable background.' },
    { code: 'platform_fit', label: 'Platform fit', score: 86, feedback: 'Good platform crop.' },
  ],
  strengths: ['Clear enough for review'],
  improvements: ['Could refine background'],
  guidance: 'Ready to test the profile photo workflow.',
};

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

async function fulfillJson(route: Route, data: unknown) {
  await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
}

async function installCommonRoutes(page: Page, features: Record<string, boolean>) {
  await page.route('**/api/config/client', route => fulfillJson(route, {
    success: true,
    data: {
      appBaseUrl: 'http://localhost:5032',
      apiBaseUrl: 'http://localhost:5032/api',
      environment: 'development',
      isDevelopment: true,
      isProduction: false,
      features,
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
      { id: 1, code: 'free_preview', name: 'Free Preview', description: 'Preview', price: 0, currency: 'USD', includedCandidateCount: 1, includedRefinementCount: 0, includedPremiumAugmentationCount: 0, includesPlatformExportKit: false, includesScoreDelta: true, displayOrder: 1, highlights: [] },
      { id: 2, code: 'starter_package', name: 'Starter Package', description: 'Starter', price: 900, currency: 'USD', includedCandidateCount: 3, includedRefinementCount: 1, includedPremiumAugmentationCount: 1, includesPlatformExportKit: true, includesScoreDelta: true, displayOrder: 2, highlights: [] },
    ],
    error: null,
  }));

  await page.route('**/api/profilephotoworkflow/entitlements', route => fulfillJson(route, {
    success: true,
    data: [{ id: 7, packageCode: 'starter_package', packageName: 'Starter Package', status: 'active', remainingPackageUses: 1, remainingCandidates: 3, remainingRefinements: 1, remainingPremiumAugmentations: 1, platformExportKitAvailable: true }],
    error: null,
  }));

  await page.route('**/api/profilephotoworkflow/export-options', route => fulfillJson(route, {
    success: true,
    data: [{ code: 'linkedin_profile', label: 'LinkedIn profile', width: 800, height: 800, fileNameSuffix: 'linkedin' }],
    error: null,
  }));

  await page.route('**/api/profilephotoworkflow/score', route => fulfillJson(route, { success: true, data: score, error: null }));
  await page.route('**/api/profilephotoworkflow/score-image/**', route => fulfillJson(route, { success: true, data: score, error: null }));

  await page.route('**/api/image/upload**', route => fulfillJson(route, {
    success: true,
    data: { uploadedFiles: [{ id: 'source-1', url: 'https://cdn.example.test/source.png', storagePath: 'dev/enhanced/user-1/source.png', fileName: 'source.png' }] },
    error: null,
  }));
}

async function openWorkspaceWithPhoto(page: Page) {
  await page.goto('/app/enhance?e2eAuthBypass=1');
  await page.getByRole('button', { name: /Accept All/i }).click().catch(() => undefined);
  await expect(page.getByRole('heading', { name: 'Photo Workspace' })).toBeVisible({ timeout: 20_000 });

  const fileChooserPromise = page.waitForEvent('filechooser');
  await page.getByText(/Upload one photo to score|Upload a photo to transform/i).click();
  const fileChooser = await fileChooserPromise;
  await fileChooser.setFiles({ name: 'source.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgo=', 'base64') });
}

test.describe('Profile workflow flags, UX, and downloads', () => {
  test('free preview generated result is available as a browser download', async ({ page }) => {
    await installCommonRoutes(page, {
      openAIHeadshotMvp: true,
      profilePhotoWorkflowOverhaul: true,
      outcomePackagesVisible: true,
      profilePhotoScoreVisible: true,
      creativeStylePackVisible: true,
      premiumAugmentationsVisible: true,
      replicateTrainingFlowVisible: false,
    });

    let headshotCalled = false;
    await page.route('**/api/headshots/generate', route => {
      headshotCalled = true;
      const request = route.request().postDataJSON();
      expect(request.packageCode).toBe('free_preview');
      expect(request.numOutputs).toBe(1);
      return fulfillJson(route, {
        success: true,
        data: {
          success: true,
          imageUrl: 'data:image/png;base64,iVBORw0KGgo=',
          storagePath: 'dev/generated/user-1/free-preview.png',
          processedImageId: 101,
          provider: 'openai',
          model: 'gpt-image-2',
          style: 'general_professional',
          background: 'auto',
          creditsCost: 0,
          remainingCredits: 5,
          correlationId: 'free-preview',
        },
        error: null,
      });
    });

    await openWorkspaceWithPhoto(page);
    await expect(page.getByText(/Professional readiness: 82\/100/)).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText('Free Preview does not include the platform export kit')).toHaveCount(0);
    await page.getByRole('checkbox', { name: /biometric data/i }).check({ force: true });
    await page.getByRole('button', { name: /Generate Candidate|Transform Photo/i }).click();

    await expect(page.getByRole('heading', { name: 'Candidate Ready' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Free Preview does not include the platform export kit')).toBeVisible();
    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: /Download Transformed Photo/i }).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/enhanced-photo-.*\.png/);
    expect(headshotCalled).toBeTruthy();
  });

  test('feature flags hide package, score, creative, and premium UI without breaking headshot generation', async ({ page }) => {
    await installCommonRoutes(page, {
      openAIHeadshotMvp: true,
      profilePhotoWorkflowOverhaul: true,
      outcomePackagesVisible: false,
      profilePhotoScoreVisible: false,
      creativeStylePackVisible: false,
      premiumAugmentationsVisible: false,
      replicateTrainingFlowVisible: false,
    });

    await page.route('**/api/headshots/generate', route => fulfillJson(route, {
      success: true,
      data: {
        success: true,
        imageUrl: 'data:image/png;base64,iVBORw0KGgo=',
        storagePath: 'dev/generated/user-1/flagged.png',
        processedImageId: 202,
        provider: 'openai',
        model: 'gpt-image-2',
        style: 'general_professional',
        background: 'auto',
        creditsCost: 1,
        remainingCredits: 4,
        correlationId: 'flags',
      },
      error: null,
    }));

    await openWorkspaceWithPhoto(page);
    await expect(page.getByText('Package Scope')).toHaveCount(0);
    await expect(page.getByText(/Professional readiness:/)).toHaveCount(0);
    await expect(page.getByText('Cartoon Mode')).toHaveCount(0);
    await expect(page.getByText('Premium Augmentation Add-ons')).toHaveCount(0);
    await expect(page.getByText('Professional Profile Photo')).toBeVisible();

    await page.getByRole('checkbox', { name: /biometric data/i }).check({ force: true });
    await page.getByRole('button', { name: /Generate Candidate|Transform Photo/i }).click();
    await expect(page.getByRole('heading', { name: 'Candidate Ready' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('button', { name: /Download Transformed Photo/i })).toBeVisible();
  });
});
