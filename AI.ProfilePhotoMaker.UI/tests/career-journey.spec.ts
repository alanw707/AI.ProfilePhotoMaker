import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const HOME = '/app/career?e2eAuthBypass=1';
const J = (over: Record<string, unknown> = {}) => ({
  profile: { version: 1, confirmed: true },
  goal: {
    version: 1,
    occupationCode: '15-1252.00',
    occupationTitle: 'Software Developers',
    location: 'Denver, CO',
  },
  nextAction: { key: 'none', route: '/app/career' },
  latestResult: null,
  activeRuns: [],
  stale: [],
  ...over,
});
const at = '2026-10-05T10:00:00Z';
const sparsePay = {
  id: 'pay-1',
  runId: 'r',
  status: 'complete',
  occupation: {
    code: '15-1252.00',
    title: 'Software Developers',
    publishedCode: '15-1252',
    mapping: 'exact',
  },
  location: { input: 'Denver, CO', resolution: 'metro', local: null },
  pinned: {
    profileVersion: 1,
    goalVersion: 1,
    oewsRelease: '2025-05',
    oewsSnapshotSha256: 's',
    projectionsRelease: '',
    ruleVersion: 'v',
    observationSourceId: null,
  },
  inputHash: 'abcdef0123456789',
  stale: false,
  staleReasons: [],
  blockedReasons: [],
  qualification: { personalizedAllowed: true, gates: [] },
  sections: [
    {
      key: 'benchmark',
      title: 'b',
      status: 'complete',
      reason: null,
      label: 'Benchmark',
      note: 'n',
      figures: [],
    },
    {
      key: 'personalized',
      title: 'p',
      status: 'insufficient_evidence',
      reason: 'insufficient_observations',
      interval: null,
      note: '',
      cohort: {
        included: 2,
        excluded: 0,
        employers: 1,
        largestEmployerShare: 1,
        concentrated: true,
        sensitive: false,
        exclusionReasons: {},
      },
    },
  ],
  sources: [],
  createdAt: at,
};

async function mock(page: Page, journeys: unknown[]) {
  const calls = { writes: [] as string[], idx: 0, journeyGets: 0 };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown) =>
      route.fulfill({ json: { success: true, isAuthenticated: true, data } });
    if (!['GET', 'HEAD'].includes(req.method())) calls.writes.push(`${req.method()} ${path}`);
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/journey') {
      calls.journeyGets++;
      return send(journeys[Math.min(calls.idx, journeys.length - 1)]);
    }
    if (path === '/api/career/goals')
      return send({ occupation: { code: '15-1252.00' }, goal: { targetRole: 'Developer' } });
    if (path === '/api/career/profile')
      return send({
        facts: { currentTitle: 'Developer', skills: [], highlights: [] },
        provenance: { source: 'manual', confirmedAt: at },
      });
    if (path.startsWith('/api/career/pay-analyses')) return send(sparsePay);
    return send([]);
  });
  return calls;
}
async function open(page: Page, url = HOME) {
  await page.goto(url);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { name: 'Career workspace' })).toBeVisible();
}

const steps: [string, Record<string, unknown>, string, RegExp][] = [
  [
    'create_profile',
    { profile: null, goal: null },
    '/app/career/setup',
    /Set up your career workspace/,
  ],
  ['set_goal', { goal: null }, '/app/career/setup', /Set up your career workspace/],
  ['build_brief', {}, '/app/career/market', /Career market brief/],
  ['analyze_pay', {}, '/app/career/pay', /Comparable pay analysis/],
  ['build_roadmap', {}, '/app/career/roadmap', /Your career roadmap/],
  ['draft_resume', {}, '/app/career/resume', /Your targeted resume/],
  ['export_material', {}, '/app/career/materials', /Career materials/],
];

test('Next step advances through the journey and each target renders', async ({ page }) => {
  const calls = await mock(
    page,
    steps.map(([key, over]) => J({ ...over, nextAction: { key, route: '/ignored' } }))
  );
  for (let i = 0; i < steps.length; i++) {
    calls.idx = i;
    await open(page);
    const [, , path, heading] = steps[i];
    const next = page.locator('[data-next]');
    await expect(next).toHaveCount(1);
    await next.click();
    await expect(page).toHaveURL(new RegExp(`${path}(\\?|$)`));
    await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible();
  }
  expect(calls.writes).toEqual([]);
});

test('empty state prompts for a goal; unknown action renders nothing', async ({ page }) => {
  const calls = await mock(page, [J({ goal: null, nextAction: { key: 'rm -rf', route: '/x' } })]);
  await open(page);
  await expect(page.getByText('You have not set a goal yet')).toBeVisible();
  await expect(page.locator('[data-next]')).toHaveCount(0);
  expect(calls.writes).toEqual([]);
});

