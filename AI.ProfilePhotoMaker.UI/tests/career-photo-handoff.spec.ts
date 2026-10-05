import { test, expect, Page } from '@playwright/test';

const GOAL_ID = '3f2b8c1e-6a4d-4e0b-9d57-1c2a3b4c5d6e';
const OTHER_GOAL_ID = '9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d';
const IMAGE =
  'data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%2280%22 height=%2280%22%3E%3Crect width=%2280%22 height=%2280%22 fill=%22%23ccc%22/%3E%3C/svg%3E';

interface Options {
  hasGoal?: boolean;
  goalId?: string;
  photosStatus?: number;
  selectedPhotoId?: number | null;
  selectedPhotoAvailable?: boolean;
  entitlements?: unknown[];
}

const photos = [
  {
    id: 42,
    imageUrl: IMAGE,
    createdAt: '2026-09-01T10:00:00Z',
    style: 'linkedin',
    isWatermarkedPreview: false,
  },
  {
    id: 43,
    imageUrl: IMAGE,
    createdAt: '2026-09-02T10:00:00Z',
    style: 'creative',
    isWatermarkedPreview: true,
  },
];

async function mockBackend(page: Page, options: Options = {}) {
  const state = { selectedPhotoId: options.selectedPhotoId ?? null, selects: [] as unknown[] };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    const method = route.request().method();
    const data = (body: unknown, status = 200) =>
      route.fulfill({
        status,
        json: { success: true, isAuthenticated: true, data: body, error: null },
      });
    const fail = (status: number, code: string) =>
      route.fulfill({ status, json: { success: false, error: { code, message: code } } });

    if (url === '/api/career/photos' && method === 'GET') {
      if (options.photosStatus && options.photosStatus !== 200)
        return fail(options.photosStatus, 'Unauthorized');
      return data({
        photos,
        selectedPhotoId: state.selectedPhotoId,
        selectedPhotoAvailable: options.selectedPhotoAvailable ?? true,
        entitlements: options.entitlements ?? [],
      });
    }
    if (url === '/api/career/photos/selection' && method === 'PUT') {
      const body = route.request().postDataJSON();
      state.selects.push(body);
      if (body.processedImageId === 43) return fail(409, 'CareerPhotoIsPreview');
      state.selectedPhotoId = body.processedImageId;
      return data({
        selectedPhotoId: body.processedImageId,
        careerGoalId: GOAL_ID,
        selectedAt: new Date().toISOString(),
      });
    }
    if (url === '/api/career/photos/selection' && method === 'DELETE') {
      state.selectedPhotoId = null;
      return route.fulfill({ status: 204 });
    }
    if (url === '/api/career/goals') {
      if (options.hasGoal === false) return fail(404, 'CareerGoalNotFound');
      return data({
        id: options.goalId ?? GOAL_ID,
        version: 1,
        etag: '"g1"',
        goal: { targetRole: 'Analyst' },
      });
    }
    if (url === '/api/career/profile') return fail(404, 'CareerProfileNotFound');
    if (url === '/api/config/client') {
      return data({
        features: {
          careerWorkspace: true,
          openAIHeadshotMvp: true,
          profilePhotoWorkflowOverhaul: true,
          outcomePackagesVisible: true,
          profilePhotoScoreVisible: true,
        },
      });
    }
    if (url.endsWith('/placeholder/style-preview')) {
      return route.fulfill({
        contentType: 'image/svg+xml',
        body: '<svg xmlns="http://www.w3.org/2000/svg" width="8" height="8"/>',
      });
    }
    if (url.endsWith('/style-preview/list')) {
      return route.fulfill({ json: { success: true, count: 0, previews: [] } });
    }
    const responses: Record<string, unknown> = {
      '/auth/account-status': { emailConfirmed: true },
      '/auth/user-roles': [],
      '/credit/status': { credits: 5, lastCreditReset: '2026-01-01', nextResetDate: '2026-02-01' },
      '/profile': { firstName: 'Test', lastName: 'User' },
      '/style': [{ id: 1, name: 'linkedin', description: 'Professional portrait', isActive: true }],
      '/profilephotoworkflow/packages': [],
      '/profilephotoworkflow/entitlements': [],
      '/profilephotoworkflow/export-options': [],
      '/headshots/resumable-preview': null,
    };
    const key = Object.keys(responses).find(candidate => url.endsWith(candidate));
    return data(key ? responses[key] : {});
  });
  return state;
}

const backLink = (page: Page) => page.getByRole('link', { name: 'Back to career materials' });

