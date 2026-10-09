import { test, expect, Page } from '@playwright/test';
import { formatFigure } from '../src/app/pages/career/market-format';
import type {
  MarketBriefDto,
  MarketFigure,
  MarketSection,
} from '../src/app/services/career-profile.service';
import {
  RUN_ID,
  BRIEF_ID,
  allowance,
  labels,
  NAT,
  DEN,
  fig,
  wages,
  employment,
  outlook,
  alternatives,
  sources,
  briefDto,
  goal,
  runDto,
  mockBackend,
  open,
  expectAllFigures,
  type Options,
} from './fixtures/career-market-brief';

test('starts a run, shows real steps, then the saved brief with every figure formatted', async ({
  page,
}) => {
  const state = await mockBackend(page);
  await open(page);
  await expect(
    page.getByText(
      'Official U.S. statistics for your confirmed occupation, nationally and near your goal location.'
    )
  ).toBeVisible();
  await page.getByRole('button', { name: 'Build my market brief' }).click();
  expect(state.startBody).toEqual({ task: 'market_brief' });
  expect(state.key).toBeTruthy();
  await expect(page).toHaveURL(/run=run-1/);
  await expect(page.getByRole('listitem').filter({ hasText: labels[0] })).toBeVisible({
    timeout: 10_000,
  });
  await expect(page.locator('[data-brief]')).toBeVisible({ timeout: 15_000 });
  await expect(page).toHaveURL(/brief=brief-1/);
  await expect(page.getByRole('heading', { name: /Software Developers/ }).first()).toContainText(
    '15-1252.00'
  );
  await expect(page.locator('[data-location]')).toContainText(DEN.areaTitle);
  await expectAllFigures(page, briefDto());
  await expect(page.locator('[data-figure="wages:medianAnnual:99"]')).toHaveText('$135,980');
  await expect(page.locator('[data-figure="wages:medianDifferenceAnnual:19740"]')).toHaveText(
    '+$1,630'
  );
  await expect(page.locator('[data-figure="outlook:annualOpenings:99"]')).toHaveText(
    '95.3 thousand'
  );
  await expect(page.locator('[data-figure="wages:pct90Annual:99"]')).toHaveText('$239,200 or more');
  const table = page.getByRole('table', { name: /Wages/ });
  await expect(table.getByRole('columnheader', { name: 'Measure' })).toBeVisible();
  await expect(table.getByRole('columnheader', { name: `Local (${DEN.areaTitle})` })).toBeVisible();
  await expect(page.getByText('Recent briefs')).toBeVisible();
});

test('related occupations disclose shared mapping and both sources with coverage', async ({
  page,
}) => {
  await mockBackend(page);
  await open(page, `&brief=${BRIEF_ID}`);
  const related = page.locator('[data-section="alternatives"]');
  await expect(related.locator('[data-alternative="15-1299.08"] [data-mapping-note]')).toHaveText(
    'BLS publishes one estimate for 15-1299 Computer Occupations, All Other, which covers this occupation together with other detailed occupations.'
  );
  await expect(related.locator('[data-as-of]')).toContainText('As of May 2025');
  await expect(related.locator('[data-as-of]')).toContainText('As of 2025-2035');
  await expect(related.locator('[data-as-of]')).toContainText(`Coverage: ${sources[0].coverage}`);
  await expect(related.locator('[data-as-of]')).toContainText(`Coverage: ${sources[1].coverage}`);
  await expectAllFigures(page, briefDto());
});

