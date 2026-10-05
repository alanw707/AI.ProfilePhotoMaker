import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const createdAt = '2026-10-05T10:00:00Z';
const base = {
  id: 'rm-1',
  version: 2,
  status: 'accepted',
  selectedOption: 'closest_fit',
  pinned: { profileVersion: 1, goalVersion: 1, occupationCode: '15-1252.00' },
  stale: false,
  staleReasons: [],
  weeklyEffortHours: 6,
  options: [
    {
      key: 'closest_fit',
      occupationCode: '15-1252.00',
      title: 'Software Developers',
      rationale: [],
      assumptions: [],
      missingEvidence: [],
      timelineNote: 'A scenario, not a promise.',
      thisWeek: [],
      milestones: [],
    },
  ],
  omittedOptions: [],
  lowTimeNote: null,
};
const task = (o: Record<string, unknown>) => ({
  origin: 'generated',
  milestoneDay: 0,
  dependsOn: [],
  status: 'not_started',
  effectiveStatus: 'not_started',
  blockedBy: [],
  effortHours: 2,
  outputNote: null,
  linkedMaterialId: null,
  linkedMaterialMissing: false,
  help: 'Start small and write down what you find.',
  etag: 'task-v1',
  ...o,
});
const initial = () => [
  task({ taskId: 't1', title: 'List three projects', help: 'Pick work you are proud of.' }),
  task({
    taskId: 't2',
    title: 'Write a project summary',
    dependsOn: ['t1'],
    status: 'in_progress',
    effectiveStatus: 'blocked',
    blockedBy: ['List three projects'],
    linkedMaterialId: 'mat-1',
    linkedMaterialMissing: true,
  }),
  task({ taskId: 't3', title: 'Share it with a peer', milestoneDay: 30, dependsOn: ['t2'] }),
];
const diff = (changes: unknown[]) => ({
  id: 'rp-1',
  baseVersion: 2,
  changes,
  preserved: [
    { taskId: 't1', title: 'List three projects', reason: 'done' },
    { taskId: 't2', title: 'Write a project summary', reason: 'has_output' },
  ],
});
const changes = [
  {
    id: 'c1',
    kind: 'changed',
    taskId: 't3',
    title: 'Share it with a peer',
    fields: [
      { field: 'milestoneDay', before: 30, after: 60 },
      { field: 'dependencies', before: ['t2'], after: ['t1'] },
    ],
    rationale: 'Market demand moved this later.',
  },
  {
    id: 'c2',
    kind: 'added',
    taskId: 'n1',
    title: 'Practice a mock interview',
    fields: [{ field: 'effort', before: null, after: 2 }],
    rationale: 'Employers in your area ask for it.',
  },
];

