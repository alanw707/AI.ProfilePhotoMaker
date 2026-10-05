import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import type { RoadmapDto, RoadmapOption } from '../src/app/services/career-profile.service';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const createdAt = '2026-10-05T10:00:00Z';
const labels = [
  'Read your career goal',
  'Read your market and pay evidence',
  'Compared possible paths',
  'Planned tasks around your available time',
  'Saved your roadmap',
];
const option = (key: RoadmapOption['key'], title: string): RoadmapOption => ({
  key,
  occupationCode: key === 'closest_fit' ? '15-1252.00' : '15-2051.00',
  title,
  rationale: [
    { text: 'Median annual wage is $135,980.', sourceId: 'oews', release: '2025-05' },
    { text: 'Employment is projected to grow 17%.', sourceId: 'projections', release: '2025-2035' },
  ],
  assumptions: ['You can spend your weekly hours on this.'],
  missingEvidence: ['No advertised pay from a qualified source yet.'],
  timelineNote: 'A scenario, not a promise.',
  thisWeek: [
    { id: 't1', title: 'List three projects you could show', effortHours: 2, dependsOn: [] },
    { id: 't2', title: 'Write one project summary', effortHours: 3, dependsOn: ['t1'] },
  ],
  milestones: [
    {
      day: 30,
      tasks: [
        { id: 't3', title: 'Share the summary with a peer', effortHours: 1, dependsOn: ['t2'] },
      ],
    },
    { day: 60, tasks: [] },
    { day: 90, tasks: [] },
  ],
});
const three: RoadmapDto = {
  id: 'rm-1',
  version: 1,
  status: 'proposed',
  selectedOption: null,
  pinned: {
    profileVersion: 3,
    goalVersion: 2,
    occupationCode: '15-1252.00',
    marketBriefId: null,
    payAnalysisId: null,
  },
  stale: false,
  staleReasons: [],
  weeklyEffortHours: 6,
  options: [
    option('closest_fit', 'Software Developers'),
    option('higher_ambition', 'Data Scientists'),
    option('steadier_transition', 'Database Administrators'),
  ],
  omittedOptions: [],
  lowTimeNote: null,
};
const one: RoadmapDto = {
  ...three,
  options: [three.options[0]],
  omittedOptions: [
    { key: 'higher_ambition', reason: 'no_supported_alternative' },
    { key: 'steadier_transition', reason: 'no_supported_alternative' },
  ],
};