test('lists photos, saves a chosen photo and shows it as selected', async ({ page }) => {
  const state = await mockBackend(page);
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  await expect(page.getByRole('heading', { name: 'Career materials', level: 1 })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Profile photo (optional)' })).toBeVisible();
  await expect(
    page.getByText('No photo chosen. A photo is optional for your career profile.')
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Use this photo' })).toBeDisabled();

  await page.getByRole('radio', { name: /Your photo from .*linkedin/ }).check();
  await page.getByRole('button', { name: 'Use this photo' }).click();
  await expect(
    page.getByRole('status').filter({ hasText: 'Photo saved to your career profile.' })
  ).toBeVisible();
  await expect(page.getByRole('button', { name: 'Stop using this photo' })).toBeVisible();
  expect(state.selects).toEqual([{ processedImageId: 42 }]);

  await page.getByRole('button', { name: 'Stop using this photo' }).click();
  await expect(
    page.getByText('No photo chosen. A photo is optional for your career profile.')
  ).toBeVisible();
});

test('a watermarked preview cannot be chosen', async ({ page }) => {
  const state = await mockBackend(page);
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  await expect(page.getByRole('radio', { name: /Your photo from .*creative/ })).toBeDisabled();
  await expect(
    page.getByText('Watermarked preview - improve it in the photo workspace first')
  ).toBeVisible();
  expect(state.selects).toEqual([]);
});

test('shows package allowances and the no-spend disclosure', async ({ page }) => {
  await mockBackend(page, {
    entitlements: [
      {
        packageCode: 'starter_package',
        packageName: 'Starter Package',
        remainingCandidates: 2,
        remainingRefinements: 3,
        remainingPremiumAugmentations: 0,
        platformExportKitAvailable: true,
        expiresAt: null,
      },
    ],
  });
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  await expect(page.getByText('Starter Package: 2 candidates, 3 refinements left')).toBeVisible();
  await expect(page.getByText(/Opening the photo workspace is free\./)).toContainText(
    'Career features never use your photo allowances.'
  );
  await expect(page.getByRole('link', { name: 'Continue without a photo' })).toHaveAttribute(
    'href',
    '/app/career'
  );
});

test('says so when there is no package and when the chosen photo is gone', async ({ page }) => {
  await mockBackend(page, { selectedPhotoId: 99, selectedPhotoAvailable: false });
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  await expect(page.getByText('You have no photo package.')).toBeVisible();
  await expect(page.getByText('The photo you chose is no longer available.')).toBeVisible();
});

test('improving a photo carries the career return and the workspace links back', async ({
  page,
}) => {
  await mockBackend(page);
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  await page.getByRole('link', { name: 'Improve in photo workspace' }).first().click();
  await expect(page).toHaveURL(/\/app\/enhance\?/);
  const url = new URL(page.url());
  expect(url.searchParams.get('refineImageId')).toBe('42');
  expect(url.searchParams.get('careerReturn')).toBe('materials');
  expect(url.searchParams.get('careerGoal')).toBe(GOAL_ID);
  await expect(page.getByText('You came from your career materials.')).toBeVisible();

  await backLink(page).click();
  await expect(page).toHaveURL(new RegExp(`/app/career/materials\\?careerGoal=${GOAL_ID}`));
  await expect(page.getByRole('heading', { name: 'Career materials', level: 1 })).toBeVisible();
  await expect(
    page.getByText('Your career goal changed while you were in the photo workspace.')
  ).not.toBeVisible();
});

test('creating a photo links into the workspace with the goal', async ({ page }) => {
  await mockBackend(page);
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  const create = page.getByRole('link', { name: 'Create a new photo' });
  await expect(create).toHaveAttribute(
    'href',
    `/app/enhance?careerReturn=materials&careerGoal=${GOAL_ID}`
  );
});

test('without a goal the workspace links carry no return', async ({ page }) => {
  await mockBackend(page, { hasGoal: false });
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  await expect(page.getByText('Save a goal first if you want a link back here.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Create a new photo' })).toHaveAttribute(
    'href',
    '/app/enhance'
  );
});

test('notes when the goal changed while in the workspace', async ({ page }) => {
  await mockBackend(page, { goalId: OTHER_GOAL_ID });
  await page.goto(`/app/career/materials?e2eAuthBypass=1&careerGoal=${GOAL_ID}`);
  await expect(
    page.getByText('Your career goal changed while you were in the photo workspace.')
  ).toBeVisible();
});

for (const key of [
  'https://evil.com',
  '//evil.com',
  '/\\evil.com',
  'javascript:alert(1)',
  '/app/career/materials',
]) {
  test(`ignores the return key ${key} and shows no back link`, async ({ page }) => {
    await mockBackend(page);
    const origin = new URL(page.url() === 'about:blank' ? 'http://localhost:4200' : page.url())
      .origin;
    await page.goto(
      `/app/enhance?e2eAuthBypass=1&careerReturn=${encodeURIComponent(key)}&careerGoal=${GOAL_ID}`
    );
    await expect(
      page.getByRole('heading', { name: 'Your professional photo, proofed and ready' })
    ).toBeVisible();
    await expect(backLink(page)).toHaveCount(0);
    await expect(page.getByText('You came from your career materials.')).toHaveCount(0);
    expect(new URL(page.url()).origin).toBe(origin);
  });
}

test('ignores a bad goal id', async ({ page }) => {
  await mockBackend(page);
  await page.goto('/app/enhance?e2eAuthBypass=1&careerReturn=materials&careerGoal=not-a-guid');
  await expect(
    page.getByRole('heading', { name: 'Your professional photo, proofed and ready' })
  ).toBeVisible();
  await expect(backLink(page)).toHaveCount(0);
});

test('an expired session leaves the materials page for sign-in', async ({ page }) => {
  await mockBackend(page, { photosStatus: 401 });
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  // The e2e auth bypass makes the guest guard bounce /auth/login onward, so the exact
  // returnUrl is asserted in career-materials.component.spec.ts; here we prove we leave.
  await expect(page.getByRole('heading', { name: 'Career materials', level: 1 })).toHaveCount(0);
  await expect(page.getByRole('radio')).toHaveCount(0);
});

test('materials page has no horizontal overflow at 320px', async ({ page }) => {
  await mockBackend(page);
  await page.setViewportSize({ width: 320, height: 800 });
  await page.goto('/app/career/materials?e2eAuthBypass=1');
  await expect(page.getByRole('heading', { name: 'Career materials', level: 1 })).toBeVisible();
  await expect(page.getByRole('radio', { name: /Your photo from .*linkedin/ })).toBeVisible();
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth
  );
  expect(overflow).toBeLessThanOrEqual(0);
});
