import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const gen = (id: string, text: string, factIds: string[]) => ({
  id,
  text,
  factIds,
  origin: 'generated',
});
const material = (o: Record<string, unknown> = {}) => ({
  id: 'mat-1',
  title: 'Targeted resume',
  etag: '"material-v1"',
  currentVersion: 1,
  pinned: { profileVersion: 1, goalVersion: 1, occupationCode: '15-1252.00' },
  stale: false,
  staleReasons: [],
  contact: { name: true, email: true, phone: false, location: false, links: false },
  sections: [
    { key: 'headline', lines: [gen('l1', 'Software developer', ['f1'])] },
    { key: 'summary', lines: [gen('l2', 'Builds reliable web apps.', ['f1', 'f2'])] },
    { key: 'experience_highlights', lines: [gen('l3', 'Shipped a billing tool', ['f2'])] },
    { key: 'skills', lines: [gen('l4', 'TypeScript', ['f3'])] },
  ],
  questions: [{ id: 'q1', factId: 'f2', text: 'How many people used the billing tool?' }],
  facts: [
    { id: 'f1', text: 'Worked as a developer for 5 years' },
    { id: 'f2', text: 'Built a billing tool at Acme' },
    { id: 'f3', text: 'Knows TypeScript' },
  ],
  ...o,
});
const changes = [
  {
    id: 'c1',
    kind: 'changed',
    section: 'summary',
    before: 'Old',
    after: 'New summary',
    factIds: ['f1'],
  },
  { id: 'c2', kind: 'added', section: 'skills', before: null, after: 'Angular', factIds: ['f3'] },
];
interface Opts {
  put?: number;
  apply?: number;
  stale?: boolean;
  m?: Record<string, unknown>;
}
async function mock(page: Page, o: Opts = {}) {
  const calls = { put: [] as any[], apply: [] as any[], restore: [] as any[], runs: [] as any[] };
  let current = material({
    stale: !!o.stale,
    staleReasons: o.stale ? ['profile_changed'] : [],
    ...o.m,
  });
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: status < 400, isAuthenticated: true, data } });
    const fail = (status: number, code?: string) =>
      route.fulfill({ status, json: { success: false, error: { code, message: code } } });
    const now = new Date().toISOString();
    const run = (extra: object) => ({
      id: 'run-1',
      task: 'targeted_resume',
      status: 'completed',
      createdAt: now,
      updatedAt: now,
      completedAt: now,
      steps: [],
      question: null,
      proposalId: null,
      profileChanged: false,
      errorCode: null,
      allowance: { used: 0, reserved: 0, limit: 10, periodStart: now },
      ...extra,
    });
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/goals')
      return send({ id: 'g1', etag: '"g1"', occupation: { code: '15-1252.00' }, goal: {} });
    if (path === '/api/career/runs' && req.method() === 'POST') {
      const body = req.postDataJSON();
      calls.runs.push(body);
      return send(
        run(body.materialId ? { materialId: 'mat-1', proposalId: 'p-1' } : { materialId: 'mat-1' }),
        201
      );
    }
    if (path === '/api/career/runs/run-1')
      return send(
        run(
          calls.runs.at(-1)?.materialId
            ? { materialId: 'mat-1', proposalId: 'p-1' }
            : { materialId: 'mat-1' }
        )
      );
    if (path === '/api/career/materials/mat-1' && req.method() === 'GET') return send(current);
    if (path === '/api/career/materials/mat-1' && req.method() === 'PUT') {
      const body = req.postDataJSON();
      calls.put.push({ body, ifMatch: req.headers()['if-match'] });
      if (o.put) {
        current = material({
          etag: '"material-v9"',
          currentVersion: 9,
          sections: [{ key: 'headline', lines: [gen('z', 'Edited elsewhere', [])] }],
        });
        return fail(o.put);
      }
      current = material({ ...body, etag: '"material-v2"', currentVersion: 2 });
      return send(current);
    }
    if (path === '/api/career/materials/mat-1/versions')
      return send({
        versions: [
          { number: 2, author: 'user', createdAt: '2026-10-05T10:00:00Z' },
          { number: 1, author: 'agent', createdAt: '2026-10-04T09:00:00Z' },
        ],
        total: 2,
      });
    if (path === '/api/career/materials/mat-1/versions/1' && req.method() === 'GET')
      return send(
        material({ sections: [{ key: 'headline', lines: [gen('o', 'Original headline', [])] }] })
      );
    if (path === '/api/career/materials/mat-1/versions/1/restore') {
      calls.restore.push(req.headers()['if-match']);
      current = material({ etag: '"material-v3"', currentVersion: 3 });
      return send(current);
    }
    if (path === '/api/career/materials/mat-1/proposals/p-1')
      return send({ id: 'p-1', baseVersion: 1, changes });
    if (path === '/api/career/materials/mat-1/proposals/p-1/apply') {
      calls.apply.push({ body: req.postDataJSON(), ifMatch: req.headers()['if-match'] });
      if (o.apply) return fail(o.apply);
      return send(current);
    }
    return send([]);
  });
  return calls;
}
async function open(page: Page, url = '/app/career/resume?e2eAuthBypass=1&material=mat-1') {
  await page.goto(url);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { name: 'Your targeted resume' })).toBeVisible();
}
const status = (page: Page) => page.locator('[data-save-status]');
const overflow = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

