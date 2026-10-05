import { test, expect, Page } from '@playwright/test';

type Mode = 'ready' | 'unreadable' | 'unsupported' | 'scanner';

const items = [
  {
    id: 'item-title',
    field: 'currentTitle',
    value: 'Senior Operations Lead',
    currentValue: 'Operations lead',
    page: 1,
    section: 'Experience',
    excerpt: 'Senior Operations Lead, Regional Health Services, 2021-present',
    flags: ['conflict'],
  },
  {
    id: 'item-skill',
    field: 'skills',
    value: 'Process improvement',
    currentValue: null,
    page: 2,
    section: 'Skills',
    excerpt: 'Process improvement',
    flags: [],
  },
  {
    id: 'item-highlight',
    field: 'highlights',
    value: 'Improved things',
    currentValue: null,
    page: 2,
    section: 'Experience',
    excerpt: 'Improved things',
    flags: ['ambiguous'],
  },
];

function fakeBackend(page: Page) {
  let mode: Mode = 'ready';
  let staleNext = false;
  let resumes: any[] = [];
  let profile: any = {
    id: 'profile-id',
    version: 1,
    etag: '"profile-v1"',
    facts: { currentTitle: 'Operations lead', skills: [], highlights: [] },
    provenance: { source: 'manual', confirmedAt: new Date().toISOString() },
  };
  let uploadBody = '';
  let acceptedIds: string[] = [];
  const send = (route: any, data: unknown, status = 200, etag?: string) =>
    route.fulfill({
      status,
      headers: etag ? { ETag: etag } : {},
      json: { success: true, data, error: null },
    });
  const fail = (route: any, status: number, code: string, extra: object = {}) =>
    route.fulfill({
      status,
      json: { success: false, error: { code, message: code, ...extra } },
    });
  const proposal = (source: string) => ({
    id: 'proposal-1',
    source,
    resumeId: source === 'resume' ? 'resume-1' : null,
    baseProfileVersion: 1,
    status: 'pending',
    isStale: false,
    items,
    createdAt: new Date().toISOString(),
  });
  page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    const method = route.request().method();
    if (url === '/api/config/client') return send(route, { features: { careerWorkspace: true } });
    if (url.startsWith('/api/career/')) {
      const path = url.slice('/api/career/'.length);
      if (path === 'profile' && method === 'GET') return send(route, profile, 200, profile.etag);
      if (path === 'goals') return fail(route, 404, 'CareerGoalNotFound');
      if (path === 'profile/versions') return send(route, []);
      if (path === 'resumes' && method === 'GET') return send(route, resumes);
      if (path === 'resumes' && method === 'POST') {
        uploadBody = route.request().postData() ?? '';
        if (mode === 'unsupported')
          return fail(route, 415, 'CareerResumeUnsupported', { detail: 'encrypted PDF' });
        if (mode === 'scanner')
          return fail(route, 503, 'CareerScannerUnavailable', { retryAfterSeconds: 60 });
        const doc = {
          id: 'resume-1',
          fileName: 'resume.pdf',
          format: 'pdf',
          sizeBytes: 2048,
          pageCount: 2,
          state: mode,
          failureCode: null,
          proposalId: mode === 'ready' ? 'proposal-1' : null,
          uploadedAt: new Date().toISOString(),
          expiresAt: new Date(Date.now() + 30 * 86400000).toISOString(),
        };
        resumes = [doc];
        return send(route, doc, 201);
      }
      if (path === 'resumes/resume-1' && method === 'DELETE') {
        resumes = [];
        return route.fulfill({ status: 204 });
      }
      if (path === 'profile/proposals' && method === 'POST')
        return send(route, proposal('pasted'), 201);
      if (path === 'profile/proposals/proposal-1' && method === 'GET')
        return send(route, proposal('resume'));
      if (path === 'profile/proposals/proposal-1/dismiss')
        return send(route, { ...proposal('resume'), status: 'dismissed' });
      if (path === 'profile/proposals/proposal-1/accept') {
        if (staleNext) return fail(route, 412, 'CareerVersionConflict');
        if (route.request().headers()['if-match'] !== profile.etag)
          return fail(route, 412, 'CareerVersionConflict');
        acceptedIds = route.request().postDataJSON().itemIds;
        profile = {
          ...profile,
          version: 2,
          etag: '"profile-v2"',
          facts: {
            currentTitle: 'Senior Operations Lead',
            skills: ['Process improvement'],
            highlights: [],
          },
          provenance: {
            source: 'resume',
            sourceProposalId: 'proposal-1',
            confirmedAt: new Date().toISOString(),
          },
        };
        return send(route, profile, 200, profile.etag);
      }
      return fail(route, 404, 'NotFound');
    }
    const responses: Record<string, unknown> = {
      '/auth/account-status': { emailConfirmed: true },
      '/auth/user-roles': [],
      '/profile': { firstName: 'Test', lastName: 'User' },
      '/credit/status': { credits: 5 },
      '/style': [],
      '/profilephotoworkflow/packages': [],
      '/profilephotoworkflow/entitlements': [],
      '/profilephotoworkflow/export-options': [],
    };
    const key = Object.keys(responses).find(k => url.endsWith(k));
    return send(route, key ? responses[key] : {});
  });
  return {
    mode: (value: Mode) => (mode = value),
    stale: () => (staleNext = true),
    uploadBody: () => uploadBody,
    acceptedIds: () => acceptedIds,
  };
}

