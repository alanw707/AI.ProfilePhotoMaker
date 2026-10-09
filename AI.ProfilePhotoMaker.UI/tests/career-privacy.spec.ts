import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const PAGE_URL = '/app/career/privacy?e2eAuthBypass=1';

interface Opts {
  reauth?: boolean;
  /** statuses returned by successive GETs of the deletion. */
  statuses?: string[];
  retryStatuses?: string[];
}
const deletion = (status: string, scope = 'career_profile') => ({
  id: 'd1',
  scope,
  status,
  attempts: 1,
  lastError: status === 'failed' ? 'StorageUnavailable' : null,
  createdAt: '2026-10-05T10:00:00Z',
  completedAt: status === 'completed' ? '2026-10-05T10:00:05Z' : null,
});
async function mock(page: Page, o: Opts = {}) {
  const calls = { posts: [] as any[], exports: 0, retries: 0, gets: 0 };
  let statuses = [...(o.statuses ?? ['pending', 'completed'])];
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: status < 400, isAuthenticated: true, data } });
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/privacy/retention')
      return send({
        items: [
          {
            key: 'profile',
            label: 'Career profile',
            retention: 'Until you delete it or delete your account',
            notes: 'Facts you confirmed',
          },
          {
            key: 'exports',
            label: 'Downloadable files',
            retention: '24 hours',
            notes: 'Then removed automatically',
          },
        ],
        processors: [{ name: 'OpenAI', purpose: 'Drafting text for runs that use a model' }],
      });
    if (path === '/api/career/privacy/export') {
      calls.exports++;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { 'content-disposition': 'attachment; filename="career-export.json"' },
        body: '{"format":"career-export/v1"}',
      });
    }
    if (path === '/api/career/privacy/deletions' && req.method() === 'POST') {
      calls.posts.push(req.postDataJSON());
      if (o.reauth)
        return route.fulfill({
          status: 401,
          json: { success: false, error: { code: 'CareerReauthRequired', message: 'x' } },
        });
      return send(deletion('pending', req.postDataJSON().scope), 202);
    }
    if (path === '/api/career/privacy/deletions/d1/retry') {
      calls.retries++;
      statuses = [...(o.retryStatuses ?? ['completed'])];
      return send(deletion('pending'), 202);
    }
    if (path === '/api/career/privacy/deletions/d1') {
      calls.gets++;
      const next = statuses.length > 1 ? statuses.shift()! : statuses[0];
      return send(deletion(next));
    }
    return send([]);
  });
  return calls;
}
async function open(page: Page) {
  await page.goto(PAGE_URL);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { name: 'Your career data', exact: true })).toBeVisible();
}
const region = (page: Page) => page.locator('[data-deletion-status]');
async function deleteWhole(page: Page) {
  await page.getByRole('radio', { name: 'Delete my whole career profile' }).check();
  await page.getByRole('button', { name: 'Delete…' }).click();
  await page.getByLabel('Type DELETE to confirm').fill('DELETE');
  await page.getByRole('button', { name: 'Delete permanently' }).click();
}

test('title and retention table render', async ({ page }) => {
  await mock(page);
  await open(page);
  await expect(page).toHaveTitle('Career Privacy - AI Profile Photo Maker');
  const table = page.getByRole('table');
  await expect(table.getByRole('columnheader')).toHaveText(['What', 'How long', 'Notes']);
  await expect(table).toContainText('Career profile');
  await expect(table).toContainText('24 hours');
  await expect(page.locator('[data-processors]')).toContainText('OpenAI');
});

test('export triggers a JSON download', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download my career data' }).click();
  expect((await download).suggestedFilename()).toMatch(/\.json$/);
  expect(calls.exports).toBe(1);
});

test('two distinct scopes; no request until DELETE is typed and confirmed', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  const raw = page.getByRole('radio', { name: 'Delete uploaded resume files only' });
  const all = page.getByRole('radio', { name: 'Delete my whole career profile' });
  await expect(raw).toBeVisible();
  await expect(all).toBeVisible();
  await expect(page.locator('[data-deletion]')).toContainText('photos, purchases and billing');
  await expect(
    page.getByRole('link', { name: 'Delete your whole account in account settings.' })
  ).toHaveAttribute('href', '/app/settings');
  await all.check();
  await expect(raw).not.toBeChecked();
  await page.getByRole('button', { name: 'Delete…' }).click();
  const confirm = page.getByRole('button', { name: 'Delete permanently' });
  await expect(confirm).toBeDisabled();
  await page.getByLabel('Type DELETE to confirm').fill('delete');
  await expect(confirm).toBeDisabled();
  expect(calls.posts).toEqual([]);
  await page.getByRole('button', { name: 'Cancel' }).click();
  expect(calls.posts).toEqual([]);
  await page.getByRole('button', { name: 'Delete…' }).click();
  await page.getByLabel('Type DELETE to confirm').fill('DELETE');
  await confirm.click();
  await expect(region(page)).toContainText('Deleting…');
  expect(calls.posts).toEqual([{ scope: 'career_profile' }]);
});

