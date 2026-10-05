import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const IMAGE =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==';
const gen = (id: string, text: string, factIds: string[]) => ({
  id,
  text,
  factIds,
  origin: 'generated',
});
const summary = (o: Record<string, unknown> = {}) => ({
  id: 'sum-1',
  kind: 'summary',
  title: 'Professional summary',
  etag: '"material-v1"',
  currentVersion: 1,
  pinned: { profileVersion: 1, goalVersion: 1, occupationCode: '15-1252.00' },
  stale: false,
  staleReasons: [],
  contact: { name: true, email: true, phone: false, location: false, links: false },
  sections: [
    { key: 'short', lines: [gen('s1', 'Software developer with 5 years of experience.', ['f1'])] },
    {
      key: 'long',
      lines: [
        gen('s2', 'I build reliable web apps. I shipped a billing tool at Acme.', ['f1', 'f2']),
      ],
    },
  ],
  questions: [],
  facts: [
    { id: 'f1', text: 'Worked as a developer for 5 years' },
    { id: 'f2', text: 'Built a billing tool at Acme' },
  ],
  ...o,
});
const resumeSummary = {
  id: 'res-1',
  kind: 'resume',
  title: 'Targeted resume',
  stale: true,
  currentVersion: 2,
  updatedAt: '2026-10-05T10:00:00Z',
};
const summaryItem = {
  id: 'sum-1',
  kind: 'summary',
  title: 'Professional summary',
  stale: false,
  currentVersion: 1,
  updatedAt: '2026-10-04T09:00:00Z',
};
interface Opts {
  photoSelected?: boolean;
  runStatus?: number;
  exportStatus?: number;
  downloadStatus?: number;
  exportCode?: string;
}
async function mock(page: Page, o: Opts = {}) {
  const calls = { runs: [] as any[], exports: [] as any[], downloads: [] as string[] };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: status < 400, isAuthenticated: true, data } });
    const fail = (status: number, code?: string) =>
      route.fulfill({ status, json: { success: false, error: { code, message: code } } });
    const now = new Date().toISOString();
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/goals')
      return send({ id: 'g1', etag: '"g1"', occupation: { code: '15-1252.00' }, goal: {} });
    if (path === '/api/career/photos')
      return send({
        photos: [
          {
            id: 42,
            imageUrl: IMAGE,
            createdAt: '2026-09-01T10:00:00Z',
            style: 'linkedin',
            isWatermarkedPreview: false,
          },
        ],
        selectedPhotoId: o.photoSelected ? 42 : null,
        selectedPhotoAvailable: true,
        entitlements: [],
      });
    if (path === '/api/career/materials' && req.method() === 'GET')
      return send({ materials: [resumeSummary, summaryItem] });
    if (path === '/api/career/runs' && req.method() === 'POST') {
      calls.runs.push(req.postDataJSON());
      if (o.runStatus) return fail(o.runStatus, 'CareerAllowanceExhausted');
      return send({ id: 'run-1' }, 201);
    }
    if (path === '/api/career/runs/run-1')
      return send({
        id: 'run-1',
        task: 'professional_summary',
        status: 'completed',
        createdAt: now,
        updatedAt: now,
        completedAt: now,
        steps: [],
        question: null,
        proposalId: null,
        materialId: 'sum-1',
        profileChanged: false,
        errorCode: null,
        allowance: { used: 0, reserved: 0, limit: 10, periodStart: now },
      });
    if (path === '/api/career/materials/sum-1' && req.method() === 'GET') return send(summary());
    if (path === '/api/career/materials/sum-1/versions')
      return send({
        versions: [{ number: 1, author: 'agent', createdAt: '2026-10-04T09:00:00Z' }],
        total: 1,
      });
    if (path === '/api/career/materials/sum-1/exports' && req.method() === 'POST') {
      calls.exports.push(req.postDataJSON());
      if (o.exportStatus) return fail(o.exportStatus, o.exportCode);
      const body = req.postDataJSON();
      const isPdf = body.format === 'pdf';
      return send(
        {
          id: isPdf ? 'e-pdf' : 'e-docx',
          format: body.format,
          version: 1,
          includesPhoto: !!body.includePhoto,
          fileName: isPdf ? 'summary.pdf' : 'summary.docx',
          expiresAt: new Date(Date.now() + 23 * 3600_000).toISOString(),
          downloadUrl: '/api/career/exports/x',
        },
        201
      );
    }
    if (path === '/api/career/materials/sum-1/exports' && req.method() === 'GET')
      return send({
        exports: [
          {
            id: 'e-old',
            format: 'pdf',
            version: 1,
            includesPhoto: false,
            fileName: 'old.pdf',
            expiresAt: new Date(Date.now() + 5 * 3600_000).toISOString(),
            downloadUrl: '/api/career/exports/e-old',
          },
        ],
      });
    if (path.startsWith('/api/career/exports/')) {
      calls.downloads.push(path.split('/').pop()!);
      if (o.downloadStatus) return fail(o.downloadStatus, 'CareerExportExpired');
      return route.fulfill({
        status: 200,
        contentType: path.endsWith('docx') ? 'application/octet-stream' : 'application/pdf',
        body: 'file-bytes',
      });
    }
    return send([]);
  });
  return calls;
}
async function open(page: Page, url: string, heading: string) {
  await page.goto(url);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible();
}
const editor = (page: Page) =>
  open(
    page,
    '/app/career/summary-draft?e2eAuthBypass=1&material=sum-1',
    'Your professional summary'
  );