async function open(page: Page) {
  await page.goto('/app/career/import?e2eAuthBypass=1');
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Import from a resume' })).toBeVisible();
}
const pdf = { name: 'resume.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4 x') };
async function upload(page: Page) {
  await page.getByLabel('I understand and agree').check();
  await page.getByLabel(/Resume file/).setInputFiles(pdf);
  await page.getByRole('button', { name: 'Upload and read resume' }).click();
}

test('upload requires consent', async ({ page }) => {
  fakeBackend(page);
  await open(page);
  await expect(page.getByText(/Your file is stored privately/)).toBeVisible();
  await page.getByLabel(/Resume file/).setInputFiles(pdf);
  await page.getByRole('button', { name: 'Upload and read resume' }).click();
  await expect(page.locator('[role=alert]')).toContainText(
    'Confirm that you agree before uploading.'
  );
  await expect(page.getByLabel('I understand and agree')).toHaveAttribute('aria-invalid', 'true');
});

test('client pre-check rejects wrong type and oversize files', async ({ page }) => {
  fakeBackend(page);
  await open(page);
  const input = page.getByLabel(/Resume file/);
  await input.setInputFiles({
    name: 'resume.doc',
    mimeType: 'application/msword',
    buffer: Buffer.from('x'),
  });
  await expect(page.locator('#import-file-error')).toContainText('Choose a PDF or DOCX file.');
  await input.setInputFiles({
    name: 'big.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.alloc(10 * 1024 * 1024 + 1),
  });
  await expect(page.locator('#import-file-error')).toContainText('up to 10 MB and 25 pages');
});