test('source drawer shows the citation and closes with Escape', async ({ page }) => {
  await mockBackend(page);
  await open(page, `&brief=${BRIEF_ID}`);
  await page
    .locator('[data-figure="wages:medianAnnual:99"]')
    .locator('xpath=following-sibling::button')
    .click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText(sources[0].citation);
  await expect(dialog).toContainText('2026-05-15');
  await expect(dialog.getByRole('link', { name: 'Definitions' })).toHaveAttribute(
    'rel',
    /noopener/
  );
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await page.getByRole('button', { name: 'Sources', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText(sources[1].name);
});

test('reloading with ?brief= shows the saved brief', async ({ page }) => {
  await mockBackend(page);
  await open(page, `&brief=${BRIEF_ID}`);
  await expect(page.locator('[data-brief]')).toBeVisible();
  await page.reload();
  await expect(page.locator('[data-figure="wages:medianAnnual:99"]')).toHaveText('$135,980');
});

test('a stale brief is kept and offers a new one', async ({ page }) => {
  await mockBackend(page, { brief: briefDto({ stale: true, staleReasons: ['goal_changed'] }) });
  await open(page, `&brief=${BRIEF_ID}`);
  await expect(page.locator('[data-stale]')).toContainText(
    'Your goal or profile changed after this brief. It is kept as it was; build a new one for current inputs.'
  );
  await expect(page.getByRole('button', { name: 'Build a new brief' })).toBeVisible();
  await expect(page.locator('[data-stale-marker]')).toBeVisible();
});

test('a partial brief keeps wages and explains the failed outlook', async ({ page }) => {
  const failed: MarketSection = {
    ...outlook,
    status: 'failed',
    reason: 'CareerReferenceUnavailable',
    figures: [],
  };
  await mockBackend(page, {
    brief: briefDto({ status: 'partial', sections: [wages, employment, failed, alternatives] }),
  });
  await open(page, `&brief=${BRIEF_ID}`);
  await expect(page.locator('[data-partial]')).toContainText(
    'Some sections could not be loaded. Completed sections are kept.'
  );
  await expect(page.getByText('This data source could not be loaded.')).toBeVisible();
  await expect(page.locator('[data-figure="wages:medianAnnual:99"]')).toHaveText('$135,980');
  await expect(page.getByRole('button', { name: 'Try again' })).toBeVisible();
});

test('an unresolved location explains itself and links to the next action', async ({ page }) => {
  const nationalOnly = (s: MarketSection) => ({
    ...s,
    figures: s.figures.filter(f => f.areaCode === '99'),
  });
  const unavailable = (s: MarketSection): MarketSection => ({
    ...s,
    status: 'unavailable',
    reason: 'location_unresolved',
    figures: [],
  });
  await mockBackend(page, {
    brief: briefDto({
      location: { input: 'Atlantis', resolution: 'unresolved', local: null },
      sections: [nationalOnly(wages), unavailable(employment), outlook, alternatives],
      nextAction: { label: 'Check your target occupation', route: '/app/career/occupation' },
    }),
  });
  await open(page, `&brief=${BRIEF_ID}`);
  await expect(page.locator('[data-location]')).toHaveText('We could not place "Atlantis"');
  await expect(
    page.getByText('Add a city and state to your goal to see local figures.')
  ).toBeVisible();
  await expect(page.locator('[data-next-action] a')).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
});

test('a missing occupation asks to confirm it first', async ({ page }) => {
  await mockBackend(page, { goal: { ...goal, occupation: null } });
  await open(page);
  await expect(
    page.getByText('This page uses your confirmed occupation.', { exact: false })
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Confirm your occupation' })).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
  await expect(page.getByRole('button', { name: 'Build my market brief' })).toHaveCount(0);
});

test('the occupationRequired start error shows the same message', async ({ page }) => {
  await mockBackend(page, { startFailure: { status: 409, code: 'CareerOccupationRequired' } });
  await open(page);
  await page.getByRole('button', { name: 'Build my market brief' }).click();
  await expect(
    page.getByText('This page uses your confirmed occupation.', { exact: false })
  ).toBeVisible();
});

test('does not overflow horizontally at 320px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 800 });
  await mockBackend(page);
  await open(page, `&brief=${BRIEF_ID}`);
  await expect(page.locator('[data-brief]')).toBeVisible();
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth
  );
  expect(overflow).toBeLessThanOrEqual(0);
});

test('the career home links to the market brief', async ({ page }) => {
  await mockBackend(page);
  await page.goto('/app/career?e2eAuthBypass=1');
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('link', { name: 'Build your market brief' })).toHaveAttribute(
    'href',
    '/app/career/market'
  );
  await expect(
    page
      .getByRole('navigation', { name: 'Career steps' })
      .getByRole('link', { name: /Market brief/ })
  ).toHaveAttribute('href', '/app/career/market');
});

test('the analytics route opens the market brief', async ({ page }) => {
  await mockBackend(page);
  await page.goto('/app/career/analytics?e2eAuthBypass=1');
  await expect(page).toHaveURL(/\/app\/career\/market/);
  await expect(page.getByRole('heading', { level: 1, name: 'Career market brief' })).toBeVisible();
});

test('a failed run offers to try again', async ({ page }) => {
  const state = await mockBackend(page, { runFails: true });
  await open(page, `&run=${RUN_ID}`);
  await expect(page.getByRole('alert')).toContainText('We could not build your market brief');
  await page.getByRole('button', { name: 'Try again' }).click();
  await expect.poll(() => state.startBody).toEqual({ task: 'market_brief' });
});

test('each section states its as-of date and coverage, and the brief its retention', async ({
  page,
}) => {
  await mockBackend(page);
  await open(page, `&brief=${BRIEF_ID}`);
  await expect(page.locator('[data-section="wages"] [data-as-of]')).toHaveText(
    'As of May 2025 (published 2026-05-15). Coverage: ' + sources[0].coverage
  );
  await expect(page.locator('[data-section="outlook"] [data-as-of]')).toContainText(
    'As of 2025-2035 (published 2026-08-27).'
  );
  await expect(page.locator('[data-retention]')).toHaveText(
    'Briefs are kept with your career data while your account exists. A newer profile or goal never changes a saved brief.'
  );
});

test('state figures are labelled as a state fallback', async ({ page }) => {
  const co = { areaCode: '08', areaTitle: 'Colorado' };
  const stateWages: MarketSection = {
    ...wages,
    figures: [
      fig('medianAnnual', 'Median annual wage', 135980, 'usd_per_year', NAT),
      fig('medianAnnual', 'Median annual wage', 138390, 'usd_per_year', co),
    ],
  };
  await mockBackend(page, {
    brief: briefDto({
      location: {
        input: 'Boulder, CO',
        resolution: 'state',
        local: { code: '08', title: 'Colorado', type: 'state' },
      },
      sections: [stateWages, employment, outlook, alternatives],
    }),
  });
  await open(page, `&brief=${BRIEF_ID}`);
  await expect(page.locator('[data-section="wages"] thead')).toContainText(
    'Local (Colorado, state figures)'
  );
});