const overflow = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

test('materials page lists both kinds and no longer shows the placeholder', async ({ page }) => {
  await mock(page);
  await open(page, '/app/career/materials?e2eAuthBypass=1', 'Career materials');
  await expect(page.getByText('arrive in a later release')).toHaveCount(0);
  const resume = page.locator('[data-material="res-1"]');
  await expect(resume).toContainText('Targeted resume');
  await expect(resume).toContainText('Resume');
  await expect(resume).toContainText('Out of date');
  await expect(resume).toContainText('5 October 2026');
  await expect(page.locator('[data-material="sum-1"]')).toContainText('Professional summary');
  await expect(page.getByRole('link', { name: 'Draft my resume' })).toHaveAttribute(
    'href',
    '/app/career/resume'
  );
  await expect(page.locator('body')).not.toContainText(/\d{4}-\d{2}-\d{2}T/);
});

test('draft my summary runs the task and opens the editor with counters and provenance', async ({
  page,
}) => {
  const calls = await mock(page);
  await open(page, '/app/career/materials?e2eAuthBypass=1', 'Career materials');
  await page.getByRole('button', { name: 'Draft my summary' }).click();
  await expect(page).toHaveURL(/summary-draft.*material=sum-1/);
  expect(calls.runs[0]).toEqual({ task: 'professional_summary' });
  await expect(page.getByRole('heading', { name: 'Your professional summary' })).toBeVisible();
  const short = page.locator('[data-line="s1"]');
  await expect(short.locator('[data-counter]')).toHaveText('46 of 300 characters');
  await expect(page.locator('[data-line="s2"] [data-counter]')).toContainText('of 1200 characters');
  await expect(short.locator('[data-origin]')).toHaveText(
    'Based on: Worked as a developer for 5 years'
  );
  await expect(page.locator('[data-save-status]')).toBeAttached();
  await expect(page.getByRole('heading', { name: 'Version history' })).toBeVisible();
  await expect(page.getByText('Nothing is posted for you')).toBeVisible();
  await expect(page.getByRole('button', { name: /publish|share|linkedin/i })).toHaveCount(0);
  await expect(page.getByRole('link', { name: /publish|share|linkedin/i })).toHaveCount(0);
});

