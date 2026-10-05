import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import type { JobObservation, JobObservations } from '../src/app/services/career-profile.service';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const MALICIOUS = '<img src=x onerror=alert(1)>';

const observation = (id: string, over: Partial<JobObservation> = {}): JobObservation => ({
  observationId: `usajobs:${id}`,
  title: `Posting ${id}`,
  organization: 'Department of Veterans Affairs',
  locations: [{ city: 'Denver', state: 'CO', areaCode: '19740', match: 'user_area' }],
  multiLocation: false,
  pay: { min: 98500, max: 128000, unit: 'usd_per_year', basis: 'annual', status: 'available' },
  postedOn: '2026-09-28',
  closesOn: '2026-10-20',
  remoteEligibility: 'eligible',
  remoteNote: null,
  series: '2210',
  grade: 'GS-12',
  sourceUrl: `https://www.usajobs.gov/job/${id}`,
  sourceId: 'usajobs',
  ...over,
});
const multi = observation('2', {
  title: 'Developer in two places',
  multiLocation: true,
  locations: [
    { city: 'Denver', state: 'CO', areaCode: '19740', match: 'user_area' },
    { city: 'Colorado Springs', state: 'CO', areaCode: null, match: 'other' },
  ],
  pay: { min: 40, max: 55.5, unit: 'usd_per_hour', basis: 'hourly', status: 'available' },
});
const unknownRemote = observation('3', {
  title: 'Analyst with no remote statement',
  remoteEligibility: 'unknown',
  pay: { min: null, max: null, unit: 'usd_per_year', basis: 'annual', status: 'not_available' },
});
const all = [observation('1', { title: 'Remote developer' }), multi, unknownRemote];

interface Options {
  reason?: 'source_not_configured' | 'source_unavailable';
  stale?: boolean;
  titles?: string[];
}
function payload(url: URL, options: Options): JobObservations {
  const eligibleOnly = url.searchParams.get('eligibleOnly') === 'true';
  const q = (url.searchParams.get('q') ?? '').toLowerCase();
  let list = options.titles
    ? options.titles.map((title, i) => observation(`m${i}`, { title }))
    : all;
  list = list.filter(o => o.title.toLowerCase().includes(q));
  const unknown = list.filter(o => o.remoteEligibility === 'unknown').length;
  if (eligibleOnly) list = list.filter(o => o.remoteEligibility !== 'unknown');
  const available = !options.reason;
  const shown = available ? list : [];
  return {
    occupation: { code: '15-1252.00', title: 'Software Developers' },
    area: {
      input: url.searchParams.get('area') ?? 'Denver, CO',
      resolution: 'metro',
      code: '19740',
      title: 'Denver-Aurora-Centennial, CO',
    },
    coverage: {
      available,
      reason: options.reason ?? null,
      sourceId: 'usajobs',
      sourceName: 'USAJOBS',
      coverage: 'U.S. federal agencies only; not the private-sector market.',
      attribution: 'Job postings from USAJOBS (U.S. Office of Personnel Management).',
      sourceUrl: 'https://www.usajobs.gov/',
      retrievedAt: '2026-10-05T18:30:00Z',
      observedFrom: available ? '2026-09-20' : null,
      observedTo: available ? '2026-09-28' : null,
      counts: {
        matched: shown.length + (eligibleOnly ? unknown : 0) + 4,
        shown: shown.length,
        duplicateIds: 0,
        duplicateReposts: 0,
        expired: 0,
        remoteUnknownExcluded: eligibleOnly ? unknown : 0,
        otherLocationExcluded: available ? 4 : 0,
      },
    },
    preferences: { areaCode: '19740', stalePreference: !!options.stale, note: null },
    observations: shown,
    truncated: false,
    note: 'Postings are observations, not employment totals or an outlook.',
  };
}