test('drafting runs the task then opens the editor with Based on', async ({ page }) => {
  const calls = await mock(page);
  await open(page, '/app/career/resume?e2eAuthBypass=1');
  await page.getByRole('button', { name: 'Draft my resume' }).click();
  await expect(page).toHaveURL(/material=mat-1/);
  expect(calls.runs[0]).toEqual({ task: 'targeted_resume' });
  await expect(page.locator('[data-line="l1"] [data-origin]')).toHaveText(
    'Based on: Worked as a developer for 5 years'
  );
  await expect(page.locator('[data-line="l2"] [data-origin]')).toContainText(
    'Built a billing tool at Acme'
  );
  await page.reload();
  await expect(page.getByLabel('Headline, line 1', { exact: true })).toHaveValue(
    'Software developer'
  );
});

test('questions are separate from the resume copy', async ({ page }) => {
  await mock(page);
  await open(page);
  const q = page.locator('[data-questions]');
  await expect(q).toContainText('How many people used the billing tool?');
  await expect(page.locator('[data-section]').filter({ hasText: 'How many people' })).toHaveCount(
    0
  );
  await q.getByRole('button', { name: /Add a result/ }).click();
  await expect(page.getByLabel('Experience highlights, line 2', { exact: true })).toBeFocused();
});

test('autosave sends If-Match and shows saved; edited line says Your wording', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  await page.getByLabel('Headline, line 1', { exact: true }).fill('Senior developer');
  await expect(status(page)).toHaveText('Saving…');
  await expect(status(page)).toHaveText('All changes saved');
  expect(calls.put).toHaveLength(1);
  expect(calls.put[0].ifMatch).toBe('"material-v1"');
  expect(calls.put[0].body.sections[0].lines[0].text).toBe('Senior developer');
  await expect(page.locator('[data-line="l1"] [data-origin]')).toHaveText('Your wording');
  await page.getByLabel('Skills, line 1', { exact: true }).fill('Angular');
  await expect(status(page)).toHaveText('All changes saved');
  expect(calls.put[1].ifMatch).toBe('"material-v2"');
});

test('412 keeps the draft and offers reload or compare', async ({ page }) => {
  await mock(page, { put: 412 });
  await open(page);
  await page.getByLabel('Summary, line 1', { exact: true }).fill('My careful words');
  await expect(status(page)).toHaveText('Not saved — changed elsewhere');
  await expect(page.getByLabel('Summary, line 1', { exact: true })).toHaveValue('My careful words');
  await page.getByRole('button', { name: 'Compare with the latest' }).click();
  await expect(page.locator('[data-latest]')).toContainText('Edited elsewhere');
  await expect(page.getByLabel('Summary, line 1', { exact: true })).toHaveValue('My careful words');
  await expect(page.getByRole('button', { name: 'Use the latest instead' })).toBeVisible();
});

test('unsaved draft is restored after reload', async ({ page }) => {
  await mock(page, { put: 412 });
  await open(page);
  await page.getByLabel('Summary, line 1', { exact: true }).fill('Draft that never saved');
  await expect(status(page)).toHaveText('Not saved — changed elsewhere');
  await page.reload();
  await expect(page.getByLabel('Summary, line 1', { exact: true })).toHaveValue(
    'Draft that never saved'
  );
});

test('contact defaults and no-photo note', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  const c = page.locator('[data-contact]');
  await expect(c.getByLabel('Name')).toBeChecked();
  await expect(c.getByLabel('Email')).toBeChecked();
  for (const l of ['Phone', 'Location', 'Links']) await expect(c.getByLabel(l)).not.toBeChecked();
  await expect(c).toContainText('No photo is included');
  await c.getByLabel('Phone').check();
  await expect(status(page)).toHaveText('All changes saved');
  expect(calls.put[0].body.contact.phone).toBe(true);
});