interface Opts {
  put?: number;
  cycle?: boolean;
  replan?: unknown;
  applyStatus?: number;
}
async function mock(page: Page, o: Opts = {}) {
  let tasks = initial();
  const calls = {
    put: [] as { body: any; ifMatch?: string; taskId: string }[],
    add: [] as any[],
    apply: [] as any[],
    reject: 0,
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
    if (path === '/api/career/goals')
      return send({ id: 'g1', etag: '"g1"', occupation: { code: '15-1252.00' }, goal: {} });
    if (path === '/api/career/profile') return send({ facts: { skills: [], highlights: [] } });
    if (path === '/api/career/roadmaps')
      return send({
        roadmaps: [
          { id: 'rm-1', version: 2, status: 'accepted', stale: false, optionCount: 1, createdAt },
        ],
      });
    if (path === '/api/career/roadmaps/rm-1') return send(base);
    if (path === '/api/career/roadmaps/rm-1/progress')
      return send({ roadmapId: 'rm-1', version: 2, tasks });
    const put = path.match(/^\/api\/career\/roadmaps\/rm-1\/progress\/(\w+)$/);
    if (put) {
      const body = req.postDataJSON();
      calls.put.push({ body, ifMatch: req.headers()['if-match'], taskId: put[1] });
      if (o.put) {
        tasks = tasks.map(t =>
          t.taskId === put[1] ? { ...t, outputNote: 'Changed elsewhere', etag: 'task-v9' } : t
        );
        return fail(o.put);
      }
      const updated = {
        ...tasks.find(t => t.taskId === put[1])!,
        ...body,
        outputNote: body.outputNote ?? null,
        etag: 'task-v2',
      };
      tasks = tasks.map(t => (t.taskId === put[1] ? updated : t));
      return send(updated);
    }
    if (path === '/api/career/roadmaps/rm-1/tasks') {
      calls.add.push(req.postDataJSON());
      if (o.cycle) return fail(409, 'CareerRoadmapCycle');
      return send(task({ taskId: 'h1', title: 'Mine', origin: 'human' }), 201);
    }
    if (path === '/api/career/roadmaps/rm-1/replan') return send(o.replan ?? diff(changes), 201);
    if (path === '/api/career/replans/rp-1/apply') {
      calls.apply.push(req.postDataJSON());
      if (o.applyStatus) return fail(o.applyStatus, 'CareerReplanStale');
      return send({ ...base, version: 3 });
    }
    if (path === '/api/career/replans/rp-1/reject') {
      calls.reject++;
      return send({});
    }
    return send([]);
  });
  return calls;
}
async function open(page: Page) {
  await page.goto('/app/career/roadmap?e2eAuthBypass=1&roadmap=rm-1');
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.locator('[data-track="t1"]')).toBeVisible();
}
const overflow = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

test('groups tasks, saves a status edit with If-Match, and says pay is unaffected', async ({
  page,
}) => {
  const calls = await mock(page);
  await open(page);
  const tracking = page.locator('[data-tracking]');
  await expect(tracking.getByRole('heading', { name: 'This week' })).toBeVisible();
  await expect(tracking.getByRole('heading', { name: 'By day 30' })).toBeVisible();
  await expect(tracking).toContainText('never changes your pay estimates');
  await page.getByLabel('Status for List three projects').selectOption('done');
  await page.getByLabel('Hours spent on List three projects').fill('3');
  await page.getByLabel('What you produced for List three projects').fill('Wrote a list');
  await page.getByRole('button', { name: 'Save changes for List three projects' }).click();
  await expect(page.locator('[data-saved]')).toContainText('Saved "List three projects".');
  expect(calls.put).toEqual([
    {
      taskId: 't1',
      ifMatch: 'task-v1',
      body: { status: 'done', effortHours: 3, outputNote: 'Wrote a list' },
    },
  ]);
});

test('blocked task names what it waits on; deleted material is explained; help shows', async ({
  page,
}) => {
  await mock(page);
  await open(page);
  const t2 = page.locator('[data-track="t2"]');
  await expect(t2.locator('[data-blocked]')).toHaveText('Waiting on: List three projects');
  await expect(t2.locator('[data-material-missing]')).toContainText('This material was deleted.');
  await page
    .locator('[data-track="t1"]')
    .getByRole('button', { name: /Help for/ })
    .click();
  await expect(page.locator('[data-help]')).toContainText('Pick work you are proud of.');
});

test('simultaneous edit (412) reloads the task but keeps the draft', async ({ page }) => {
  await mock(page, { put: 412 });
  await open(page);
  const note = page.getByLabel('What you produced for List three projects');
  await note.fill('My draft');
  await page.getByRole('button', { name: 'Save changes for List three projects' }).click();
  await expect(page.locator('[data-track="t1"] [data-task-error]')).toContainText(
    'This task changed elsewhere'
  );
  await expect(note).toHaveValue('My draft');
});

test('adding my own task and a cycle error in words', async ({ page }) => {
  const calls = await mock(page, { cycle: true });
  await open(page);
  await page.getByLabel('Task title').fill('Mine');
  await page.getByLabel('Hours', { exact: true }).fill('2');
  await page.getByLabel('When').selectOption('60');
  await page.locator('[data-add-task]').getByLabel('List three projects').check();
  await page.getByRole('button', { name: 'Add task' }).click();
  await expect(page.locator('[data-add-error]')).toContainText('depend on each other in a loop');
  expect(calls.add).toEqual([
    { title: 'Mine', effortHours: 2, milestoneDay: 60, dependsOn: ['t1'] },
  ]);
  await expect(page.locator('body')).not.toContainText('CareerRoadmapCycle');
});

