import { expect, test } from '@playwright/test';

const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl3v9kAAAAASUVORK5CYII=', 'base64');
const preview = {
  processedImageId: 77, imageUrl: '/api/headshots/images/77/original',
  storagePath: 'test/generated-private/user-1/raw.png', sourceStoragePath: 'test/uploads/user-1/source.png',
  style: 'linkedin', createdAt: '2026-08-28T00:00:00Z', hasRawPreview: true,
  canPromotePreview: false, activePackageCode: 'starter_package', remainingCandidateCount: 2,
  promotedCandidate: { processedImageId: 77, imageUrl: '/api/headshots/images/77/original', storagePath: 'test/generated-private/user-1/raw.png', provider: 'openai', model: 'gpt-image-2', correlationId: 'promotion' },
  candidates: [{ processedImageId: 77, imageUrl: '/api/headshots/images/77/original', storagePath: 'test/generated-private/user-1/raw.png', provider: 'openai', model: 'gpt-image-2', correlationId: 'promotion' }],
};

test('renders promoted private preview with authorized blob bytes', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ email: 'test@example.test', token: 'test' })));
  await page.route('**/profile-images/**', route => route.fulfill({ contentType: 'image/png', body: png }));
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/config/client')) return route.fulfill({ json: { success: true, data: { features: { openAIHeadshotMvp: true, profilePhotoWorkflowOverhaul: true, outcomePackagesVisible: true } } } });
    if (path.endsWith('/auth/account-status')) return route.fulfill({ json: { success: true, data: { emailConfirmed: true } } });
    if (path.endsWith('/auth/profile-completion-status')) return route.fulfill({ json: { isCompleted: true, hasFirstName: true, hasLastName: true, hasGender: true, hasEthnicity: true } });
    if (path.includes('/headshots/resumable-preview')) return route.fulfill({ json: { success: true, data: preview } });
    if (path.endsWith('/headshots/images/77/original')) return route.fulfill({ contentType: 'image/png', body: png });
    if (path.includes('generated-private')) return route.fulfill({ status: 404 });
    return route.fulfill({ json: { success: true, data: [] } });
  });
  await page.goto('/app/enhance');
  const image = page.locator('img[alt="Selected generated candidate"]');
  await expect(image).toBeVisible({ timeout: 10000 });
  await expect(image).toHaveAttribute('src', /^blob:/);
  await expect(page.getByText('Proof preview unavailable')).toHaveCount(0);
});