async function mock(page: Page, options: Options = {}) {
  const requests: string[] = [];
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    const send = (data: unknown) =>
      route.fulfill({ status: 200, json: { success: true, isAuthenticated: true, data } });
    if (url.pathname === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (url.pathname === '/api/career/goals')
      return send({
        id: 'goal-1',
        etag: '"goal-v2"',
        goal: { targetRole: 'Developer', targetLocation: 'Austin, TX' },
        preferredArea: { code: '12420', title: 'Austin, TX', level: 'metro' },
        occupation: { code: '15-1252.00', title: 'Software Developers' },
      });
    if (url.pathname === '/api/career/jobs/observations') {
      requests.push(url.search);
      return send(payload(url, options));
    }
    return send([]);
  });
  return requests;
}
async function open(page: Page, query = '') {
  await page.goto(`/app/career/jobs?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Open postings' })).toBeVisible();
}
const cards = (page: Page) => page.locator('[data-observation]');

test.describe('desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test('intro names the source and says what the list is not', async ({ page }) => {
    await mock(page);
    await open(page);
    const intro = page.locator('[data-intro]');
    await expect(intro).toContainText('observed from one source, USAJOBS');
    await expect(intro).toContainText('not employment totals, not an outlook');
    await expect(intro).toContainText('not a count of all vacancies');
    await expect(intro).toContainText(
      'Job postings from USAJOBS (U.S. Office of Personnel Management).'
    );
    await expect(intro.getByRole('link', { name: /USAJOBS/ })).toHaveAttribute(
      'href',
      'https://www.usajobs.gov/'
    );
  });

  test('lists observations with pay basis, locations, remote status and a safe link', async ({
    page,
  }) => {
    await mock(page);
    await open(page);
    await expect(cards(page)).toHaveCount(3);
    await expect(cards(page).nth(0)).toContainText('$98,500 to $128,000 per year');
    await expect(cards(page).nth(0)).toContainText('28 September 2026');
    await expect(cards(page).nth(0)).toContainText('GS-12');
    await expect(cards(page).nth(1)).toContainText('$40.00 to $55.50 per hour');
    await expect(cards(page).nth(1).locator('[data-locations]')).toContainText(
      'Denver, CO (matches your area)'
    );
    await expect(cards(page).nth(1).locator('[data-locations]')).toContainText(
      'Colorado Springs, CO'
    );
    await expect(cards(page).nth(2)).toContainText('Pay not stated');
    await expect(cards(page).nth(2).locator('[data-remote]')).toHaveText('Remote not stated');
    const link = cards(page)
      .nth(0)
      .getByRole('link', { name: /View original posting/ });
    await expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    await expect(link).toHaveAttribute('target', '_blank');
  });

  test('the eligible-only filter drops the unknown-remote posting and counts it', async ({
    page,
  }) => {
    await mock(page);
    await open(page);
    await page.getByLabel('Only postings stated as remote eligible').check();
    await expect(page).toHaveURL(/eligibleOnly=true/);
    await expect(cards(page)).toHaveCount(2);
    await expect(page.getByText('Analyst with no remote statement')).toHaveCount(0);
    const counts = page.locator('[data-counts]');
    await expect(counts).toContainText('1 hidden: remote eligibility not stated');
    await expect(counts).toContainText('4 hidden: not open where you are');
    await expect(counts).not.toContainText(/vacancy rate/i);
  });

  test('available-but-empty names the filters and offers a way forward', async ({ page }) => {
    await mock(page);
    await open(page, '&q=cobol');
    const empty = page.locator('[data-empty]');
    await expect(empty).toContainText('No open postings matched');
    await expect(empty).toContainText('search: "cobol"');
    await expect(empty).toContainText('widening the area');
  });

  test('not configured explains itself and links to the benchmark pages', async ({ page }) => {
    await mock(page, { reason: 'source_not_configured' });
    await open(page);
    const box = page.locator('[data-unavailable]');
    await expect(box).toContainText('The posting source is not configured yet.');
    await expect(box).toContainText('USAJOBS');
    await expect(box).toContainText('U.S. federal agencies only');
    await expect(box).toContainText('Job postings from USAJOBS');
    await expect(box.getByRole('link', { name: 'wage benchmark' })).toBeVisible();
    await expect(box.getByRole('link', { name: 'pay analysis' })).toBeVisible();
    await expect(box.getByRole('link', { name: 'market comparison' })).toBeVisible();
    await expect(cards(page)).toHaveCount(0);
  });

  test('source unreachable uses its own words', async ({ page }) => {
    await mock(page, { reason: 'source_unavailable' });
    await open(page);
    await expect(page.locator('[data-reason]')).toHaveText(
      'The posting source could not be reached. The benchmark pages are unaffected.'
    );
  });

  test('a stale preference names the saved location and the current filter', async ({ page }) => {
    await mock(page, { stale: true });
    await open(page, '&area=Denver');
    await expect(page.locator('[data-stale]')).toHaveText(
      'Your saved location is Austin, TX; this filter is Denver.'
    );
  });

  test('filters persist across reload and clear', async ({ page }) => {
    const requests = await mock(page);
    await open(page);
    await page.getByLabel('Area').fill('Boulder');
    await page.getByLabel('Search title or organization').fill('developer');
    await page.getByLabel('Remote work').selectOption('eligible');
    await page.getByLabel('Only postings stated as remote eligible').check();
    await expect(page).toHaveURL(/area=Boulder/);
    await expect(page).toHaveURL(/q=developer/);
    await expect(page).toHaveURL(/remote=eligible/);
    await expect(page).toHaveURL(/eligibleOnly=true/);
    await page.reload();
    await expect(page.getByLabel('Area')).toHaveValue('Boulder');
    await expect(page.getByLabel('Search title or organization')).toHaveValue('developer');
    await expect(page.getByLabel('Remote work')).toHaveValue('eligible');
    await expect(page.getByLabel('Only postings stated as remote eligible')).toBeChecked();
    expect(requests.at(-1)).toContain('area=Boulder');
    await page.getByRole('button', { name: 'Clear' }).click();
    await expect(page).not.toHaveURL(/area=|q=|remote=|eligibleOnly=/);
    await expect(page.getByLabel('Area')).toHaveValue('');
  });

  test('untrusted listing text renders as literal text', async ({ page }) => {
    await mock(page, { titles: [MALICIOUS] });
    await open(page);
    await expect(page.getByText(MALICIOUS, { exact: true }).first()).toBeVisible();
    await expect(page.locator('main img')).toHaveCount(0);
  });
});

test.describe('mobile', () => {
  test.use({ viewport: { width: 320, height: 800 } });
  test('does not overflow horizontally at 320px', async ({ page }) => {
    await mock(page);
    await open(page);
    await expect(cards(page)).toHaveCount(3);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth
      )
    ).toBe(false);
  });
});

test.describe('accessibility', () => {
  test.skip(!existsSync(AXE_PATH), `axe-core not found at ${AXE_PATH}; set AXE_PATH`);
  for (const width of [1280, 390, 320]) {
    test(`0 axe WCAG 2.2 AA violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      const run = async () => {
        if (!(await page.evaluate(() => 'axe' in window)))
          await page.addScriptTag({ path: AXE_PATH });
        return page.evaluate(async () => {
          const result = await (window as any).axe.run(document, {
            runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
          });
          return result.violations.map(
            (v: any) => `${v.id}: ${v.nodes.map((n: any) => n.target.join(' ')).join(' | ')}`
          );
        });
      };
      await mock(page);
      await open(page);
      await expect(cards(page)).toHaveCount(3);
      expect(await run()).toEqual([]);
      await page.getByLabel('Only postings stated as remote eligible').check();
      await expect(cards(page)).toHaveCount(2);
      expect(await run()).toEqual([]);
      await page.unroute('**/api/**');
      await mock(page, { reason: 'source_not_configured' });
      await open(page);
      await expect(page.locator('[data-unavailable]')).toBeVisible();
      expect(await run()).toEqual([]);
    });
  }
});
