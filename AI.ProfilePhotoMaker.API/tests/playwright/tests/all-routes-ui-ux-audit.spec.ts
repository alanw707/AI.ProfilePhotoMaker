import { test, expect, Page, Route } from '@playwright/test';

const baseOrigin = process.env.BASE_URL || 'http://localhost:4200';
const jwtPayload = Buffer.from(JSON.stringify({ sub: 'admin-1', email: 'admin@example.com', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64');
const mockToken = `mock.${jwtPayload}.signature`;

const publicRoutes = [
  '/',
  '/home',
  '/how-it-works',
  '/examples',
  '/reviews',
  '/corporate-headshot',
  '/free-headshot-enhancer',
  '/ai-headshot-generator',
  '/linkedin-headshots',
  '/professional-headshots',
  '/headshots-for-job-search',
  '/realtor-headshots',
  '/lawyer-headshots',
  '/doctor-headshots',
  '/compare/aragon-ai',
  '/compare/headshotpro',
  '/pricing',
  '/packages',
  '/features',
  '/dating-app-headshots',
  '/real-estate-agent-headshots',
  '/medical-professional-headshots',
  '/nurse-headshots',
  '/teacher-headshots',
  '/blog',
  '/blog/how-to-choose-linkedin-profile-photo',
  '/help',
  '/help/faq',
  '/legal/privacy',
  '/legal/terms',
  '/legal/retention-policy',
  '/legal/refund-policy',
  '/legal/cookies',
  '/legal/subprocessors',
  '/legal/ai-transparency',
  '/legal/acceptable-use',
  '/legal/ip-dmca',
  '/legal/security',
  '/legal/biometric-consent',
  '/legal/children-privacy',
  '/privacy',
  '/terms',
  '/unsubscribe',
  '/404',
];

const appRoutes = [
  '/app/enhance?e2eAuthBypass=1',
  '/app/enhance?e2eAuthBypass=1',
  '/app/gallery?e2eAuthBypass=1',
  '/app/settings?e2eAuthBypass=1',
  '/app/support?e2eAuthBypass=1',
  '/app/enhance?e2eAuthBypass=1',
  '/enhance?e2eAuthBypass=1',
  '/gallery?e2eAuthBypass=1',
  '/settings?e2eAuthBypass=1',
];

const authRoutes = ['/auth/login', '/auth/register', '/auth/complete-profile', '/auth/verify-email', '/auth/confirm-email'];
const adminRoutes = ['/admin/dashboard', '/admin/users', '/admin/users/admin-1', '/admin/coupons', '/admin/audit-log', '/admin/campaigns'];

async function fulfillJson(route: Route, data: unknown) {
  await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
}

async function installRouteAuditMocks(page: Page) {
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

  await page.route('**/api/auth/user-roles', route => fulfillJson(route, { success: true, data: { roles: ['Admin'] }, error: null }));
  await page.route('**/api/auth/**', route => {
    const url = route.request().url();
    const body = url.includes('profile-completion-status')
      ? { isCompleted: true }
      : { success: true, data: { valid: true, emailConfirmed: true }, error: null };
    return fulfillJson(route, body);
  });

  await page.route('**/api/profile/data-stats', route => fulfillJson(route, {
    success: true,
    data: { inputPhotos: 0, generatedPhotos: 0, hasTrainedModel: false, totalDataSize: 0, accountAge: 1 },
    error: null,
  }));
  await page.route('**/api/profile/**', route => fulfillJson(route, { success: true, data: {}, error: null }));

  await page.route('**/api/credit/status', route => fulfillJson(route, {
    success: true,
    data: { credits: 5, lastCreditReset: new Date().toISOString(), nextResetDate: new Date().toISOString() },
    error: null,
  }));
  await page.route('**/api/credit/**', route => fulfillJson(route, { success: true, data: [], error: null }));

  await page.route('**/api/profilephotoworkflow/packages', route => fulfillJson(route, {
    success: true,
    data: [
      { id: 1, code: 'free_preview', name: 'Free Preview', description: 'Preview one candidate', price: 0, currency: 'USD', includedCandidateCount: 1, includedRefinementCount: 0, includedPremiumAugmentationCount: 0, includesPlatformExportKit: false, includesScoreDelta: true, displayOrder: 1, highlights: ['Score before you pay'] },
      { id: 2, code: 'starter_package', name: 'Starter Package', description: 'Profile-ready set', price: 900, currency: 'USD', includedCandidateCount: 3, includedRefinementCount: 1, includedPremiumAugmentationCount: 1, includesPlatformExportKit: true, includesScoreDelta: true, displayOrder: 2, highlights: ['3 candidates', 'Platform exports'] },
      { id: 3, code: 'pro_package', name: 'Pro Package', description: 'Full set', price: 1900, currency: 'USD', includedCandidateCount: 9, includedRefinementCount: 3, includedPremiumAugmentationCount: 2, includesPlatformExportKit: true, includesScoreDelta: true, displayOrder: 3, highlights: ['9 candidates'] },
    ],
    error: null,
  }));
  await page.route('**/api/profilephotoworkflow/entitlements', route => fulfillJson(route, { success: true, data: [], error: null }));
  await page.route('**/api/profilephotoworkflow/export-options', route => fulfillJson(route, { success: true, data: [], error: null }));

  await page.route('**/api/image/**', route => fulfillJson(route, {
    success: true,
    data: { images: [], totalImages: 0, uploadedImages: 0, generatedImages: 0, totalProcessedImages: 0 },
    error: null,
  }));

  await page.route('**/api/admin/**', route => fulfillJson(route, {
    success: true,
    data: { items: [], users: [], campaigns: [], coupons: [], auditLogs: [], totalCount: 0, stats: {} },
    error: null,
  }));

  await page.route('**/api/support/**', route => fulfillJson(route, { success: true, data: [], error: null }));
  await page.route('**/api/blog/**', route => fulfillJson(route, { success: true, data: [], error: null }));
  await page.route('**/api/**', route => fulfillJson(route, { success: true, data: {}, error: null }));
}

async function seedAuthenticatedAdmin(page: Page) {
  await page.addInitScript(({ token }) => {
    localStorage.setItem('auth_token', token);
    localStorage.setItem('currentUser', JSON.stringify({ token, email: 'admin@example.com', firstName: 'Admin', lastName: 'User' }));
    localStorage.setItem('e2eAuthBypass', 'true');
    localStorage.setItem('biometricConsent', JSON.stringify({ accepted: true, acceptedAt: new Date().toISOString() }));
  }, { token: mockToken });
}

async function auditRoute(page: Page, route: string) {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  const response = await page.goto(route, { waitUntil: 'domcontentloaded' });
  expect(response?.status(), route).toBeLessThan(500);
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => undefined);
  await expect(page.locator('body')).toBeVisible();
  const bodyText = (await page.locator('body').innerText()).trim();
  expect(bodyText.length, route).toBeGreaterThan(20);
  await expect(page.getByText(/Cannot match any routes|Unhandled Runtime Error|TypeError:/i)).toHaveCount(0);
  expect(errors, route).toEqual([]);

  const horizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2);
  expect(horizontalOverflow, `${route} horizontal overflow`).toBeFalsy();
}

test.describe('All routed surfaces smoke and UI/UX audit', () => {
  test.beforeEach(async ({ page }) => {
    await seedAuthenticatedAdmin(page);
    await installRouteAuditMocks(page);
  });

  for (const route of publicRoutes) {
    test(`public route renders cleanly: ${route}`, async ({ page }) => {
      await auditRoute(page, route);
    });
  }

  for (const route of appRoutes) {
    test(`authenticated app route renders cleanly: ${route}`, async ({ page }) => {
      await auditRoute(page, route);
    });
  }

  for (const route of authRoutes) {
    test(`auth route renders cleanly: ${route}`, async ({ page }) => {
      await auditRoute(page, route);
    });
  }

  for (const route of adminRoutes) {
    test(`admin route renders cleanly: ${route}`, async ({ page }) => {
      await auditRoute(page, route);
    });
  }
});