interface Opts {
  roadmap?: RoadmapDto;
  stale?: boolean;
  lowTime?: boolean;
  acceptStatus?: number;
  cycle?: boolean;
}
async function mock(page: Page, o: Opts = {}) {
  let polls = 0;
  let current: RoadmapDto = {
    ...(o.roadmap ?? three),
    stale: o.stale ?? false,
    lowTimeNote: o.lowTime ? 'With under 2 hours a week, the plan keeps one task per week.' : null,
  };
  const calls = {
    accept: [] as { body: unknown; ifMatch: string | undefined }[],
    goals: 0,
    put: [] as unknown[],
    start: undefined as unknown,
  };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: status < 400, isAuthenticated: true, data } });
    const fail = (status: number, code?: string) =>
      route.fulfill({ status, json: { success: false, error: { code, message: code } } });
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/goals') {
      calls.goals++;
      return route.fulfill({
        headers: { ETag: `"goal-v${calls.goals}"` },
        json: {
          success: true,
          isAuthenticated: true,
          data: {
            id: 'g1',
            etag: `"goal-v${calls.goals}"`,
            occupation: { code: '15-1252.00' },
            goal: {},
          },
        },
      });
    }
    if (path === '/api/career/profile')
      return send({ facts: { currentTitle: 'Developer', skills: [], highlights: [] } });
    if (path === '/api/career/roadmaps')
      return send({
        roadmaps: [
          {
            id: 'rm-1',
            version: 1,
            status: current.status,
            stale: current.stale,
            optionCount: current.options.length,
            createdAt,
          },
        ],
      });
    if (path === '/api/career/roadmaps/rm-1/accept') {
      calls.accept.push({ body: req.postDataJSON(), ifMatch: req.headers()['if-match'] });
      if (o.acceptStatus) return fail(o.acceptStatus, 'CareerVersionConflict');
      current = {
        ...current,
        status: 'accepted',
        selectedOption: (req.postDataJSON() as { optionKey: RoadmapOption['key'] }).optionKey,
        goalUnchanged: true,
      };
      return send(current);
    }
    if (path === '/api/career/roadmaps/rm-1/tasks/t1') {
      const body = req.postDataJSON() as { effortHours: number };
      calls.put.push(body);
      if (o.cycle) return fail(409, 'CareerRoadmapCycle');
      current = {
        ...current,
        version: current.version + 1,
        options: current.options.map(op => ({
          ...op,
          thisWeek: op.thisWeek.map(t =>
            t.id === 't1' ? { ...t, effortHours: body.effortHours } : t
          ),
        })),
      };
      return send(current);
    }
    if (path === '/api/career/roadmaps/rm-1') return send(current);
    if (path === '/api/career/runs' && req.method() === 'POST') {
      calls.start = req.postDataJSON();
      return send({ id: 'run-1', status: 'queued', steps: [] });
    }
    if (path === '/api/career/runs/run-1') {
      polls++;
      const count = polls === 1 ? 0 : polls === 2 ? 2 : 5;
      return send({
        id: 'run-1',
        status: count === 5 ? 'completed' : 'working',
        updatedAt: createdAt,
        roadmapId: count === 5 ? 'rm-1' : null,
        steps: labels.slice(0, count).map((label, i) => ({ ordinal: i + 1, label })),
      });
    }
    return send([]);
  });
  return calls;
}
async function open(page: Page, query = '') {
  await page.goto(`/app/career/roadmap?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Your career roadmap' })).toBeVisible();
}
const overflow = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

test('run with real steps leads to a reload-safe roadmap with three options', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  await expect(page).toHaveTitle('Career Roadmap - AI Profile Photo Maker');
  await page.getByRole('button', { name: 'Build my roadmap' }).click();
  expect(calls.start).toEqual({ task: 'roadmap' });
  await expect(page).toHaveURL(/run=run-1/);
  await expect(page.getByText(labels[0])).toBeVisible({ timeout: 10000 });
  await expect(page).toHaveURL(/roadmap=rm-1/, { timeout: 15000 });
  await expect(page.locator('[data-option]')).toHaveCount(3);
  const first = page.locator('[data-option="closest_fit"]');
  await expect(first).toContainText('Median annual wage is $135,980.');
  await expect(first).toContainText(
    'Source: BLS Occupational Employment and Wage Statistics, May 2025'
  );
  await expect(first.locator('[data-assumptions]')).toContainText(
    'You can spend your weekly hours'
  );
  await expect(first.locator('[data-missing]')).toContainText('No advertised pay');
  await expect(first.locator('[data-scenario]')).toHaveText('A scenario, not a promise.');
  await expect(first.getByRole('heading', { name: 'This week' })).toBeVisible();
  await expect(first.getByRole('heading', { name: 'By day 30' })).toBeVisible();
  await expect(first.getByRole('heading', { name: 'By day 90' })).toBeVisible();
  await expect(first.locator('[data-task="t2"]')).toContainText('3 hours');
  await expect(first.locator('[data-task="t2"]')).toContainText(
    'Do this after: List three projects you could show.'
  );
  await expect(page.getByRole('group', { name: 'Choose a path' })).toBeVisible();
  await expect(page.getByText('Roadmaps are kept with your career data')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Recent roadmaps' })).toBeVisible();
  await page.reload();
  await expect(page.locator('[data-option]')).toHaveCount(3);
});

test('one option explains what was left out; low-time note shows', async ({ page }) => {
  await mock(page, { roadmap: one, lowTime: true });
  await open(page, '&roadmap=rm-1');
  await expect(page.locator('[data-option]')).toHaveCount(1);
  await expect(page.locator('[data-omitted]')).toContainText(
    'No other path is supported by the evidence yet.'
  );
  await expect(page.locator('[data-omitted]')).toContainText('Higher ambition');
  await expect(page.locator('[data-low-time]')).toContainText('under 2 hours a week');
});

test('accept needs a choice and a confirmation, then says the goal is unchanged', async ({
  page,
}) => {
  const calls = await mock(page);
  await open(page, '&roadmap=rm-1');
  await page.getByRole('button', { name: 'Accept this path' }).click();
  await expect(page.locator('[data-error]')).toContainText('Choose a path first.');
  expect(calls.accept).toHaveLength(0);
  await page.getByRole('radio', { name: /Data Scientists/ }).check();
  await page.getByRole('button', { name: 'Accept this path' }).click();
  await expect(page.getByRole('dialog')).toContainText('does not change your goal');
  expect(calls.accept).toHaveLength(0);
  await page.getByRole('button', { name: 'Cancel' }).click();
  expect(calls.accept).toHaveLength(0);
  await page.getByRole('button', { name: 'Accept this path' }).click();
  await page.getByRole('button', { name: 'Yes, accept this path' }).click();
  await expect(page.locator('[data-accepted]')).toContainText('Your goal itself was not changed.');
  await expect(page.getByRole('link', { name: 'Go to the occupation page' })).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
  expect(calls.accept).toHaveLength(1);
  expect(calls.accept[0].body).toEqual({ optionKey: 'higher_ambition' });
  expect(calls.accept[0].ifMatch).toMatch(/^"goal-v\d+"$/);
});

test('a changed goal (412) refetches it and asks to retry', async ({ page }) => {
  const calls = await mock(page, { acceptStatus: 412 });
  await open(page, '&roadmap=rm-1');
  await page.getByRole('radio', { name: /Software Developers/ }).check();
  await page.getByRole('button', { name: 'Accept this path' }).click();
  await page.getByRole('button', { name: 'Yes, accept this path' }).click();
  await expect(page.locator('[data-error]')).toContainText('Choose Accept this path to try again.');
  expect(calls.goals).toBeGreaterThanOrEqual(3);
  await expect(page.getByRole('button', { name: 'Accept this path' })).toBeVisible();
});

test('effort edits save through the endpoint and cycles are explained', async ({ page }) => {
  const calls = await mock(page);
  await open(page, '&roadmap=rm-1');
  const input = page
    .locator('[data-option="closest_fit"]')
    .getByLabel('Hours for List three projects you could show');
  await input.fill('4');
  await input.press('Enter');
  await expect(page.locator('[data-effort-saved]')).toContainText('Saved 4 hours');
  expect(calls.put).toEqual([{ effortHours: 4 }]);
  await input.fill('50');
  await input.press('Enter');
  await expect(page.getByText('Enter hours between 0.5 and 40.').first()).toBeVisible();
  expect(calls.put).toHaveLength(1);

  const cyc = await page.context().newPage();
  await mock(cyc, { cycle: true });
  await open(cyc, '&roadmap=rm-1');
  const field = cyc
    .locator('[data-option="closest_fit"]')
    .getByLabel('Hours for List three projects you could show');
  await field.fill('3');
  await field.press('Enter');
  await expect(cyc.getByText('depend on each other in a loop')).toBeVisible();
  await expect(cyc.locator('body')).not.toContainText('CareerRoadmapCycle');
});

test('stale banner, keyboard selection and no machine codes', async ({ page }) => {
  await mock(page, { stale: true });
  await open(page, '&roadmap=rm-1');
  await expect(page.locator('[data-stale]')).toContainText(
    'Your profile or goal changed after this roadmap.'
  );
  await page.getByRole('radio', { name: /Software Developers/ }).focus();
  await page.keyboard.press('Space');
  await expect(page.getByRole('radio', { name: /Software Developers/ })).toBeChecked();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('radio', { name: /Data Scientists/ })).toBeChecked();
  const text = (await page.locator('[data-roadmap]').innerText()).replace(/\s+/g, ' ');
  for (const code of [
    'closest_fit',
    'higher_ambition',
    'steadier_transition',
    'no_supported_alternative',
    'oews',
    '15-1252.00',
    '2026-10-05T',
    'proposed',
  ])
    expect(text).not.toContain(code);
  await expect(
    page.getByRole('button', { name: /Oct 5, 2026|October 5, 2026|2026-10-05/ })
  ).toHaveCount(0);
});

test('home links to the roadmap', async ({ page }) => {
  await mock(page);
  await page.goto('/app/career?e2eAuthBypass=1');
  await expect(page.getByRole('link', { name: 'Career roadmap' })).toHaveAttribute(
    'href',
    '/app/career/roadmap'
  );
});

test.describe('accessibility', () => {
  test.skip(!existsSync(AXE_PATH), `axe-core not found at ${AXE_PATH}; set AXE_PATH`);
  for (const width of [1280, 390, 320]) {
    test(`0 axe WCAG 2.2 AA violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mock(page, { stale: true, lowTime: true });
      await open(page, '&roadmap=rm-1');
      await expect(page.locator('[data-option]')).toHaveCount(3);
      await page.addScriptTag({ path: AXE_PATH });
      const run = () =>
        page.evaluate(async () => {
          const result = await (window as any).axe.run(document, {
            runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
          });
          return result.violations.map(
            (v: any) => `${v.id}: ${v.nodes.map((n: any) => n.target.join(' ')).join(' | ')}`
          );
        });
      expect(await run()).toEqual([]);
      await page.getByRole('radio', { name: /Software Developers/ }).check();
      await page.getByRole('button', { name: 'Accept this path' }).click();
      await expect(page.getByRole('dialog')).toBeVisible();
      expect(await run()).toEqual([]);
      await page.getByRole('button', { name: 'Cancel' }).click();
      if (width === 320) expect(await overflow(page)).toBeLessThanOrEqual(0);
    });
  }
});