test('latest result link carries its id and sparse pay shows page wording', async ({ page }) => {
  await mock(page, [
    J({
      nextAction: { key: 'build_roadmap' },
      latestResult: { kind: 'pay_analysis', id: 'pay-1', version: 1, createdAt: at },
    }),
  ]);
  await open(page);
  const goal = page.locator('[data-goal]');
  await expect(goal).toContainText('Software Developers');
  await expect(goal.locator('.brief__place')).toHaveText('Denver, CO');
  await expect(page.locator('body')).not.toContainText('15-1252');
  await expect(page.locator('body')).not.toContainText('2026-10-05T');
  const link = page.locator('[data-latest]');
  await expect(link).toHaveAttribute('href', '/app/career/pay?analysis=pay-1');
  await link.click();
  await expect(page).toHaveURL(/analysis=pay-1/);
  await expect(page.locator('[data-section="personalized"]')).toContainText(
    'Insufficient evidence'
  );
});

test('active work: still working and try again; stale notice', async ({ page }) => {
  await mock(page, [
    J({
      activeRuns: [
        { id: 'a', task: 'market_brief', status: 'working', startedAt: at },
        { id: 'b', task: 'roadmap', status: 'failed', startedAt: at },
      ],
      stale: [{ kind: 'roadmap', id: 'r1', reasons: ['profile_changed'] }],
    }),
  ]);
  await open(page);
  const runs = page.locator('[data-run]');
  await expect(runs).toHaveText(['Still working', 'Try again']);
  await expect(runs.first()).toHaveAttribute('href', '/app/career/market?run=a');
  await expect(runs.last()).toHaveAttribute('href', '/app/career/roadmap?run=b');
  await expect(page.locator('[data-stale-link]')).toHaveAttribute(
    'href',
    '/app/career/roadmap?roadmap=r1'
  );
  await expect(page.locator('[data-stale]')).toContainText('may be out of date');
  await expect(page.locator('body')).not.toContainText('profile_changed');
});

test('Try again carries the run id; page shows the failed run; home posts nothing', async ({
  page,
}) => {
  const calls = await mock(page, [
    J({ activeRuns: [{ id: 'b', task: 'roadmap', status: 'failed', startedAt: at }] }),
  ]);
  await page.route('**/api/career/runs/b', route =>
    route.fulfill({
      json: {
        success: true,
        isAuthenticated: true,
        data: {
          id: 'b',
          task: 'roadmap',
          status: 'failed',
          createdAt: at,
          updatedAt: at,
          completedAt: at,
          steps: [],
          question: null,
          errorCode: 'x',
          profileChanged: false,
        },
      },
    })
  );
  await open(page);
  expect(calls.writes).toEqual([]);
  await page.locator('[data-run]').click();
  await expect(page).toHaveURL(/roadmap\?run=b/);
  await expect(page.getByText('We could not build your roadmap.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible();
  expect(calls.writes).toEqual([]);
});

test('malicious context is text; no dialog, no writes', async ({ page }) => {
  let dialogs = 0;
  page.on('dialog', d => {
    dialogs++;
    d.dismiss();
  });
  const calls = await mock(page, [
    J({
      goal: {
        version: 1,
        occupationTitle: '<img src=x onerror=alert(1)>',
        location: 'javascript:alert(1)',
      },
      nextAction: { key: 'javascript:alert(1)', route: 'javascript:alert(1)' },
    }),
  ]);
  await open(page, `${HOME}&next=javascript:alert(1)`);
  await expect(page.locator('[data-goal]')).toContainText('<img src=x onerror=alert(1)>');
  await expect(page.locator('img[src="x"]')).toHaveCount(0);
  await expect(page.locator('[data-next]')).toHaveCount(0);
  await expect(page.locator('a[href^="javascript"]')).toHaveCount(0);
  await page.waitForTimeout(500);
  expect(dialogs).toBe(0);
  expect(calls.writes).toEqual([]);
});

test('every career page link resolves', async ({ page }) => {
  await mock(page, [J()]);
  await open(page);
  const hrefs = await page
    .locator('nav[aria-label="Career steps"] a')
    .evaluateAll(as => as.map(a => a.getAttribute('href') as string));
  // workspace home + 7 journey steps + 7 more pages, all distinct
  expect(new Set(hrefs).size).toBe(15);
  for (const href of hrefs) {
    await page.goto(`${href}?e2eAuthBypass=1`);
    await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible();
    await expect(page.locator('body')).not.toContainText(/page not found|404/i);
    await expect(page.locator('body')).not.toContainText(/later release|coming soon/i);
  }
});

for (const [w, h] of [
  [1280, 800],
  [390, 844],
  [320, 640],
]) {
  test(`axe and no overflow at ${w}`, async ({ page }) => {
    test.skip(!existsSync(AXE_PATH), 'axe-core not installed');
    await page.setViewportSize({ width: w, height: h });
    await mock(page, [
      J({
        nextAction: { key: 'analyze_pay' },
        latestResult: { kind: 'roadmap', id: 'r1', createdAt: at },
        activeRuns: [{ id: 'b', task: 'roadmap', status: 'failed', startedAt: at }],
        stale: [{ kind: 'roadmap', id: 'r1' }],
      }),
    ]);
    await open(page);
    await page.addScriptTag({ path: AXE_PATH });
    const result = await page.evaluate(() =>
      (window as any).axe.run(document, {
        runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      })
    );
    expect(result.violations.map((v: any) => v.id)).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
      true
    );
  });
}
