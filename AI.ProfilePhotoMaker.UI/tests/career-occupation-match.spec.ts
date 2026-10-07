import { test, expect, Page } from '@playwright/test';

const RUN_ID = 'run-1';
const MATCH_ID = 'match-1';
const allowance = { used: 0, reserved: 0, limit: 20, periodStart: '2026-10-01T00:00:00Z' };
const labels = [
  'Read your confirmed profile',
  'Read your career goal',
  'Compared your duties with occupation tasks',
  'Saved the matches for your review',
];
const profile = {
  id: 'p1',
  version: 1,
  etag: '"p1"',
  facts: { currentTitle: 'Analyst', skills: [], highlights: [] },
  provenance: { source: 'manual', confirmedAt: '2026-10-01T00:00:00Z' },
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
};
const goal = {
  id: 'g1',
  version: 2,
  etag: '"goal-v2"',
  goal: { targetRole: 'Backend lead' },
  basedOnProfileVersion: 1,
  isStale: false,
  occupation: null as unknown,
  provenance: { source: 'manual', confirmedAt: '2026-10-01T00:00:00Z' },
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
};
const reference = {
  name: 'O*NET 30.0 Database',
  release: '30.0',
  releaseDate: '2025-08',
  taxonomy: 'O*NET-SOC 2019',
  license: 'CC BY 4.0',
  licenseUrl: 'https://creativecommons.org/licenses/by/4.0/',
  url: 'https://www.onetcenter.org/database.html',
  attribution:
    'This page includes information from the O*NET 30.0 Database by the U.S. Department of Labor.',
};
const candidate = {
  code: '15-1252.00',
  title: 'Software Developers',
  description: 'Research, design and develop computer software.',
  strength: 'strong',
  evidence: [
    {
      kind: 'duty',
      profileField: 'highlights',
      profileIndex: 0,
      profileText: 'Built REST APIs',
      referenceKind: 'task',
      referenceId: '16987',
      referenceText: 'Modify existing software to correct errors',
    },
    {
      kind: 'skill',
      profileField: 'skills',
      profileIndex: 1,
      profileText: 'Python',
      referenceKind: 'technology',
      referenceId: null,
      referenceText: 'Python',
    },
  ],
  titleMatched: true,
  missingEvidence: ['Analyze user needs and software requirements'],
  knownGaps: ['Systems Analysis'],
  unsupportedSkills: ['Phlebotomy'],
};
const second = {
  ...candidate,
  code: '15-1211.00',
  title: 'Computer Systems Analysts',
  strength: 'weak',
  titleMatched: false,
};

type Scenario = 'normal' | 'question' | 'unsupported' | 'changed';

function runDto(status: string, stepCount: number, extra: Record<string, unknown> = {}) {
  return {
    id: RUN_ID,
    task: 'occupation_match',
    status,
    createdAt: '2026-10-04T10:00:00Z',
    updatedAt: '2026-10-04T10:00:00Z',
    completedAt: null,
    pinnedProfileVersion: 1,
    pinnedGoalVersion: 2,
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
    occupationMatchId: null,
    profileChanged: false,
    errorCode: null,
    allowance,
    ...extra,
  };
}