test('version history lists readable rows; view and restore', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  const v = page.locator('[data-versions]');
  await expect(v.locator('[data-version="2"]')).toContainText('Version 2 · You · 5 October 2026');
  await expect(v.locator('[data-version="1"]')).toContainText('Drafted for you');
  await v.getByRole('button', { name: 'View version 1' }).click();
  await expect(page.locator('[data-viewing]')).toContainText('Original headline');
  await v.getByRole('button', { name: 'Restore version 1' }).click();
  await expect(page.locator('[data-notice]')).toContainText('Version 1 was restored');
  expect(calls.restore).toEqual(['"material-v1"']);
});

test('proposal: partial apply sends only checked ids', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  await page.getByRole('button', { name: 'Refresh from my profile' }).click();
  const c1 = page.locator('[data-change="c1"]');
  await expect(c1).toContainText('Line changed in Summary');
  await expect(c1).toContainText('After: New summary');
  await expect(c1.getByRole('checkbox')).not.toBeChecked();
  await expect(page.locator('[data-change="c2"]').getByRole('checkbox')).not.toBeChecked();
  expect(calls.runs[0]).toEqual({ task: 'targeted_resume', materialId: 'mat-1' });
  await c1.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Apply selected changes' }).click();
  await expect(page.locator('[data-proposal-notice]')).toContainText('applied');
  expect(calls.apply).toEqual([{ body: { acceptedChangeIds: ['c1'] }, ifMatch: '"material-v1"' }]);
});

test('apply 412 says your edits were kept', async ({ page }) => {
  await mock(page, { apply: 412 });
  await open(page);
  await page.getByRole('button', { name: 'Refresh from my profile' }).click();
  await page.locator('[data-change="c1"]').getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Apply selected changes' }).click();
  await expect(page.locator('[data-proposal-error]')).toContainText('Your edits were kept');
  await expect(page.getByLabel('Summary, line 1', { exact: true })).toHaveValue(
    'Builds reliable web apps.'
  );
});

test('stale banner names the change and says nothing was deleted', async ({ page }) => {
  await mock(page, { stale: true });
  await open(page);
  const s = page.locator('[data-stale]');
  await expect(s).toContainText('Your profile changed');
  await expect(s).toContainText('Nothing was deleted');
});

test('fact text is rendered as text and no machine codes show', async ({ page }) => {
  await mock(page, {
    stale: true,
    m: {
      facts: [
        { id: 'f1', text: '<img src=x onerror="window.__pwned=1">' },
        { id: 'f2', text: 'b' },
        { id: 'f3', text: 'c' },
      ],
    },
  });
  await open(page);
  await expect(page.locator('[data-line="l1"] [data-origin]')).toContainText('<img src=x');
  expect(await page.evaluate(() => (window as any).__pwned)).toBeUndefined();
  await expect(page.locator('main img')).toHaveCount(0);
  const text = (await page.locator('main').innerText()).replace(/\s+/g, ' ');
  for (const code of [
    'experience_highlights',
    'material-v',
    'mat-1',
    '2026-10-05T',
    'profile_changed',
    'agent',
    'Career',
  ])
    expect(text).not.toContain(code);
});

test.describe('accessibility', () => {
  test.skip(!existsSync(AXE_PATH), `axe-core not found at ${AXE_PATH}; set AXE_PATH`);
  for (const width of [1280, 390, 320]) {
    test(`0 axe WCAG 2.2 AA violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mock(page, { stale: true });
      await open(page);
      await page.getByRole('button', { name: 'Refresh from my profile' }).click();
      await expect(page.locator('[data-change="c1"]')).toBeVisible();
      await page.getByRole('button', { name: 'View version 1' }).click();
      await expect(page.locator('[data-viewing]')).toBeVisible();
      await page.addScriptTag({ path: AXE_PATH });
      const violations = await page.evaluate(async () => {
        const result = await (window as any).axe.run(document, {
          runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
        });
        return result.violations.map(
          (v: any) => `${v.id}: ${v.nodes.map((n: any) => n.target.join(' ')).join(' | ')}`
        );
      });
      expect(violations).toEqual([]);
      if (width === 320) expect(await overflow(page)).toBeLessThanOrEqual(0);
    });
  }
});
