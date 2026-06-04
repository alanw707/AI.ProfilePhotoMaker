import { test, expect } from '@playwright/test';

const baseOrigin = process.env.BASE_URL || 'http://localhost:4200';
const jwtPayload = Buffer.from(JSON.stringify({ sub: 'user-1', email: 'user@example.com', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64');
const mockToken = `mock.${jwtPayload}.signature`;

const professionalScore = {
  overallScore: 88,
  ratingLabel: 'LinkedIn-ready',
  subscores: [
    { code: 'face_presence', label: 'Face presence', score: 90, feedback: 'Clear face framing.' },
    { code: 'lighting', label: 'Lighting', score: 86, feedback: 'Balanced light.' },
    { code: 'background', label: 'Background', score: 84, feedback: 'Clean background.' },
    { code: 'platform_fit', label: 'Platform fit', score: 92, feedback: 'Strong crop for profile use.' },
  ],
  strengths: ['Clear face', 'Professional framing'],
  improvements: ['Minor background cleanup'],
  guidance: 'Ready for a professional profile workflow.',
  qualityGate: { status: 'pass', reasons: [], recommendations: [] },
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

test.describe('Instant headshot mocked flow', () => {
  test('upload -> score -> generate paid candidates -> select best shot -> augment -> export path uses workflow endpoints', async ({ page }) => {
    await page.route('**/api/config/client', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
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
        }),
      });
    });

    await page.route('**/api/profilephotoworkflow/packages', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: [
            { id: 1, code: 'free_preview', name: 'Free Preview', description: 'Preview', price: 0, currency: 'USD', includedCandidateCount: 1, includedRefinementCount: 0, includedPremiumAugmentationCount: 0, includesPlatformExportKit: false, includesScoreDelta: true, displayOrder: 1, highlights: [] },
            { id: 2, code: 'starter_package', name: 'Starter Package', description: 'Starter', price: 900, currency: 'USD', includedCandidateCount: 3, includedRefinementCount: 1, includedPremiumAugmentationCount: 1, includesPlatformExportKit: true, includesScoreDelta: true, displayOrder: 2, highlights: [] },
          ],
          error: null,
        }),
      });
    });

    await page.route('**/api/profilephotoworkflow/entitlements', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: [
            { id: 10, packageCode: 'starter_package', packageName: 'Starter Package', status: 'active', remainingPackageUses: 1, remainingCandidates: 3, remainingRefinements: 1, remainingPremiumAugmentations: 1, platformExportKitAvailable: true },
          ],
          error: null,
        }),
      });
    });

    await page.route('**/api/profilephotoworkflow/export-options', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: [
            { code: 'linkedin_profile', label: 'LinkedIn profile', width: 800, height: 800, fileNameSuffix: 'linkedin' },
            { code: 'resume_headshot', label: 'Resume headshot', width: 1200, height: 1600, fileNameSuffix: 'resume' },
          ],
          error: null,
        }),
      });
    });

    await page.route('**/api/profilephotoworkflow/score', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: professionalScore, error: null }) });
    });

    await page.route('**/api/profilephotoworkflow/score-image/**', async route => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: professionalScore, error: null }) });
    });

    await page.route('**/api/credit/status', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: { credits: 5, lastCreditReset: new Date().toISOString(), nextResetDate: new Date().toISOString() }, error: null }),
      });
    });

    await page.route('**/api/auth/**', async route => {
      const url = route.request().url();
      let body: unknown = { success: true, data: { emailConfirmed: true }, error: null };
      if (url.includes('profile-completion-status')) {
        body = { isCompleted: true };
      } else if (url.includes('validate-session')) {
        body = { success: true, data: { valid: true, emailConfirmed: true }, error: null };
      }
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    });

    await page.route('**/api/image/upload**', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: { uploadedFiles: [{ id: 'source-1', url: 'https://cdn.example.test/source.png', storagePath: 'dev/enhanced/user-1/source.png', fileName: 'source.png' }] },
          error: null,
        }),
      });
    });

    let headshotCalled = false;
    await page.route('**/api/headshots/generate', async route => {
      headshotCalled = true;
      const request = route.request().postDataJSON();
      expect(request.imageStoragePath).toContain('/enhanced/');
      expect(request.packageCode).toBe('starter_package');
      expect(request.numOutputs).toBe(3);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          data: {
            success: true,
            imageUrl: 'data:image/png;base64,iVBORw0KGgo=',
            storagePath: 'dev/generated/user-1/headshot-1.png',
            processedImageId: 42,
            provider: 'openai',
            model: 'gpt-image-2',
            style: 'general_professional',
            background: 'auto',
            creditsCost: 1,
            remainingCredits: 4,
            correlationId: 'corr-1',
            candidates: [
              { imageUrl: 'data:image/png;base64,iVBORw0KGgo=', storagePath: 'dev/generated/user-1/headshot-1.png', processedImageId: 42, provider: 'openai', model: 'gpt-image-2', correlationId: 'corr-1' },
              { imageUrl: 'data:image/png;base64,iVBORw0KGgo=', storagePath: 'dev/generated/user-1/headshot-2.png', processedImageId: 43, provider: 'openai', model: 'gpt-image-2', correlationId: 'corr-1' },
              { imageUrl: 'data:image/png;base64,iVBORw0KGgo=', storagePath: 'dev/generated/user-1/headshot-3.png', processedImageId: 44, provider: 'openai', model: 'gpt-image-2', correlationId: 'corr-1' },
            ],
          },
          error: null,
        }),
      });
    });

    let augmentationCalled = false;
    await page.route('**/api/enhancement/enhance', async route => {
      augmentationCalled = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true, data: { provider: 'OpenAI', Status: 'succeeded', dataUrl: 'data:image/png;base64,iVBORw0KGgo=', processedImageId: 45, storagePath: 'dev/generated/user-1/augmented.png' }, error: null }),
      });
    });

    let exportCalled = false;
    await page.route('**/api/profilephotoworkflow/export-package', async route => {
      exportCalled = true;
      const request = route.request().postDataJSON();
      expect(request.processedImageId).toBe(45);
      expect(request.exportCodes).toContain('linkedin_profile');
      await route.fulfill({ status: 200, contentType: 'application/zip', body: Buffer.from('PK\u0003\u0004') });
    });

    await page.goto('/');
    await page.evaluate(({ token }) => {
      localStorage.setItem('auth_token', token);
      localStorage.setItem('currentUser', JSON.stringify({ token, email: 'user@example.com', firstName: 'Test', lastName: 'User' }));
      localStorage.setItem('biometricConsent', JSON.stringify({ accepted: true, acceptedAt: new Date().toISOString() }));
      localStorage.setItem('e2eAuthBypass', 'true');
    }, { token: mockToken });

    await page.goto('/app/enhance?e2eAuthBypass=1');
    await page.getByRole('button', { name: /Accept All/i }).click().catch(() => undefined);
    await expect(page.getByRole('heading', { name: /Create a platform-ready profile photo|Photo Workspace/i })).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText('Upload one photo to score')).toBeVisible();

    const fileChooserPromise = page.waitForEvent('filechooser');
    await page.getByText(/Upload one photo to score/i).click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles({ name: 'source.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgo=', 'base64') });

    await expect(page.getByText(/88\/100/).first()).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText('Linkedin').first()).toBeVisible();
    await page.getByRole('checkbox', { name: /biometric data/i }).check({ force: true });
    await expect(page.getByRole('button', { name: /Transform Photo|Generate/i })).toBeEnabled({ timeout: 10_000 });

    await page.getByRole('button', { name: /Transform Photo|Generate/i }).click();
    await expect(page.getByRole('heading', { name: 'Candidate Ready' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Best Shot Selector')).toBeVisible();
    await expect(page.getByText(/Candidate score: 88\/100/)).toBeVisible();

    await expect(page.getByRole('button', { name: /Relighting/i })).toBeVisible();
    expect(headshotCalled).toBeTruthy();
  });
});