async function mockBackend(page: Page, scenario: Scenario = 'normal') {
  const state = {
    answered: null as unknown,
    confirmed: null as unknown,
    ifMatch: null as string | null,
    confirmFailure: null as { status: number; code: string } | null,
    goalMissing: false,
    gets: 0,
    savedOccupation: null as unknown,
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
    if (url === '/api/career/journey') {
      const occ = state.savedOccupation as { code: string; title: string } | null;
      return send({
        profile: { version: 1, confirmed: true },
        goal: {
          version: 1,
          occupationCode: occ?.code ?? null,
          occupationTitle: occ?.title ?? null,
          location: null,
        },
        nextAction: { key: 'none', route: '/app/career' },
        latestResult: occ
          ? { kind: 'occupation_match', id: MATCH_ID, createdAt: '2026-10-05T10:00:00Z' }
          : null,
        activeRuns: [],
        stale: [],
      });
    }
    if (url === '/api/career/profile') return send(profile);
    if (url === '/api/career/goals') {
      if (state.goalMissing) return fail(404, 'CareerGoalNotFound');
      return send({ ...goal, occupation: state.savedOccupation });
    }
    if (url === '/api/career/runs' && method === 'POST') {
      expect(route.request().postDataJSON()).toEqual({ task: 'occupation_match' });
      return send(runDto('queued', 0), 202);
    }
    if (url === `/api/career/runs/${RUN_ID}/answers`) {
      state.answered = route.request().postDataJSON();
      state.gets = 10;
      return send(runDto('queued', 1));
    }
    if (url === `/api/career/runs/${RUN_ID}` && method === 'GET') {
      state.gets++;
      const n = state.gets;
      if (scenario === 'question' && n < 10) {
        return send(
          runDto('needs_input', 1, {
            question: {
              id: 'occupation',
              text: 'Which of these is closest to the work you want analysed?',
              maxLength: 20,
              choices: [
                { value: '15-1252.00', label: 'Software Developers' },
                { value: 'none', label: 'None of these' },
              ],
            },
          })
        );
      }
      if (n === 1) return send(runDto('queued', 0));
      if (n === 2) return send(runDto('working', 2));
      return send(runDto('completed', 4, { occupationMatchId: MATCH_ID }));
    }
    if (url === `/api/career/occupation-matches/${MATCH_ID}/confirm`) {
      state.ifMatch = route.request().headers()['if-match'] ?? null;
      state.confirmed = route.request().postDataJSON();
      if (state.confirmFailure) return fail(state.confirmFailure.status, state.confirmFailure.code);
      return send(goal);
    }
    if (url === `/api/career/occupation-matches/${MATCH_ID}/dismiss`) {
      return send({ ...matchDto(scenario), status: 'dismissed' });
    }
    if (url === `/api/career/occupation-matches/${MATCH_ID}`) return send(matchDto(scenario));
    if (url.startsWith('/api/career/')) return send([]);
    return route.continue();
  });
  return state;
}

function matchDto(scenario: Scenario) {
  const unsupported = scenario === 'unsupported';
  return {
    id: MATCH_ID,
    runId: RUN_ID,
    status: unsupported ? 'unsupported' : 'proposed',
    pinnedProfileVersion: 1,
    pinnedGoalVersion: 2,
    profileChanged: scenario === 'changed',
    reference,
    matcherVersion: 'duty-overlap-2',
    candidates: unsupported ? [] : [candidate, second],
    clarification:
      scenario === 'question' ? { question: 'Which?', answer: 'Software Developers' } : null,
    guidance: unsupported
      ? 'Describe the work you do in your profile highlights so we can compare it.'
      : null,
    confirmedCode: null,
    confirmedIntoGoalVersion: null,
    createdAt: '2026-10-04T10:00:00Z',
    decidedAt: null,
  };
}