test('upload PDF, review, accept 2 of 3 and see provenance', async ({ page }) => {
  const backend = fakeBackend(page);
  await open(page);
  await upload(page);
  await expect(page.getByRole('heading', { name: 'Review suggestions' })).toBeFocused();
  expect(backend.uploadBody()).toContain('name="consent"');
  expect(backend.uploadBody()).toContain('name="file"');
  for (const name of ['Senior Operations Lead', 'Process improvement', 'Improved things'])
    await expect(page.getByLabel(name)).not.toBeChecked();
  await expect(page.getByText('Dates conflict - check this')).toBeVisible();
  await expect(page.getByText('Vague - add specifics')).toBeVisible();
  await expect(page.getByText('Current: Operations lead')).toBeVisible();
  await expect(page.getByText(/Page 1 · Experience · "Senior Operations Lead/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Accept selected (0)' })).toBeDisabled();
  await expect(
    page.getByText('Only checked items are added. You can edit anything afterwards.')
  ).toBeVisible();
  await page.getByLabel('Senior Operations Lead').check();
  await page.getByLabel('Process improvement').check();
  await page.getByRole('button', { name: 'Accept selected (2)' }).click();
  await expect(page).toHaveURL(/\/app\/career\/profile/);
  await expect(page.getByText('Profile updated from your resume')).toBeVisible();
  await expect(page.getByText(/From your resume · confirmed/)).toBeVisible();
  expect(backend.acceptedIds()).toEqual(['item-title', 'item-skill']);
});

test('unreadable file offers the paste fallback', async ({ page }) => {
  const backend = fakeBackend(page);
  backend.mode('unreadable');
  await open(page);
  await upload(page);
  await expect(page.getByRole('heading', { name: 'We could not read this file' })).toBeFocused();
  await expect(
    page.getByText(
      'We could not read text from this file (it may be a scan). Paste your resume text instead.'
    )
  ).toBeVisible();
  await page.getByRole('button', { name: 'Get suggestions from pasted text' }).click();
  await expect(page.locator('[role=alert]')).toContainText('Paste some resume text first.');
  await page.getByLabel('Resume text').fill('Jane Doe\nSenior Operations Lead');
  await page.getByRole('button', { name: 'Get suggestions from pasted text' }).click();
  await expect(page.getByRole('heading', { name: 'Review suggestions' })).toBeVisible();
  await expect(page.getByText('from the text you pasted')).toBeVisible();
});

test('unsupported file shows a specific message', async ({ page }) => {
  const backend = fakeBackend(page);
  backend.mode('unsupported');
  await open(page);
  await upload(page);
  await expect(page.locator('[role=alert]')).toContainText('Choose a PDF or DOCX file');
  await expect(page.locator('[role=alert]')).toContainText('encrypted PDF');
  await expect(page.locator('[role=alert]')).toBeFocused();
});

test('scanner outage shows retry-after', async ({ page }) => {
  const backend = fakeBackend(page);
  backend.mode('scanner');
  await open(page);
  await upload(page);
  await expect(page.locator('[role=alert]')).toContainText('Try again in about 60 seconds');
});

test('stale accept keeps the review and explains', async ({ page }) => {
  const backend = fakeBackend(page);
  await open(page);
  await upload(page);
  await page.getByLabel('Process improvement').check();
  backend.stale();
  await page.getByRole('button', { name: 'Accept selected (1)' }).click();
  await expect(page.locator('[role=alert]')).toContainText(
    'Your profile changed since these suggestions were made. Reload your profile and review again.'
  );
  await expect(page.getByLabel('Process improvement')).toBeChecked();
});

test('removing a file asks for confirmation', async ({ page }) => {
  fakeBackend(page);
  await open(page);
  await upload(page);
  await page.getByRole('button', { name: 'Dismiss all' }).click();
  await expect(page.getByText('Deleted automatically on')).toBeVisible();
  await page.getByRole('button', { name: /^Remove resume\.pdf/ }).click();
  await page.getByRole('button', { name: 'Cancel' }).click();
  await expect(page.locator('strong', { hasText: 'resume.pdf' })).toBeVisible();
  await page.getByRole('button', { name: /^Remove resume\.pdf/ }).click();
  await page.getByRole('button', { name: /^Confirm remove/ }).click();
  await expect(page.getByText('resume.pdf was removed.')).toBeVisible();
  await expect(page.getByText('No files uploaded.')).toBeVisible();
});

test('import and review fit a 320px viewport', async ({ page }) => {
  fakeBackend(page);
  await page.setViewportSize({ width: 320, height: 700 });
  await open(page);
  const width = () => page.evaluate(() => document.documentElement.scrollWidth);
  expect(await width()).toBeLessThanOrEqual(320);
  await upload(page);
  await expect(page.getByRole('heading', { name: 'Review suggestions' })).toBeVisible();
  expect(await width()).toBeLessThanOrEqual(320);
});