test('quota exhausted on drafting still explains and keeps files available', async ({ page }) => {
  const calls = await mock(page, { runStatus: 429 });
  await open(page, '/app/career/materials?e2eAuthBypass=1', 'Career materials');
  await page.getByRole('button', { name: 'Draft my summary' }).click();
  await expect(page.locator('[data-materials-error]')).toContainText('drafting allowance');
  expect(calls.runs).toHaveLength(1);
  await editor(page);
  await page.getByRole('button', { name: 'Download', exact: true }).click();
  await expect(page.locator('[data-export-status]')).toContainText('downloaded');
});

test('export PDF and Word send the right body and download a file', async ({ page }) => {
  const calls = await mock(page);
  await editor(page);
  await expect(
    page.getByText('Exports are free and do not use your drafting allowance.')
  ).toBeVisible();
  await expect(
    page.getByText('we do not promise how applicant tracking systems read it')
  ).toBeVisible();
  await expect(page.locator('[data-export="e-old"]')).toContainText(
    'PDF · Version 1 · Link works for about'
  );
  const pdf = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download', exact: true }).click();
  expect((await pdf).suggestedFilename()).toBe('summary.pdf');
  expect(calls.exports[0]).toEqual({ format: 'pdf', includePhoto: false });
  await page.getByLabel('Word').check();
  const docx = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download', exact: true }).click();
  expect((await docx).suggestedFilename()).toBe('summary.docx');
  expect(calls.exports[1]).toEqual({ format: 'docx', includePhoto: false });
  expect(calls.downloads).toEqual(['e-pdf', 'e-docx']);
});

test('photo checkbox only shows for PDF with a selected photo', async ({ page }) => {
  const calls = await mock(page, { photoSelected: true });
  await editor(page);
  const box = page.getByLabel('Make a separate PDF with my photo');
  await expect(box).toBeVisible();
  await page.getByLabel('Word').check();
  await expect(box).toHaveCount(0);
  await page.getByLabel('PDF', { exact: true }).check();
  await box.check();
  await page.getByRole('button', { name: 'Download', exact: true }).click();
  await expect(page.locator('[data-export-status]')).toContainText('downloaded');
  expect(calls.exports[0]).toEqual({ format: 'pdf', includePhoto: true });
});

test('photo checkbox is absent without a selected photo', async ({ page }) => {
  await mock(page);
  await editor(page);
  await expect(page.getByLabel('Make a separate PDF with my photo')).toHaveCount(0);
});

test('an expired link says so and never shows a code', async ({ page }) => {
  await mock(page, { downloadStatus: 410 });
  await editor(page);
  await page.getByRole('button', { name: /Download again/ }).click();
  await expect(page.locator('[data-export-error]')).toContainText('expired');
  await expect(page.locator('[data-export-error]')).toContainText('Make a new file');
  await expect(page.locator('body')).not.toContainText(/Career[A-Z][A-Za-z]+/);
});

test('an expired export on create also says the link expired', async ({ page }) => {
  await mock(page, { exportStatus: 410, exportCode: 'CareerExportExpired' });
  await editor(page);
  await page.getByRole('button', { name: 'Download', exact: true }).click();
  await expect(page.locator('[data-export-error]')).toContainText('expired');
  await expect(page.locator('body')).not.toContainText('CareerExportExpired');
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
    test(`materials list: 0 axe violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mock(page, { photoSelected: true });
      await open(page, '/app/career/materials?e2eAuthBypass=1', 'Career materials');
      await expect(page.locator('[data-material="sum-1"]')).toBeVisible();
      await page.addScriptTag({ path: AXE_PATH });
      expect(await scan(page)).toEqual([]);
      if (width === 320) expect(await overflow(page)).toBeLessThanOrEqual(0);
    });
    test(`summary editor and exports: 0 axe violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mock(page, { photoSelected: true });
      await editor(page);
      await expect(page.locator('[data-export="e-old"]')).toBeVisible();
      await page.addScriptTag({ path: AXE_PATH });
      expect(await scan(page)).toEqual([]);
      if (width === 320) expect(await overflow(page)).toBeLessThanOrEqual(0);
    });
  }
});
