import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import { formatFigure } from '../src/app/pages/career/market-format';
import type {
  MarketComparison,
  MarketComparisonArea,
  MarketMetric,
} from '../src/app/services/career-profile.service';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const SUPPRESSED = 'Wyoming';
const TOP_CODED = 'New York';
const STATES = [
  'Alabama',
  'Alaska',
  'Arizona',
  'Arkansas',
  'California',
  'Colorado',
  'Connecticut',
  'Delaware',
  'District of Columbia',
  'Florida',
  'Georgia',
  'Hawaii',
  'Idaho',
  'Illinois',
  'Indiana',
  'Iowa',
  'Kansas',
  'Kentucky',
  'Louisiana',
  'Maine',
  'Maryland',
  'Massachusetts',
  'Michigan',
  'Minnesota',
  'Mississippi',
  'Missouri',
  'Montana',
  'Nebraska',
  'Nevada',
  'New Hampshire',
  'New Jersey',
  'New Mexico',
  'New York',
  'North Carolina',
  'North Dakota',
  'Ohio',
  'Oklahoma',
  'Oregon',
  'Pennsylvania',
  'Rhode Island',
  'South Carolina',
  'South Dakota',
  'Tennessee',
  'Texas',
  'Utah',
  'Vermont',
  'Virginia',
  'Washington',
  'West Virginia',
  'Wisconsin',
  SUPPRESSED,
];
const CODES: Record<string, string> = { California: '06', Colorado: '08', [SUPPRESSED]: '56' };
const code = (title: string) => CODES[title] ?? String(100 + STATES.indexOf(title));

// California is rank 1 and Colorado rank 12 of 49; one state is suppressed and one top-coded.
function buildStates(): MarketComparisonArea[] {
  const ranked = [
    'California',
    ...STATES.filter(s => !['California', 'Colorado', SUPPRESSED, TOP_CODED].includes(s)).slice(
      0,
      10
    ),
    'Colorado',
    ...STATES.filter(s => !['California', 'Colorado', SUPPRESSED, TOP_CODED].includes(s)).slice(10),
  ];
  const areas = ranked.map((title, i): MarketComparisonArea => {
    const rank = i + 1;
    const value =
      rank === 1 ? 191000 : rank < 12 ? 191000 - rank * 3000 : 138390 - (rank - 12) * 1500;
    return {
      areaCode: code(title),
      areaTitle: title,
      type: 'state',
      value: title === 'Colorado' ? 138390 : value,
      status: 'available',
      rank,
      rankedOf: ranked.length,
      selected: false,
    };
  });
  const unranked: MarketComparisonArea[] = [
    {
      areaCode: code(TOP_CODED),
      areaTitle: TOP_CODED,
      type: 'state',
      value: 239200,
      status: 'top_coded',
      rank: null,
      rankedOf: ranked.length,
      selected: false,
    },
    {
      areaCode: code(SUPPRESSED),
      areaTitle: SUPPRESSED,
      type: 'state',
      value: null,
      status: 'not_available',
      rank: null,
      rankedOf: ranked.length,
      selected: false,
    },
  ];
  return [...areas, ...unranked];
}
const METROS: MarketComparisonArea[] = [
  ['19740', 'Denver-Aurora-Centennial, CO', 137610],
  ['14500', 'Boulder, CO', 150200],
  ['41860', 'San Francisco-Oakland-Fremont, CA', 198000],
].map(([areaCode, areaTitle, value], i) => ({
  areaCode: String(areaCode),
  areaTitle: String(areaTitle),
  type: 'metro' as const,
  value: Number(value),
  status: 'available' as const,
  rank: [2, 3, 1][i],
  rankedOf: 3,
  selected: false,
}));
const metrics: MarketMetric[] = [
  {
    key: 'median_wage',
    label: 'Median annual wage',
    unit: 'usd_per_year',
    measure: 'BLS OEWS median annual wage',
    supported: true,
    reason: null,
    geographyLevels: ['national', 'state', 'metro'],
  },
  {
    key: 'employment',
    label: 'Employment',
    unit: 'jobs',
    measure: 'Estimated wage and salary employment',
    supported: true,
    reason: null,
    geographyLevels: ['national', 'state', 'metro'],
  },
  {
    key: 'projected_change',
    label: 'Projected employment change',
    unit: 'percent',
    measure: 'BLS Employment Projections',
    supported: false,
    reason: 'national_only_source',
    geographyLevels: ['national'],
  },
];
const states = buildStates();
const goalDto = {
  id: 'goal-1',
  version: 2,
  etag: '"goal-v2"',
  goal: { targetRole: 'Developer', targetLocation: 'Austin, TX' },
  occupation: { code: '15-1252.00', title: 'Software Developers' },
};

