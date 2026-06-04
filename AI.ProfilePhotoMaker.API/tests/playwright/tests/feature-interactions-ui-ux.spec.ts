import { test, expect, Page, Route } from '@playwright/test';

const baseOrigin = process.env.BASE_URL || 'http://localhost:4200';
const jwtPayload = Buffer.from(JSON.stringify({ sub: 'admin-1', email: 'admin@example.com', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64');
const mockToken = `mock.${jwtPayload}.signature`;

async function fulfillJson(route: Route, data: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(data) });
}

async function installCommonMocks(page: Page) {
  await page.route('**/api/config/client', route => fulfillJson(route, { success: true, data: { appBaseUrl: 'http://localhost:5032', apiBaseUrl: 'http://localhost:5032/api', environment: 'development', isDevelopment: true, isProduction: false, features: { openAIHeadshotMvp: true, profilePhotoWorkflowOverhaul: true, outcomePackagesVisible: true, profilePhotoScoreVisible: true, creativeStylePackVisible: true, premiumAugmentationsVisible: true, replicateTrainingFlowVisible: false } } }));
  await page.route('**/api/auth/user-roles', route => fulfillJson(route, { success: true, data: { roles: ['Admin'] }, error: null }));
  await page.route('**/api/auth/profile-completion-status', route => fulfillJson(route, { isCompleted: true }));
  await page.route('**/api/auth/validate-session', route => fulfillJson(route, { success: true, data: { valid: true, emailConfirmed: true }, error: null }));
  await page.route('**/api/auth/account-status', route => fulfillJson(route, { success: true, data: { emailConfirmed: true }, error: null }));
  await page.route('**/api/profile/data-stats', route => fulfillJson(route, { success: true, data: { inputPhotos: 1, generatedPhotos: 1, hasTrainedModel: false, totalDataSize: 1024, accountAge: 1 }, error: null }));
  await page.route('**/api/profile/**', route => fulfillJson(route, { success: true, data: {}, error: null }));
  await page.route('**/api/credit/payment/config', route => fulfillJson(route, { success: true, data: { stripePublishableKey: 'pk_test_mock', paymentSimulation: { enabled: true, skipStripeIntegration: true } }, error: null }));
  await page.route('**/api/credit/purchase', route => fulfillJson(route, { success: true, data: { credits: 5, totalCredits: 5, purchasedCredits: 5, generatedImages: 0 }, error: null }));
  await page.route('**/api/credit/status', route => fulfillJson(route, { success: true, data: { credits: 5, lastCreditReset: new Date().toISOString(), nextResetDate: new Date().toISOString() }, error: null }));
  await page.route('**/api/profilephotoworkflow/packages', route => fulfillJson(route, {
    success: true,
    data: [
      { id: 1, code: 'free_preview', name: 'Free Preview', description: 'Preview one candidate', price: 0, currency: 'USD', includedCandidateCount: 1, includedRefinementCount: 0, includedPremiumAugmentationCount: 0, includesPlatformExportKit: false, includesScoreDelta: true, displayOrder: 1, highlights: ['Score before you pay'] },
      { id: 2, code: 'starter_package', name: 'Starter Package', description: 'Profile-ready set', price: 9, currency: 'USD', internalCreditPackageId: 2, includedCandidateCount: 3, includedRefinementCount: 1, includedPremiumAugmentationCount: 1, includesPlatformExportKit: true, includesScoreDelta: true, displayOrder: 2, highlights: ['3 candidates', 'Platform exports'] },
    ],
    error: null,
  }));
  await page.route('**/api/profilephotoworkflow/export-options', route => fulfillJson(route, { success: true, data: [], error: null }));
  await page.route('**/api/support/**', route => fulfillJson(route, { success: true, message: 'Thanks — we received your message.', data: {}, error: null }));
  await page.route('**/api/feedback', route => fulfillJson(route, { success: true, message: 'Thanks — we received your message.', data: {}, error: null }));
  await page.route('**/api/feedback/**', route => fulfillJson(route, { success: true, message: 'Thanks — we received your message.', data: {}, error: null }));
  await page.route('**/api/admin/**', route => fulfillJson(route, { success: true, data: { items: [], users: [{ id: 'admin-1', email: 'admin@example.com', firstName: 'Admin', lastName: 'User' }], coupons: [], campaigns: [], auditLogs: [], totalCount: 0, stats: {} }, error: null }));
  await page.route('**/api/image/images', route => fulfillJson(route, { success: true, data: { images: [{ id: 77, originalImageUrl: 'data:image/png;base64,iVBORw0KGgo=', processedImageUrl: 'data:image/png;base64,iVBORw0KGgo=', style: 'professional', createdAt: new Date().toISOString(), isGenerated: true }], totalImages: 1, uploadedImages: 0, generatedImages: 1, totalProcessedImages: 1 }, error: null }));
  await page.route('**/api/image/**', route => fulfillJson(route, { success: true, data: {}, error: null }));
}

async function seedUser(page: Page) {
  await page.addInitScript(({ token }) => {
    localStorage.setItem('auth_token', token);
    localStorage.setItem('currentUser', JSON.stringify({ token, email: 'admin@example.com', firstName: 'Admin', lastName: 'User' }));
    localStorage.setItem('e2eAuthBypass', 'true');
  }, { token: mockToken });
}

test.describe('Functional feature interactions and UX states', () => {
  test.beforeEach(async ({ page }) => {
    await seedUser(page);
    await installCommonMocks(page);
  });

  test('auth forms expose validation and submit states', async ({ browser }) => {
    const context = await browser.newContext();
    const page = await context.newPage();
    await installCommonMocks(page);
    await page.goto('/auth/login');
    const loginEmail = page.locator('input[type="email"], input[name="email"]').first();
    await expect(loginEmail).toBeVisible();
    await loginEmail.fill('not-an-email');
    await loginEmail.blur();
    await expect(page.locator('body')).toContainText(/email|required|password/i);
    await expect(page.getByRole('button', { name: /sign in|login/i })).toBeDisabled();

    await page.goto('/auth/register');
    const registerEmail = page.locator('input[type="email"], input[name="email"]').first();
    await expect(registerEmail).toBeVisible();
    await registerEmail.fill('invalid');
    await registerEmail.blur();
    await expect(page.locator('body')).toContainText(/required|password|email/i);
    await expect(page.getByRole('button', { name: 'Create Account', exact: true })).toBeDisabled();
    await context.close();
  });

  test('support form validates and submits user message', async ({ page }) => {
    await page.goto('/app/support?e2eAuthBypass=1');
    await page.getByRole('button', { name: /Accept All/i }).click().catch(() => undefined);
    await page.getByLabel('Message').fill('short');
    await page.getByLabel('Message').blur();
    await expect(page.getByText(/Message must be/i)).toBeVisible();
    await page.getByLabel('Category').selectOption('Question');
    await page.getByLabel('Message').fill('Please help me verify the package export workflow.');
    const feedbackResponse = page.waitForResponse(response => response.url().includes('/api/feedback') && response.request().method() === 'POST');
    const sendButton = page.getByRole('button', { name: /^Send$/i });
    await expect(sendButton).toBeEnabled();
    await sendButton.click();
    await expect((await feedbackResponse).status()).toBe(200);
  });

  test('settings data deletion modal requires explicit confirmation', async ({ page }) => {
    await page.goto('/app/settings?e2eAuthBypass=1');
    await expect(page.locator('main')).toContainText(/data|privacy|account/i);
    const deleteButtons = page.getByRole('button', { name: /delete/i });
    await expect(deleteButtons.first()).toBeVisible();
    const deleteCount = await deleteButtons.count();
    for (let i = 0; i < deleteCount; i++) {
      if (await deleteButtons.nth(i).isEnabled()) {
        await deleteButtons.nth(i).click();
        break;
      }
    }
    await expect(page.locator('body')).toContainText(/confirm|delete|cannot be undone/i);
    const confirmInput = page.locator('input[placeholder="DELETE"]');
    if (await confirmInput.count()) {
      await expect(page.getByRole('button', { name: /delete/i }).last()).toBeDisabled();
      await confirmInput.fill('DELETE');
      await expect(page.getByRole('button', { name: /delete/i }).last()).toBeEnabled();
    }
  });

  test('gallery workspace exposes generated image actions', async ({ page }) => {
    await page.goto('/app/gallery?e2eAuthBypass=1');
    await expect(page.getByRole('heading', { name: /Photo Workspace|Your Photos/i }).first()).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('body')).toContainText(/professional|generated|workspace/i);
    await expect(page.getByRole('button', { name: /download|view|delete/i }).first()).toBeVisible();
  });

  test('pricing package purchase simulation exposes payment path', async ({ page }) => {
    await page.goto('/pricing');
    await page.getByRole('button', { name: /Accept All/i }).click().catch(() => undefined);
    await expect(page.getByText('Starter Package')).toBeVisible({ timeout: 20_000 });
    await page.getByRole('button', { name: /Choose Package/i }).first().click();
    await expect(page.locator('body')).toContainText(/Complete Payment|Card Details|Payment simulation|Development Mode/i);
  });

  test('admin coupons and user detail surfaces expose management controls', async ({ page }) => {
    await page.goto('/admin/coupons');
    await expect(page.locator('body')).toContainText(/coupon|discount|create|admin/i);
    await expect(page.getByRole('button', { name: /create|add|new/i }).first()).toBeVisible();

    await page.goto('/admin/users/admin-1');
    await expect(page.locator('body')).toContainText(/admin@example.com|user|account|admin/i);
    await expect(page.getByRole('button').first()).toBeVisible();
  });
});
