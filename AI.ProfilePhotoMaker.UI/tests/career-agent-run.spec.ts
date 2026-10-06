import { test, expect, Page } from '@playwright/test';

const RUN_ID = 'run-1';
const labels = [
  'Read your confirmed profile',
  'Read your career goal',
  'Drafted a summary',
  'Saved the draft for your review',
];
const allowance = { used: 1, reserved: 0, limit: 20, periodStart: '2026-10-01T00:00:00Z' };
const profile = {
  id: 'p1',
  version: 1,
  etag: '"p1"',
  facts: { currentTitle: 'Analyst', skills: [], highlights: [] },
  provenance: { source: 'manual', confirmedAt: '2026-10-01T00:00:00Z' },
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
};

type Scenario = 'normal' | 'question' | 'allowance' | 'profileRequired' | 'failed' | 'changed';

function runDto(status: string, stepCount: number, extra: Record<string, unknown> = {}) {
  return {
    id: RUN_ID,
    task: 'profile_summary',
    status,
    createdAt: '2026-10-04T10:00:00Z',
    updatedAt: '2026-10-04T10:00:00Z',
    completedAt: null,
    pinnedProfileVersion: 1,
    pinnedGoalVersion: 1,
    steps: labels.slice(0, stepCount).map((label, i) => ({
      ordinal: i + 1,
      kind: 'tool',
      name: `step_${i}`,
      label,
      status: 'completed',
      completedAt: '2026-10-04T10:00:01Z',
    })),
    question: null,
    proposalId: null,
    profileChanged: false,
    errorCode: null,
    allowance,
    ...extra,
  };
}

async function mockBackend(page: Page, scenario: Scenario = 'normal') {
  const state = {
    keys: [] as (string | null)[],
    gets: 0,
    failNextCreate: false,
    failPolls: 0,
    cancelled: false,
    answered: null as unknown,
  };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    const method = route.request().method();
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: true, isAuthenticated: true, data, error: null } });
    const fail = (status: number, code: string) =>
      route.fulfill({ status, json: { success: false, error: { code, message: code } } });

    if (url === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (url === '/api/career/profile') return send(profile);
    if (url === '/api/career/allowance')
      return send({
        policyVersion: 'v1',
        limit: 20,
        used: 1,
        reserved: 0,
        remaining: 19,
        resetsAt: '2026-11-01T00:00:00Z',
      });
    if (url === '/api/career/goals') return fail(404, 'CareerGoalNotFound');
    if (url === '/api/career/runs' && method === 'POST') {
      state.keys.push(route.request().headers()['idempotency-key'] ?? null);
      if (state.failNextCreate) {
        state.failNextCreate = false;
        return route.abort('failed');
      }
      if (scenario === 'allowance') return fail(429, 'CareerAllowanceExhausted');
      if (scenario === 'profileRequired') return fail(409, 'CareerProfileRequired');
      return send(runDto('queued', 0), 202);
    }
    if (url === '/api/career/runs') {
      return send({
        runs: state.gets || state.cancelled ? [runDto('completed', 4)] : [],
        allowance,
      });
    }
    if (url === `/api/career/runs/${RUN_ID}/cancel`) {
      state.cancelled = true;
      return send(runDto('cancelled', 1));
    }
    if (url === `/api/career/runs/${RUN_ID}/answers`) {
      state.answered = route.request().postDataJSON();
      state.gets = 10;
      return send(runDto('queued', 1));
    }
    if (url === `/api/career/runs/${RUN_ID}` && method === 'GET') {
      if (state.failPolls > 0) {
        state.failPolls--;
        return route.abort('failed');
      }
      if (state.cancelled) return send(runDto('cancelled', 1));
      state.gets++;
      const n = state.gets;
      if (scenario === 'question' && n < 10) {
        return send(
          runDto('needs_input', 1, {
            question: { id: 'audience', text: 'Who should this summary speak to?', maxLength: 200 },
          })
        );
      }
      if (scenario === 'failed') return send(runDto('failed', 1, { errorCode: 'CareerTimeLimit' }));
      if (n === 1) return send(runDto('queued', 0));
      if (n === 2) return send(runDto('working', 1));
      if (n === 3) return send(runDto('working', 3));
      return send(
        runDto('completed', 4, {
          proposalId: 'proposal-9',
          profileChanged: scenario === 'changed',
        })
      );
    }
    if (url === '/api/career/profile/proposals/proposal-9') {
      return send({
        id: 'proposal-9',
        source: 'agent',
        resumeId: null,
        baseProfileVersion: 1,
        status: 'pending',
        isStale: false,
        items: [
          {
            id: 'i1',
            field: 'summary',
            value: 'A drafted summary.',
            currentValue: null,
            page: null,
            section: null,
            excerpt: null,
            flags: [],
          },
        ],
        createdAt: '2026-10-04T10:00:00Z',
      });
    }
    if (url.startsWith('/api/career/')) return send([]);
    return route.continue();
  });
  return state;
}