test('replan diff in words; partial apply sends only checked ids', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  await page.getByRole('button', { name: 'Check for a new plan' }).click();
  const c1 = page.locator('[data-change="c1"]');
  await expect(c1).toContainText('Due: Day 30 → Day 60');
  await expect(c1).toContainText('Do this after: Write a project summary → List three projects');
  await expect(c1).toContainText('Why: Market demand moved this later.');
  await expect(page.locator('[data-change="c2"]')).toContainText('Time: nothing → 2 hours');
  await expect(page.locator('[data-preserved]')).toContainText(
    'List three projects: You already finished it.'
  );
  await expect(c1.getByRole('checkbox')).not.toBeChecked();
  await expect(page.locator('[data-change="c2"]').getByRole('checkbox')).not.toBeChecked();
  await c1.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Apply selected changes' }).click();
  await expect(page.locator('[data-replan-notice]')).toContainText('applied');
  expect(calls.apply).toEqual([{ acceptedChangeIds: ['c1'] }]);
});

test('stale replan asks to check again', async ({ page }) => {
  await mock(page, { applyStatus: 409 });
  await open(page);
  await page.getByRole('button', { name: 'Check for a new plan' }).click();
  await page.locator('[data-change="c1"]').getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Apply selected changes' }).click();
  await expect(page.locator('[data-replan-error]')).toContainText('Check for a new plan again.');
});

test('keeping the current plan rejects the replan', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  await page.getByRole('button', { name: 'Check for a new plan' }).click();
  await page.getByRole('button', { name: 'Keep current plan' }).click();
  await expect(page.locator('[data-replan-notice]')).toContainText('kept as it is');
  expect(calls.reject).toBe(1);
  expect(calls.apply).toHaveLength(0);
});

test('an empty diff says the plan is current', async ({ page }) => {
  await mock(page, { replan: diff([]) });
  await open(page);
  await page.getByRole('button', { name: 'Check for a new plan' }).click();
  await expect(page.locator('[data-no-changes]')).toHaveText('Your plan is still current.');
});

test('selected task and unsaved draft survive a reload; no machine codes', async ({ page }) => {
  await mock(page);
  await open(page);
  await page
    .locator('[data-track="t1"]')
    .getByRole('button', { name: /Help for/ })
    .click();
  await page.getByLabel('What you produced for List three projects').fill('Unsaved words');
  await page.getByLabel('Status for List three projects').selectOption('in_progress');
  await page.reload();
  await expect(page.locator('[data-track="t1"]')).toBeVisible();
  await expect(page.locator('[data-help]')).toContainText('Pick work you are proud of.');
  await expect(page.getByLabel('What you produced for List three projects')).toHaveValue(
    'Unsaved words'
  );
  await expect(page.getByLabel('Status for List three projects')).toHaveValue('in_progress');
  await page.getByRole('button', { name: 'Check for a new plan' }).click();
  await expect(page.locator('[data-change="c1"]')).toBeVisible();
  const text = (await page.locator('[data-tracking]').innerText()).replace(/\s+/g, ' ');
  for (const code of [
    'not_started',
    'in_progress',
    'has_output',
    'milestoneDay',
    'dependsOn',
    'task-v1',
    'mat-1',
    '2026-10-05T',
    'CareerReplan',
  ])
    expect(text).not.toContain(code);
});

test.describe('accessibility', () => {
  test.skip(!existsSync(AXE_PATH), `axe-core not found at ${AXE_PATH}; set AXE_PATH`);
  for (const width of [1280, 390, 320]) {
    test(`0 axe WCAG 2.2 AA violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mock(page);
      await open(page);
      await page.getByRole('button', { name: 'Check for a new plan' }).click();
      await expect(page.locator('[data-change="c1"]')).toBeVisible();
      await page
        .locator('[data-track="t1"]')
        .getByRole('button', { name: /Help for/ })
        .click();
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