test('resume-files scope sends raw_documents after a plain confirmation', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  await page.getByRole('radio', { name: 'Delete uploaded resume files only' }).check();
  await page.getByRole('button', { name: 'Delete…' }).click();
  await page.getByRole('button', { name: 'Delete permanently' }).click();
  await expect(region(page)).toContainText('Deleting…');
  expect(calls.posts).toEqual([{ scope: 'raw_documents' }]);
});

test('reauth asks to sign in again with a return url', async ({ page }) => {
  await mock(page, { reauth: true });
  await open(page);
  await deleteWhole(page);
  const notice = page.locator('[data-reauth]');
  await expect(notice).toContainText('For your safety, sign in again before deleting');
  await expect(notice.getByRole('link', { name: 'Sign in again' })).toHaveAttribute(
    'href',
    /returnUrl=%2Fapp%2Fcareer%2Fprivacy/
  );
  await expect(page.locator('body')).not.toContainText('CareerReauthRequired');
  await expect(region(page)).not.toContainText('Deleted');
});

test('polls until Deleted and never claims it while pending', async ({ page }) => {
  await mock(page, { statuses: ['pending', 'in_progress', 'completed'] });
  await open(page);
  await deleteWhole(page);
  await expect(region(page)).toHaveText(/Deleting…/);
  expect(await region(page).innerText()).not.toMatch(/^Deleted/m);
  await expect(region(page)).toHaveText('Deleted', { timeout: 10_000 });
  await expect(region(page)).toHaveAttribute('aria-live', 'polite');
});

test('failed shows retry and then Deleted', async ({ page }) => {
  const calls = await mock(page, { statuses: ['failed'], retryStatuses: ['completed'] });
  await open(page);
  await deleteWhole(page);
  await expect(region(page)).toContainText('Not finished — some files could not be removed yet', {
    timeout: 10_000,
  });
  await expect(region(page)).not.toHaveText(/^Deleted/);
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(region(page)).toHaveText('Deleted', { timeout: 10_000 });
  expect(calls.retries).toBe(1);
  await expect(page.locator('body')).not.toContainText('StorageUnavailable');
});

test('reload restores the pending status', async ({ page }) => {
  await mock(page, { statuses: ['in_progress'] });
  await open(page);
  await deleteWhole(page);
  await expect(region(page)).toContainText('Deleting…');
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Your career data' })).toBeVisible();
  await expect(region(page)).toContainText('Deleting…');
  await expect(region(page)).not.toContainText('Deleted');
});

test('no machine codes or raw dates', async ({ page }) => {
  await mock(page, { statuses: ['failed'] });
  await open(page);
  await deleteWhole(page);
  await expect(region(page)).toContainText('Not finished', { timeout: 10_000 });
  const body = page.locator('body');
  await expect(body).not.toContainText(/in_progress|career_profile|raw_documents|CareerReauth/);
  await expect(body).not.toContainText(/\d{4}-\d{2}-\d{2}T/);
});

test.describe('accessibility', () => {
  test.skip(!existsSync(AXE_PATH), `axe-core not found at ${AXE_PATH}; set AXE_PATH`);
  const scan = (page: Page) =>
    page.evaluate(async () => {
      const result = await (window as any).axe.run(document, {
        runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
      });
      return result.violations.map(
        (v: any) => `${v.id}: ${v.nodes.map((n: any) => n.target.join(' ')).join(' | ')}`
      );
    });
  for (const width of [1280, 390, 320]) {
    test(`privacy page: 0 axe violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mock(page, { statuses: ['failed'] });
      await open(page);
      await expect(page.getByRole('table')).toBeVisible();
      await deleteWhole(page);
      await expect(page.getByRole('button', { name: 'Retry' })).toBeVisible({ timeout: 10_000 });
      await page.addScriptTag({ path: AXE_PATH });
      expect(await scan(page)).toEqual([]);
      if (width === 320)
        expect(
          await page.evaluate(
            () => document.documentElement.scrollWidth - document.documentElement.clientWidth
          )
        ).toBeLessThanOrEqual(0);
    });
  }
});