async function open(page: Page, query = '') {
  await page.goto(`/app/career/summary?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(
    page.getByRole('heading', { level: 1, name: 'Draft a profile summary' })
  ).toBeVisible();
}

test('start polls through real steps to a review link', async ({ page }) => {
  const state = await mockBackend(page);
  await open(page);
  await expect(page.getByText('19 of 20 drafts left this month')).toBeVisible();
  await page.getByRole('button', { name: 'Start draft' }).click();
  await expect(page).toHaveURL(/run=run-1/);
  expect(state.keys).toHaveLength(1);
  expect(state.keys[0]).toMatch(/^[0-9a-f-]{36}$/);
  await expect(page.locator('[data-run-status]')).toHaveText('Working', { timeout: 10_000 });
  await expect(page.getByRole('listitem').filter({ hasText: labels[0] })).toBeVisible();
  const review = page.getByRole('link', { name: 'Review the draft' });
  await expect(review).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('[data-run-status]')).toHaveText('Draft ready');
  await expect(page.getByRole('listitem').filter({ hasText: labels[3] })).toBeVisible();
  await expect(page.locator('.run-area')).not.toContainText('%');
  await expect(page.locator('progress, [role="progressbar"]')).toHaveCount(0);
  await review.click();
  await expect(page).toHaveURL(/\/app\/career\/import\?proposal=proposal-9/);
  await expect(page.getByRole('heading', { name: 'Review suggestions' })).toBeVisible();
  await expect(page.getByText('A drafted summary.')).toBeVisible();
});

test('reload mid-run reconnects via ?run= and keeps polling', async ({ page }) => {
  await mockBackend(page);
  await open(page, `&run=${RUN_ID}`);
  await expect(page.locator('[data-run-status]')).toHaveText('Waiting to start');
  await page.reload();
  await expect(page.locator('[data-run-status]')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Review the draft' })).toBeVisible({
    timeout: 15_000,
  });
});

test('a failed poll shows the retry note and recovers', async ({ page }) => {
  const state = await mockBackend(page);
  state.failPolls = 1;
  await open(page, `&run=${RUN_ID}`);
  await expect(page.getByText('Connection lost. Retrying…')).toBeVisible();
  await expect(page.locator('[data-run-status]')).toBeVisible({ timeout: 10_000 });
  await expect(page.getByText('Connection lost. Retrying…')).toHaveCount(0);
});

test('a question can be answered', async ({ page }) => {
  const state = await mockBackend(page, 'question');
  await open(page, `&run=${RUN_ID}`);
  await expect(page.locator('[data-run-status]')).toHaveText('Needs your answer');
  const box = page.getByLabel('Who should this summary speak to?');
  await expect(box).toHaveAttribute('maxlength', '200');
  await box.fill('Hiring managers');
  await page.getByRole('button', { name: 'Send answer' }).click();
  await expect
    .poll(() => state.answered)
    .toEqual({ questionId: 'audience', answer: 'Hiring managers' });
  await expect(page.getByRole('link', { name: 'Review the draft' })).toBeVisible({
    timeout: 15_000,
  });
});

test('cancel shows the stopped text', async ({ page }) => {
  await mockBackend(page);
  await open(page, `&run=${RUN_ID}`);
  await page.getByRole('button', { name: 'Cancel' }).click();
  await expect(page.getByText('Stopped. Nothing was changed on your profile.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Start a new draft' })).toBeVisible();
});

test('a retried start after a failed POST reuses the same idempotency key', async ({ page }) => {
  const state = await mockBackend(page);
  state.failNextCreate = true;
  await open(page);
  await page.getByRole('button', { name: 'Start draft' }).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await page.getByRole('button', { name: 'Start draft' }).click();
  await expect(page).toHaveURL(/run=run-1/);
  expect(state.keys).toHaveLength(2);
  expect(state.keys[0]).toBeTruthy();
  expect(state.keys[1]).toBe(state.keys[0]);
});

test('exhausted allowance shows a message', async ({ page }) => {
  await mockBackend(page, 'allowance');
  await open(page);
  await page.getByRole('button', { name: 'Start draft' }).click();
  await expect(page.getByText('You have used all drafts for this month.')).toBeVisible();
});

test('missing profile links to the profile page', async ({ page }) => {
  await mockBackend(page, 'profileRequired');
  await open(page);
  await page.getByRole('button', { name: 'Start draft' }).click();
  await expect(page.getByRole('link', { name: 'Confirm your profile first' })).toHaveAttribute(
    'href',
    '/app/career/profile'
  );
});

test('a failed run explains why and offers a new draft', async ({ page }) => {
  await mockBackend(page, 'failed');
  await open(page, `&run=${RUN_ID}`);
  await expect(page.getByText(/took too long/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Start a new draft' })).toBeVisible();
});

test('warns when the profile changed after the draft started', async ({ page }) => {
  await mockBackend(page, 'changed');
  await open(page, `&run=${RUN_ID}`);
  await expect(page.getByText(/Your profile changed after this draft started/)).toBeVisible({
    timeout: 15_000,
  });
});

test('fits a 320px screen', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 700 });
  await mockBackend(page);
  await open(page, `&run=${RUN_ID}`);
  await expect(page.getByRole('link', { name: 'Review the draft' })).toBeVisible({
    timeout: 15_000,
  });
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth
  );
  expect(overflow).toBe(false);
});

test('career import opens a proposal from ?proposal=', async ({ page }) => {
  await mockBackend(page);
  await page.goto('/app/career/import?e2eAuthBypass=1&proposal=proposal-9');
  await expect(page.getByRole('heading', { name: 'Review suggestions' })).toBeVisible();
  await expect(page.getByText('A drafted summary.')).toBeVisible();
  // An assistant draft is not a resume import: its own heading, no upload prompt.
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Review your drafted summary');
  await expect(page.getByText('Upload a PDF or DOCX')).toHaveCount(0);
});