async function open(page: Page, query = '') {
  await page.goto(`/app/career/occupation?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(
    page.getByRole('heading', { level: 1, name: 'Confirm your occupation' })
  ).toBeVisible();
}

test('shows intro, polls real steps and shows evidence with attribution', async ({ page }) => {
  await mockBackend(page);
  await open(page);
  await expect(page.getByText('Your job title alone does not decide the match.')).toBeVisible();
  await page.getByRole('button', { name: 'Find matching occupations' }).click();
  await expect(page).toHaveURL(/run=run-1/);
  await expect(page.getByRole('listitem').filter({ hasText: labels[0] })).toBeVisible({
    timeout: 10_000,
  });
  await expect(page.getByRole('heading', { name: 'Matching occupations' })).toBeVisible({
    timeout: 15_000,
  });
  const first = page.getByRole('article', { name: 'Software Developers' });
  await expect(first.getByText('Strong evidence')).toBeVisible();
  await expect(first.getByText('You wrote: Built REST APIs')).toBeVisible();
  await expect(
    first.getByText('Occupation task: Modify existing software to correct errors')
  ).toBeVisible();
  await expect(first.getByText('Skill/Technology: Python')).toBeVisible();
  await expect(first.getByText('Your job title also matches.')).toBeVisible();
  await expect(first.getByRole('heading', { name: 'Not shown in your profile' })).toBeVisible();
  await expect(first.getByText('Analyze user needs and software requirements')).toBeVisible();
  await expect(
    first.getByRole('heading', {
      name: 'Skills this occupation relies on that you have not listed',
    })
  ).toBeVisible();
  await expect(first.getByRole('heading', { name: 'Your skills not used here' })).toBeVisible();
  await expect(page.getByText('Limited evidence')).toBeVisible();
  await expect(page.locator('[data-attribution]')).toContainText('U.S. Department of Labor');
  await expect(page.locator('[data-retention]')).toHaveText(
    'Matches are kept with your career data while your account exists. Dismissing a match does not change your goal.'
  );
  await expect(page.getByRole('link', { name: 'O*NET 30.0 Database' })).toHaveAttribute(
    'href',
    reference.url
  );
  await expect(page.getByRole('link', { name: 'CC BY 4.0' })).toHaveAttribute('rel', /noopener/);
  await expect(page.locator('main')).not.toContainText('%');
});

test('an ambiguous run asks a question with choices', async ({ page }) => {
  const state = await mockBackend(page, 'question');
  await open(page, `&run=${RUN_ID}`);
  const group = page.getByRole('group', {
    name: 'Which of these is closest to the work you want analysed?',
  });
  await expect(group).toBeVisible();
  await group.getByLabel('Software Developers').check();
  await page.getByRole('button', { name: 'Send answer' }).click();
  await expect
    .poll(() => state.answered)
    .toEqual({ questionId: 'occupation', answer: '15-1252.00' });
  await expect(page.getByText('You told us: Software Developers')).toBeVisible({ timeout: 15_000 });
});

test('unsupported results give guidance and link to the profile', async ({ page }) => {
  await mockBackend(page, 'unsupported');
  await open(page, `&run=${RUN_ID}`);
  await expect(page.getByText('Describe the work you do in your profile highlights')).toBeVisible({
    timeout: 15_000,
  });
  await expect(
    page.getByRole('link', { name: 'Add responsibilities to your profile' })
  ).toHaveAttribute('href', '/app/career/profile');
  await expect(page.getByRole('button', { name: 'Use this occupation' })).toHaveCount(0);
});

test('confirm sends If-Match and shows the saved state', async ({ page }) => {
  const state = await mockBackend(page);
  await open(page, `&run=${RUN_ID}`);
  const confirm = page.getByRole('button', { name: 'Use this occupation' });
  await expect(confirm).toBeDisabled({ timeout: 15_000 });
  await page.getByRole('radio', { name: /Software Developers/ }).check();
  await confirm.click();
  await expect(page.getByText('Saved to your career goal.')).toBeVisible();
  expect(state.ifMatch).toBe('"goal-v2"');
  expect(state.confirmed).toEqual({ occupationCode: '15-1252.00' });
});

test('a changed goal shows the reload message', async ({ page }) => {
  const state = await mockBackend(page);
  state.confirmFailure = { status: 412, code: 'CareerVersionConflict' };
  await open(page, `&run=${RUN_ID}`);
  await page.getByRole('radio', { name: /Software Developers/ }).check({ timeout: 15_000 });
  await page.getByRole('button', { name: 'Use this occupation' }).click();
  await expect(
    page.getByText('Your goal changed in another tab. Reload and try again.')
  ).toBeVisible();
});

test('a missing goal links to setup', async ({ page }) => {
  const state = await mockBackend(page);
  state.confirmFailure = { status: 409, code: 'CareerGoalRequired' };
  await open(page, `&run=${RUN_ID}`);
  await page.getByRole('radio', { name: /Software Developers/ }).check({ timeout: 15_000 });
  await page.getByRole('button', { name: 'Use this occupation' }).click();
  await expect(page.getByText('Save a career goal first.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Set up your career goal' })).toHaveAttribute(
    'href',
    '/app/career/setup'
  );
});

test('a changed profile disables confirm', async ({ page }) => {
  await mockBackend(page, 'changed');
  await open(page, `&run=${RUN_ID}`);
  await expect(
    page.getByText('Your profile changed after this match. Start a new match.')
  ).toBeVisible({
    timeout: 15_000,
  });
  await page.getByRole('radio', { name: /Software Developers/ }).check();
  await expect(page.getByRole('button', { name: 'Use this occupation' })).toBeDisabled();
});

test('dismiss hides the actions', async ({ page }) => {
  await mockBackend(page);
  await open(page, `&run=${RUN_ID}`);
  await page.getByRole('button', { name: 'Dismiss these matches' }).click({ timeout: 15_000 });
  await expect(page.getByText('You dismissed these matches.')).toBeVisible();
});

test('fits a 320px screen', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 700 });
  await mockBackend(page);
  await open(page, `&run=${RUN_ID}`);
  await expect(page.getByRole('heading', { name: 'Matching occupations' })).toBeVisible({
    timeout: 15_000,
  });
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth
  );
  expect(overflow).toBe(false);
});

test('career home shows the confirmed occupation', async ({ page }) => {
  const state = await mockBackend(page);
  state.savedOccupation = {
    code: '15-1252.00',
    title: 'Software Developers',
    referenceRelease: '30.0',
    matchId: MATCH_ID,
  };
  await page.goto('/app/career?e2eAuthBypass=1');
  await expect(page.locator('[data-goal]')).toContainText('Software Developers');
  await expect(page.getByRole('link', { name: 'Occupation match' })).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
});