function comparison(
  level: string,
  metric: MarketMetric,
  q: string,
  selected: string[]
): MarketComparison {
  const source = level === 'metro' ? METROS : states;
  const areas = source
    .filter(a => a.areaTitle.toLowerCase().includes(q.toLowerCase()))
    .map(a => ({ ...a, selected: selected.includes(a.areaCode) }));
  return {
    occupation: {
      code: '15-1252.00',
      title: 'Software Developers',
      publishedCode: '15-1252',
      mapping: 'exact',
    },
    metric,
    level: level as 'state' | 'metro',
    national: { areaCode: '99', areaTitle: 'U.S.', value: 135980, status: 'available' },
    reference: {
      release: '2025-05',
      publishedOn: '2026-05-15',
      coverage: 'Nonfarm establishments in all 50 states and DC.',
      definitionsUrl: 'https://www.bls.gov/oes/oes_ques.htm',
      citation: 'U.S. Bureau of Labor Statistics, OEWS May 2025.',
    },
    areas,
    selectionLimit: 3,
    truncated: false,
  };
}

interface Options {
  noOccupation?: boolean;
  noGoal?: boolean;
  saveStatus?: number;
}
async function mock(page: Page, options: Options = {}) {
  const posts: unknown[] = [];
  const ifMatch: string[] = [];
  let etag = goalDto.etag;
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: status < 400, isAuthenticated: true, data } });
    const fail = (status: number, errorCode: string, message: string) =>
      route.fulfill({ status, json: { success: false, error: { code: errorCode, message } } });
    if (url.pathname === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (url.pathname === '/api/career/goals') {
      return options.noGoal
        ? fail(404, 'CareerGoalNotFound', 'No goal')
        : send({
            ...goalDto,
            etag,
            occupation: options.noOccupation ? null : goalDto.occupation,
          });
    }
    if (url.pathname === '/api/career/markets/metrics') return send({ metrics });
    if (url.pathname === '/api/career/markets/compare') {
      if (options.noOccupation)
        return fail(409, 'CareerOccupationRequired', 'Confirm your occupation.');
      const metric = metrics.find(m => m.key === url.searchParams.get('metric'));
      if (!metric) return fail(400, 'ValidationError', 'Unknown metric');
      if (!metric.supported) return fail(409, 'CareerMetricUnsupported', metric.reason ?? '');
      const selected = (url.searchParams.get('areas') ?? '').split(',').filter(Boolean);
      return send(
        comparison(
          url.searchParams.get('level') ?? 'state',
          metric,
          url.searchParams.get('q') ?? '',
          selected
        )
      );
    }
    if (url.pathname === '/api/career/markets/preference') {
      posts.push(route.request().postDataJSON());
      ifMatch.push(route.request().headers()['if-match'] ?? '');
      if (options.saveStatus === 412 && ifMatch.length === 1) {
        etag = '"goal-v3"'; // another tab saved: the freshly fetched goal carries the new ETag
        return fail(412, 'CareerVersionConflict', 'stale');
      }
      return send({ ...goalDto, etag: '"goal-v4"', version: 4 });
    }
    return send([]);
  });
  return Object.assign(posts, { ifMatch });
}
async function open(page: Page, query = '') {
  await page.goto(`/app/career/markets?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Compare U.S. markets' })).toBeVisible();
}
const tile = (page: Page, areaCode: string) => page.locator(`button.tile[data-area="${areaCode}"]`);
const row = (page: Page, areaCode: string) => page.locator(`tr[data-area="${areaCode}"]`);
const overflowing = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);

test.describe('desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test('intro, legend and a heatmap cell label from the payload', async ({ page }) => {
    await mock(page);
    await open(page);
    const intro = page.locator('[data-intro]');
    await expect(intro).toContainText('BLS OEWS median annual wage');
    await expect(intro).toContainText('May 2025');
    await expect(intro).toContainText('15 May 2026');
    await expect(intro).toContainText('Nonfarm establishments in all 50 states and DC.');
    await expect(intro).toContainText('not job openings');
    expect(await page.locator('body').innerText()).not.toMatch(/\d{4}-\d{2}-\d{2}/);
    const legend = page.locator('[data-legend]');
    await expect(legend).toContainText('U.S. dollars per year');
    await expect(legend).toContainText('Hatched: no figure to rank');
    await expect(page.locator('[data-national]')).toContainText('$135,980');
    await expect(tile(page, '08')).toHaveAttribute(
      'aria-label',
      'Colorado, median annual wage $138,390, rank 12 of 49'
    );
    await expect(tile(page, '06')).toHaveAttribute(
      'aria-label',
      /^California, .*\$191,000, rank 1 of 49$/
    );
  });

  test('dates read for people in the legend and the source dialog', async ({ page }) => {
    await mock(page);
    await open(page);
    await expect(page.locator('[data-legend]')).toContainText('published 15 May 2026');
    await page.getByRole('button', { name: 'Source details' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Release May 2025 · Published 15 May 2026');
    expect(await dialog.innerText()).not.toMatch(/\d{4}-\d{2}/);
  });

  test('every table row equals its heatmap cell', async ({ page }) => {
    await mock(page);
    await open(page);
    await expect(page.locator('button.tile')).toHaveCount(states.length);
    await expect(page.locator('tr[data-area]')).toHaveCount(states.length);
    await expect(page.locator('table caption')).toContainText(
      'Median annual wage, U.S. dollars per year, May 2025'
    );
    for (const a of states) {
      const figure = {
        key: 'median_wage',
        label: 'Median annual wage',
        unit: 'usd_per_year' as const,
        sourceId: 'oews',
        areaCode: a.areaCode,
        areaTitle: a.areaTitle,
        value: a.value,
        status: a.status,
      };
      const cell = tile(page, a.areaCode);
      const tableRow = row(page, a.areaCode);
      const raw = a.value === null ? '' : String(a.value);
      await expect(cell).toHaveAttribute('data-value', raw);
      await expect(tableRow).toHaveAttribute('data-value', raw);
      await expect(cell).toHaveAttribute('data-status', a.status);
      await expect(tableRow).toHaveAttribute('data-status', a.status);
      await expect(tableRow.locator('[data-cell="value"]')).toHaveText(formatFigure(figure));
      await expect(cell).toHaveAttribute(
        'aria-label',
        new RegExp(`^${a.areaTitle},.*${formatFigure(figure).replace(/[$()]/g, '\\$&')}`)
      );
      await expect(tableRow.locator('[data-cell="rank"]')).toHaveText(
        a.rank === null ? 'Not ranked' : `${a.rank} of ${a.rankedOf}`
      );
    }
  });

  test('suppressed and top-coded areas use the pattern and are not ranked', async ({ page }) => {
    await mock(page);
    await open(page);
    const suppressed = tile(page, code(SUPPRESSED));
    await expect(suppressed).toHaveClass(/unranked/);
    expect(await suppressed.evaluate(el => getComputedStyle(el).backgroundImage)).toContain(
      'repeating-linear-gradient'
    );
    await expect(suppressed).toContainText('No rank');
    const suppressedRow = row(page, code(SUPPRESSED));
    await expect(suppressedRow).toContainText('Not available (too few survey responses)');
    await expect(suppressedRow).toContainText('Not ranked');
    await expect(tile(page, code(TOP_CODED))).toHaveClass(/unranked/);
    await expect(row(page, code(TOP_CODED))).toContainText('$239,200 or more');
    await expect(tile(page, '06')).not.toHaveClass(/unranked/);
  });

  test('tiles follow the ranked order for keyboard focus', async ({ page }) => {
    await mock(page);
    await open(page);
    const order = await page
      .locator('button.tile')
      .evaluateAll(els => els.map(el => el.getAttribute('data-area')));
    expect(order[0]).toBe('06');
    expect(order[11]).toBe('08');
    expect(order.slice(-2).sort()).toEqual([code(TOP_CODED), code(SUPPRESSED)].sort());
  });

  test('the metric selector disables the national-only metric with its reason', async ({
    page,
  }) => {
    await mock(page);
    await open(page);
    const projected = page.getByRole('radio', { name: 'Projected employment change' });
    await expect(projected).toBeDisabled();
    await expect(page.locator('[data-metric-reason]')).toHaveText(
      'Only national projections are published, so this metric is not available per market.'
    );
    await expect(projected).toHaveAttribute('aria-describedby', 'metric-reason-projected_change');
    await expect(page.getByRole('radio', { name: 'Median annual wage' })).toBeChecked();
    await expect(page.getByRole('radio', { name: 'Employment', exact: true })).toBeEnabled();
  });

  test('filters persist across reload through the query params', async ({ page }) => {
    await mock(page);
    await open(page);
    await page.getByRole('radio', { name: 'Metropolitan area' }).check();
    await expect(page).toHaveURL(/level=metro/);
    await page.getByLabel('Search areas').fill('er');
    await expect(page).toHaveURL(/q=er/);
    await expect(page.locator('button.tile')).toHaveCount(2);
    await tile(page, '14500').click();
    await page.getByRole('radio', { name: 'Employment', exact: true }).check();
    await expect(page).toHaveURL(/metric=employment/);
    await expect(page).toHaveURL(/areas=14500/);
    await page.reload();
    await expect(page.getByRole('radio', { name: 'Metropolitan area' })).toBeChecked();
    await expect(page.getByRole('radio', { name: 'Employment', exact: true })).toBeChecked();
    await expect(page.getByLabel('Search areas')).toHaveValue('er');
    await expect(page.locator('button.tile')).toHaveCount(2);
    await expect(row(page, '14500').getByRole('checkbox')).toBeChecked();
    await page.getByRole('button', { name: 'Clear' }).click();
    await expect(page.getByLabel('Search areas')).toHaveValue('');
    await expect(page.locator('button.tile')).toHaveCount(3);
    await expect(page).not.toHaveURL(/q=|areas=/);
  });

  test('keyboard selection, text badge, sorting and the three-area limit', async ({ page }) => {
    await mock(page);
    await open(page);
    await tile(page, '08').focus();
    await page.keyboard.press('Enter');
    await expect(row(page, '08').getByRole('checkbox')).toBeChecked();
    await expect(tile(page, '08')).toHaveAttribute('aria-pressed', 'true');
    await expect(tile(page, '08').locator('.tile-badge')).toHaveText('Selected');
    await row(page, '06').getByRole('checkbox', { name: 'Select California' }).check();
    await tile(page, code('Texas')).click();
    await tile(page, code('Utah')).click();
    await expect(page.locator('[data-live]')).toContainText('You can compare up to 3 areas');
    await expect(tile(page, code('Utah'))).toHaveAttribute('aria-pressed', 'false');
    await row(page, code('Utah')).getByRole('checkbox').click();
    await expect(row(page, code('Utah')).getByRole('checkbox')).not.toBeChecked();
    await expect(page.locator('[data-selected]')).toContainText('Colorado, California, Texas');

    const valueHeader = page.getByRole('columnheader', { name: 'Value' });
    await expect(valueHeader).toHaveAttribute('aria-sort', 'descending');
    await valueHeader.getByRole('button').focus();
    await page.keyboard.press('Enter');
    await expect(valueHeader).toHaveAttribute('aria-sort', 'ascending');
    await expect(page.locator('tbody tr').first()).toHaveAttribute(
      'data-area',
      code(states[48].areaTitle)
    );
    const areaHeader = page.getByRole('columnheader', { name: 'Area' });
    await areaHeader.getByRole('button').click();
    await expect(areaHeader).toHaveAttribute('aria-sort', 'ascending');
    await expect(valueHeader).toHaveAttribute('aria-sort', 'none');
    await expect(page.locator('tbody tr').first()).toContainText('Alabama');
  });

  test('saving needs the confirmation step, then succeeds', async ({ page }) => {
    const posts = await mock(page);
    await open(page);
    const save = page.getByRole('button', { name: 'Save this location to my goal' });
    await expect(save).toBeDisabled();
    await tile(page, '08').click();
    await expect(save).toBeEnabled();
    await save.click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Set Colorado as the location on your career goal?');
    expect(posts).toHaveLength(0);
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    expect(posts).toHaveLength(0);
    await save.click();
    const request = page.waitForRequest('**/api/career/markets/preference');
    await dialog.getByRole('button', { name: 'Yes, save this location' }).click();
    expect((await request).headers()['if-match']).toBe('"goal-v2"');
    expect([...posts]).toEqual([{ areaCode: '08', level: 'state', confirmed: true }]);
    await expect(page.locator('[data-live]')).toHaveText('Saved to your career goal.');
  });

  test('a stale goal refetches the ETag, says try again, and the retry uses the fresh ETag', async ({
    page,
  }) => {
    const posts = await mock(page, { saveStatus: 412 });
    await open(page);
    await tile(page, '08').click();
    await page.getByRole('button', { name: 'Save this location to my goal' }).click();
    await page.getByRole('button', { name: 'Yes, save this location' }).click();
    await expect(page.locator('[data-error]')).toHaveText(
      'Your goal changed in another tab. Try again.'
    );
    const save = page.getByRole('button', { name: 'Save this location to my goal' });
    await expect(save).toBeEnabled();
    await save.click();
    await page.getByRole('button', { name: 'Yes, save this location' }).click();
    await expect(page.locator('[data-live]')).toHaveText('Saved to your career goal.');
    expect(posts.ifMatch).toEqual(['"goal-v2"', '"goal-v3"']);
    expect(posts.ifMatch[1]).not.toBe(posts.ifMatch[0]);
  });

  test('a selected area hidden by the filter never shows its code', async ({ page }) => {
    await mock(page);
    await open(page, '&areas=06&q=colo');
    const neutral = '1 selected area is not shown by the current filter';
    await expect(page.locator('[data-selected]')).toContainText(neutral);
    await expect(page.locator('tr[data-area]')).toHaveCount(1);
    expect(await page.locator('body').innerText()).not.toContain('06');
    await page.getByRole('button', { name: 'Save this location to my goal' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('1 area not shown by the current filter');
    expect(await dialog.innerText()).not.toContain('06');
    await expect(dialog).toContainText('This replaces "Austin, TX".');
  });

  test('known and hidden selections are named without codes', async ({ page }) => {
    await mock(page);
    await open(page, '&areas=08,06&q=colo');
    await expect(page.locator('[data-selected]')).toHaveText(
      'Selected: Colorado and 1 selected area is not shown by the current filter'
    );
  });

  test('no goal links to setup; no occupation asks to confirm it', async ({ page }) => {
    await mock(page, { noGoal: true });
    await open(page);
    await expect(page.getByRole('link', { name: 'Set up your career goal' })).toHaveAttribute(
      'href',
      '/app/career/setup'
    );
    await expect(page.getByRole('button', { name: 'Save this location to my goal' })).toHaveCount(
      0
    );
    await page.close();
  });

  test('without a confirmed occupation the page links to the occupation step', async ({ page }) => {
    await mock(page, { noOccupation: true });
    await open(page);
    await expect(page.getByText('Confirm your occupation first.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Confirm your occupation' })).toHaveAttribute(
      'href',
      '/app/career/occupation'
    );
    await expect(page.locator('button.tile')).toHaveCount(0);
  });
});

for (const width of [390, 320]) {
  test.describe(`mobile ${width}px`, () => {
    test.use({ viewport: { width, height: 800 } });

    test('shows the searchable list instead of the tiles, same values, no overflow', async ({
      page,
    }) => {
      await mock(page);
      await open(page);
      await expect(page.locator('button.tile')).toHaveCount(0);
      const items = page.locator('li[data-area]');
      await expect(items).toHaveCount(states.length);
      for (const a of states) {
        await expect(page.locator(`li[data-area="${a.areaCode}"]`)).toHaveAttribute(
          'data-value',
          a.value === null ? '' : String(a.value)
        );
      }
      await expect(page.locator(`li[data-area="08"]`)).toContainText('$138,390');
      expect(await overflowing(page)).toBe(false);
      await page.getByLabel('Search areas').fill('colorado');
      await expect(items).toHaveCount(1);
      await items.first().getByRole('checkbox').check();
      await page.getByLabel('Search areas').fill('');
      await expect(items).toHaveCount(states.length);
      await page.getByText('Show as a table').click();
      await expect(row(page, '08')).toBeVisible();
      await expect(row(page, '08').getByRole('checkbox')).toBeChecked();
      await expect(row(page, '08').locator('[data-cell="value"]')).toHaveText('$138,390');
      expect(await overflowing(page)).toBe(false);
    });
  });
}

test.describe('accessibility', () => {
  test.skip(!existsSync(AXE_PATH), `axe-core not found at ${AXE_PATH}; set AXE_PATH`);
  for (const width of [1280, 390, 320]) {
    test(`0 axe WCAG 2.2 AA violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mock(page);
      await open(page, '&areas=08,06');
      await expect(page.locator('tr[data-area]').first()).toBeAttached();
      if (width < 720) await page.getByText('Show as a table').click();
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
      await page.getByRole('radio', { name: 'Metropolitan area' }).check();
      await expect(page.locator('tr[data-area]').first()).toBeAttached();
      expect(await run()).toEqual([]);
    });
  }
});
