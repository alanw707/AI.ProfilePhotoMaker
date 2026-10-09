// Shared populated-state mocks; imported by the feature spec and the axe contrast gate.
import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import { formatFigure } from '../../src/app/pages/career/market-format';
import type {
  MarketComparison,
  MarketComparisonArea,
  MarketMetric,
} from '../../src/app/services/career-profile.service';

export const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
export const SUPPRESSED = 'Wyoming';
export const TOP_CODED = 'New York';
export const STATES = [
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
export const CODES: Record<string, string> = {
  California: '06',
  Colorado: '08',
  [SUPPRESSED]: '56',
};
export const code = (title: string) => CODES[title] ?? String(100 + STATES.indexOf(title));

// California is rank 1 and Colorado rank 12 of 49; one state is suppressed and one top-coded.
export function buildStates(): MarketComparisonArea[] {
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
export const METROS: MarketComparisonArea[] = [
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
export const metrics: MarketMetric[] = [
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
export const states = buildStates();
export const goalDto = {
  id: 'goal-1',
  version: 2,
  etag: '"goal-v2"',
  goal: { targetRole: 'Developer', targetLocation: 'Austin, TX' },
  occupation: { code: '15-1252.00', title: 'Software Developers' },
};

export function comparison(
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

export interface Options {
  noOccupation?: boolean;
  noGoal?: boolean;
  saveStatus?: number;
}
export async function mock(page: Page, options: Options = {}) {
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
export async function open(page: Page, query = '') {
  await page.goto(`/app/career/markets?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Compare U.S. markets' })).toBeVisible();
}
export const tile = (page: Page, areaCode: string) =>
  page.locator(`button.tile[data-area="${areaCode}"]`);
export const row = (page: Page, areaCode: string) => page.locator(`tr[data-area="${areaCode}"]`);
export const overflowing = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
