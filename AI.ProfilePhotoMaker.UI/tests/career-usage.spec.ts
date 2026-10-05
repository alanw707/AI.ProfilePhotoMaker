import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const HOME = '/app/career?e2eAuthBypass=1';
const journey = {
  profile: { version: 1, confirmed: true },
  goal: { version: 1, occupationTitle: 'Software Developers', location: 'Denver, CO' },
  nextAction: { key: 'none' },
  latestResult: null,
  activeRuns: [],
  stale: [],
};
const allowance = (over = {}) => ({
  policyVersion: 'v1',
  limit: 20,
  used: 5,
  reserved: 0,
  remaining: 15,
  resetsAt: '2026-11-01T00:00:00Z',
  ...over,
});
const usage = {
  actions: [
    {
      action: 'pay_analysis',
      count: 12,
      costUsd: 1.5,
      latencyP50Ms: 900,
      latencyP95Ms: 2400,
      failureRate: 0.05,
    },
  ],
  perUser: { activeUsers: 4, p50Cost: 0.2, p95Cost: 0.9, maxCost: 1.1, p50Runs: 2, p95Runs: 7 },
  totalCostUsd: 1.5,
};

async function mock(page: Page, opts: { allowance?: object; runStatus?: number; code?: string }) {
  const calls = { puts: [] as unknown[] };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown) =>
      route.fulfill({ json: { success: true, isAuthenticated: true, data, roles: ['Admin'] } });
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/auth/user-roles') return send({ roles: ['Admin'] });
    if (path === '/api/career/journey') return send(journey);
    if (path === '/api/career/allowance') return send(allowance(opts.allowance));
    if (path === '/api/career/runs' && req.method() === 'POST') {
      return route.fulfill({
        status: opts.runStatus ?? 503,
        headers: { 'Retry-After': '60' },
        json: { success: false, error: { code: opts.code, message: 'raw' } },
      });
    }
    if (path === '/api/admin/career/usage') return send(usage);
    if (path === '/api/admin/career/controls') {
      if (req.method() === 'PUT') {
        calls.puts.push(req.postDataJSON());
        return send({ ...req.postDataJSON(), updatedAt: '2026-10-05T12:00:00Z' });
      }
      return send({ generationDisabled: false, sourcesDisabled: false, updatedAt: null });
    }
    return send([]);
  });
  return calls;
}
async function open(page: Page, url: string, heading: string) {
  await page.goto(url);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible();
}

test('allowance shows remaining, in progress and a readable reset date', async ({ page }) => {
  await mock(page, { allowance: { reserved: 2, remaining: 13 } });
  await open(page, HOME, 'Career workspace');
  await expect(page.locator('[data-allowance-count]')).toContainText('13 of 20 drafts left');
  await expect(page.locator('[data-allowance-reserved]')).toHaveText('2 in progress');
  await expect(page.locator('[data-allowance-reset]')).toHaveText('Resets on November 1, 2026.');
  await expect(page.locator('[data-allowance]')).not.toContainText('2026-11-01');
});

test('an used-up allowance still offers edit and export links', async ({ page }) => {
  await mock(page, { allowance: { used: 20, remaining: 0 } });
  await open(page, HOME, 'Career workspace');
  const used = page.locator('[data-allowance-used]');
  await expect(used).toContainText('readable, editable and exportable');
  await expect(used.getByRole('link', { name: 'edit your profile' })).toBeVisible();
  await expect(used.getByRole('link', { name: 'export your materials' })).toBeVisible();
});

for (const [code, text] of [
  ['CareerGenerationPaused', 'Drafting is paused for now. Your saved work is still available.'],
  ['CareerBusy', 'Busy \u2014 try again in a minute.'],
]) {
  test(`starting a run shows plain copy for ${code}`, async ({ page }) => {
    await mock(page, { code });
    await open(page, '/app/career/summary?e2eAuthBypass=1', 'Profile summary');
    await page.getByRole('button', { name: 'Start draft' }).click();
    await expect(page.getByRole('alert')).toHaveText(text);
    await expect(page.locator('body')).not.toContainText(code);
  });
}

test('admin page shows usage and toggles only after confirmation', async ({ page }) => {
  const calls = await mock(page, {});
  await open(page, '/admin/career-usage?e2eAuthBypass=1', 'Career usage');
  const row = page.locator('[data-action-row]');
  await expect(row).toHaveCount(1);
  await expect(row).toContainText('Pay analysis');
  await expect(row).not.toContainText('pay_analysis');
  await expect(page.locator('[data-per-user]')).toContainText('4');
  await page.locator('[data-switch="generationDisabled"]').click();
  await expect(page.locator('[data-confirm]')).toBeVisible();
  expect(calls.puts).toEqual([]);
  await page.getByRole('button', { name: 'Confirm' }).click();
  await expect(page.getByText('Saved.')).toBeVisible();
  expect(calls.puts).toEqual([{ generationDisabled: true, sourcesDisabled: false }]);
  await expect(page.getByText('Last changed Oct 5, 2026')).toBeVisible();
});

for (const [w, h] of [
  [1280, 800],
  [390, 844],
  [320, 640],
]) {
  for (const url of [HOME, '/admin/career-usage?e2eAuthBypass=1']) {
    test(`axe and no overflow at ${w} on ${url.split('?')[0]}`, async ({ page }) => {
      test.skip(!existsSync(AXE_PATH), 'axe-core not installed');
      await page.setViewportSize({ width: w, height: h });
      await mock(page, { allowance: { reserved: 1, remaining: 0 } });
      await open(page, url, url === HOME ? 'Career workspace' : 'Career usage');
      await page.addScriptTag({ path: AXE_PATH });
      const result = await page.evaluate(() =>
        (window as any).axe.run(document, {
          runOnly: {
            type: 'tag',
            values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
          },
        })
      );
      expect(result.violations.map((v: any) => v.id)).toEqual([]);
      if (w === 320) {
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
          true
        );
      }
    });
  }
}
