import { expect, test, Page } from '@playwright/test';

// #404: a photo opened for refinement (?refineImageId=) must survive checkout.
const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl3v9kAAAAASUVORK5CYII=',
  'base64'
);
const source = {
  processedImageId: 42,
  imageUrl: '/api/headshots/images/42/original',
  storagePath: 'test/generated/user-1/42.png',
};
const packages = [
  {
    code: 'starter_package',
    name: 'Starter',
    description: 'Starter package',
    includedCandidateCount: 4,
    includedRefinementCount: 2,
    includesPlatformExportKit: true,
    internalCreditPackageId: 11,
  },
];

async function mock(page: Page, opts: { entitled: boolean }) {
  const calls = { entitlements: 0, source: 0 };
  await page.addInitScript(() =>
    localStorage.setItem(
      'currentUser',
      JSON.stringify({ email: 'test@example.test', token: 'test' })
    )
  );
  await page.route('**/profile-images/**', route =>
    route.fulfill({ contentType: 'image/png', body: png })
  );
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/config/client'))
      return route.fulfill({
        json: {
          success: true,
          data: {
            features: {
              openAIHeadshotMvp: true,
              profilePhotoWorkflowOverhaul: true,
              outcomePackagesVisible: true,
            },
          },
        },
      });
    if (path.endsWith('/auth/account-status'))
      return route.fulfill({ json: { success: true, data: { emailConfirmed: true } } });
    if (path.endsWith('/auth/profile-completion-status'))
      return route.fulfill({
        json: {
          isCompleted: true,
          hasFirstName: true,
          hasLastName: true,
          hasGender: true,
          hasEthnicity: true,
        },
      });
    if (path.endsWith('/profilephotoworkflow/images/42/studio-source')) {
      calls.source++;
      return route.fulfill({ json: { success: true, data: source } });
    }
    if (path.endsWith('/profilephotoworkflow/entitlements')) {
      calls.entitlements++;
      const data = opts.entitled
        ? [
            {
              packageCode: 'starter_package',
              status: 'Active',
              remainingCandidates: 4,
              remainingRefinements: 2,
              remainingPremiumAugmentations: 0,
              platformExportKitAvailable: true,
            },
          ]
        : [];
      return route.fulfill({ json: { success: true, data } });
    }
    if (path.endsWith('/profilephotoworkflow/packages'))
      return route.fulfill({ json: { success: true, data: packages } });
    if (path.endsWith('/headshots/images/42/original'))
      return route.fulfill({ contentType: 'image/png', body: png });
    if (path.includes('/headshots/resumable-preview'))
      return route.fulfill({ json: { success: true, data: null } });
    return route.fulfill({ json: { success: true, data: [] } });
  });
  return calls;
}

test('checkout started while refining carries refineImageId in the returnUrl', async ({ page }) => {
  await mock(page, { entitled: false });
  await page.goto('/app/enhance?refineImageId=42');
  await expect(page.getByText('Refining saved photo')).toBeVisible({ timeout: 10000 });
  await page.getByRole('button', { name: 'Upgrade to Starter' }).click();
  await page.waitForURL(/\/pricing/);
  const returnUrl = new URL(page.url()).searchParams.get('returnUrl') ?? '';
  expect(returnUrl.startsWith('/app/enhance')).toBe(true);
  expect(new URLSearchParams(returnUrl.split('?')[1]).get('refineImageId')).toBe('42');
});

test('returning upgraded with refineImageId reloads entitlements and reopens the photo', async ({
  page,
}) => {
  const calls = await mock(page, { entitled: true });
  await page.goto('/app/enhance?refineImageId=42&upgraded=starter_package');
  await expect(page.getByText('Refining saved photo')).toBeVisible({ timeout: 10000 });
  await expect(page.locator('img[alt="Selected generated candidate"]')).toBeVisible();
  await expect(page.getByRole('region', { name: 'Starter' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Free Preview' })).toHaveCount(0);
  expect(calls.source).toBe(1);
  // once at start-up plus a fresh reload for the purchase just made
  expect(calls.entitlements).toBeGreaterThanOrEqual(2);
  await expect(page.getByRole('button', { name: 'Upgrade to Starter' })).toHaveCount(0);
});
