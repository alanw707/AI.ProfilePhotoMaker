import { test, expect } from '@playwright/test';
import path from 'node:path';

// Browser-only error recovery coverage; APIs/providers are deliberately mocked.
test.beforeEach(async ({ page }) => {
  await page.route('**/*', route => {
    const host = new URL(route.request().url()).hostname;
    return host === 'localhost' || host === '127.0.0.1' ? route.continue() : route.abort();
  });
  await page.route('**/profile-images/style-previews/**', route => route.abort());
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    if (url.endsWith('/placeholder/style-preview')) {
      return route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="80" height="80"><rect width="80" height="80" fill="#eee"/></svg>' });
    }
    if (url.endsWith('/headshots/generate')) {
      return route.fulfill({ status: 500, json: { success: false, error: { code: 'GenerationFailed', message: 'Generation unavailable. Please try again.' } } });
    }
    const responses: Record<string, unknown> = {
      '/config/client': { features: { openAIHeadshotMvp: true, profilePhotoWorkflowOverhaul: true, outcomePackagesVisible: true, profilePhotoScoreVisible: true } },
      '/auth/account-status': { emailConfirmed: true },
      '/auth/user-roles': [],
      '/credit/status': { credits: 5, lastCreditReset: '2026-01-01', nextResetDate: '2026-02-01' },
      '/profile': { firstName: 'Test', lastName: 'User' },
      '/style': [{ id: 1, name: 'linkedin', description: 'Professional portrait', isActive: true }],
      '/profilephotoworkflow/packages': [{ id: 1, code: 'free_preview', name: 'Free Preview', includedCandidateCount: 1, price: 0, currency: 'USD', highlights: [] }],
      '/profilephotoworkflow/entitlements': [],
      '/profilephotoworkflow/export-options': [],
      '/headshots/resumable-preview': null,
      '/profilephotoworkflow/score': { overallScore: 86, subscores: [], strengths: [], improvements: [], qualityGate: { status: 'pass', reasons: [], recommendations: [] } },
      '/image/upload': { uploadedFiles: [{ Url: '/uploads/source.jpg', FileName: 'source.jpg', StoragePath: 'source/source.jpg' }] },
    };
    if (url.endsWith('/style-preview/list')) return route.fulfill({ json: { success: true, count: 0, previews: [] } });
    const key = Object.keys(responses).find(key => url.endsWith(key));
    return route.fulfill({ json: { success: true, isAuthenticated: true, data: key ? responses[key] : {}, error: null } });
  });
  await page.goto('/app/enhance?e2eAuthBypass=1');
  await page.locator('input[type="file"]').setInputFiles(path.resolve(__dirname, '../cypress/fixtures/test-image.jpg'));
  await expect(page.getByRole('heading', { name: 'Choose your portrait direction' })).toBeVisible();
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
});

test('dark workspace headings retain readable contrast against their panels', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.getByRole('button', { name: 'Switch to dark theme', exact: true }).click();
  const ratio = await page.getByRole('heading', { name: 'Choose your portrait direction' }).evaluate(heading => {
    const luminance = (color: string) => (color.match(/[\d.]+/g) || []).slice(0, 3)
      .map(Number).map(value => value / 255)
      .map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4)
      .reduce((total, value, index) => total + value * [0.2126, 0.7152, 0.0722][index], 0);
    const foreground = luminance(getComputedStyle(heading).color);
    let panel: Element | null = heading;
    while (panel.parentElement && getComputedStyle(panel).backgroundColor === 'rgba(0, 0, 0, 0)') panel = panel.parentElement;
    const background = luminance(getComputedStyle(panel).backgroundColor);
    return (Math.max(foreground, background) + 0.05) / (Math.min(foreground, background) + 0.05);
  });
  expect(ratio).toBeGreaterThanOrEqual(4.5);
});

test('unavailable portrait previews use a working placeholder', async ({ page }) => {
  const image = page.getByRole('img', { name: /portrait style preview/ }).first();
  await expect(image).toHaveAttribute('src', /\/placeholder\/style-preview$/);
  await expect.poll(() => image.evaluate((el: HTMLImageElement) => el.complete && el.naturalWidth > 0)).toBe(true);
});

test('dismissing a generation error preserves the photo and resumes only the saved request', async ({ page }) => {
  const requests: Record<string, unknown>[] = [];
  let uploads = 0;
  page.on('request', request => {
    if (new URL(request.url()).pathname.endsWith('/headshots/generate')) requests.push(request.postDataJSON());
    if (new URL(request.url()).pathname.endsWith('/image/upload')) uploads++;
  });
  await page.getByRole('checkbox', { name: /I consent/ }).first().check();
  const generate = page.getByRole('button', { name: 'Generate Free Preview', exact: true });
  await expect(generate).toBeEnabled();
  await generate.click();
  await expect(page.getByRole('heading', { name: 'We could not finish that action' })).toBeVisible();
  await page.getByRole('button', { name: 'Return to your work' }).click();
  await expect(page.getByRole('heading', { name: 'We could not finish that action' })).not.toBeVisible();
  await expect(page.getByRole('heading', { name: 'Choose your portrait direction' })).toBeVisible();
  await expect(generate).not.toBeVisible();
  const resume = page.getByRole('button', { name: 'Resume generation', exact: true });
  await expect(resume).toBeEnabled();
  await resume.click();
  await expect(page.getByRole('heading', { name: 'We could not finish that action' })).toBeVisible();
  expect(requests).toHaveLength(2);
  expect(requests[1]).toEqual(requests[0]);
  expect(uploads).toBe(1);
  expect(await page.evaluate(() => localStorage.getItem('photoWorkspaceInterruptedGeneration'))).not.toBeNull();
});
