// Shared populated-state mocks; imported by the feature spec and the axe contrast gate.
import { test, expect, Page } from '@playwright/test';
import { formatFigure } from '../../src/app/pages/career/market-format';
import type {
  MarketBriefDto,
  MarketFigure,
  MarketSection,
} from '../../src/app/services/career-profile.service';

export const RUN_ID = 'run-1';
export const BRIEF_ID = 'brief-1';
export const allowance = { used: 0, reserved: 0, limit: 20, periodStart: '2026-10-01T00:00:00Z' };
export const labels = [
  'Read your career goal',
  'Looked up wages and employment',
  'Looked up the job outlook',
  'Compared related occupations',
  'Saved your market brief',
];
export const NAT = { areaCode: '99', areaTitle: 'U.S.' };
export const DEN = { areaCode: '19740', areaTitle: 'Denver-Aurora-Centennial, CO' };

export function fig(
  key: string,
  label: string,
  value: number | string | null,
  unit: MarketFigure['unit'],
  area = NAT,
  status: MarketFigure['status'] = 'available',
  sourceId = 'oews'
): MarketFigure {
  return { key, label, value, status, unit, ...area, sourceId };
}

export const wages: MarketSection = {
  key: 'wages',
  title: 'Wages',
  status: 'complete',
  reason: null,
  note: 'Benchmark wages for this occupation. Not open jobs, not a personal salary prediction, not total compensation.',
  figures: [
    fig('medianAnnual', 'Median annual wage', 135980, 'usd_per_year'),
    fig('pct90Annual', '90th percentile annual wage', 239200, 'usd_per_year', NAT, 'top_coded'),
    fig('medianHourly', 'Median hourly wage', 65.38, 'usd_per_hour'),
    fig('meanPrse', 'Mean wage relative standard error', 0.4, 'percent_rse'),
    fig('medianAnnual', 'Median annual wage', 137610, 'usd_per_year', DEN),
    fig('pct90Annual', '90th percentile annual wage', null, 'usd_per_year', DEN, 'not_available'),
    fig('medianDifferenceAnnual', 'Local median minus national', 1630, 'usd_per_year', DEN),
  ],
  items: [],
};
export const employment: MarketSection = {
  key: 'employment',
  title: 'Employment',
  status: 'complete',
  reason: null,
  note: 'How many people hold this occupation.',
  figures: [
    fig('employment', 'Employment', 1687890, 'jobs'),
    fig('employment', 'Employment', 27010, 'jobs', DEN),
    fig('jobsPer1000', 'Jobs per 1,000 jobs', 16.78, 'per_1000_jobs', DEN),
    fig('locationQuotient', 'Location quotient', 1.55, 'ratio', DEN),
  ],
  items: [],
};
export const outlook: MarketSection = {
  key: 'outlook',
  title: 'Outlook',
  status: 'complete',
  reason: null,
  note: 'Projected national change, 2025 to 2035.',
  figures: [
    fig('changePercent', 'Projected change', 10.2, 'percent', NAT, 'available', 'projections'),
    fig(
      'annualOpenings',
      'Average annual openings',
      95.3,
      'jobs_thousands',
      NAT,
      'available',
      'projections'
    ),
    fig(
      'typicalEducation',
      'Typical education',
      "Bachelor's degree",
      'text',
      NAT,
      'available',
      'projections'
    ),
  ],
  items: [],
};
export const alternatives: MarketSection = {
  key: 'alternatives',
  title: 'Related occupations',
  status: 'complete',
  reason: null,
  note: 'Other occupations from your match.',
  figures: [],
  items: [
    {
      code: '15-1211.00',
      title: 'Computer Systems Analysts',
      note: null,
      figures: [fig('medianAnnual', 'Median annual wage', 103800, 'usd_per_year')],
    } as MarketSection['items'][number] & { note: string | null },
    {
      code: '15-1299.08',
      title: 'Computer Systems Engineers/Architects',
      note: ' BLS publishes one estimate for 15-1299 Computer Occupations, All Other, which covers this occupation together with other detailed occupations.',
      figures: [
        fig('medianAnnual', 'Median annual wage', 116580, 'usd_per_year'),
        fig('changePercent', 'Projected change', 5.1, 'percent', NAT, 'available', 'projections'),
      ],
    } as MarketSection['items'][number] & { note: string | null },
  ],
};
export const sources = [
  {
    id: 'oews',
    name: 'Occupational Employment and Wage Statistics, May 2025',
    publisher: 'U.S. Bureau of Labor Statistics',
    referencePeriod: '2025-05',
    publishedOn: '2026-05-15',
    url: 'https://www.bls.gov/oes/',
    definitionsUrl: 'https://www.bls.gov/oes/oes_ques.htm',
    license: 'Public domain (U.S. government work)',
    citation: 'U.S. Bureau of Labor Statistics, OEWS May 2025, published 2026-05-15.',
    definition: 'Wages are before taxes and exclude benefits.',
    coverage: 'Nonfarm wage and salary jobs.',
  },
  {
    id: 'projections',
    name: 'Employment Projections 2025-35',
    publisher: 'U.S. Bureau of Labor Statistics',
    referencePeriod: '2025-2035',
    publishedOn: '2026-08-27',
    url: 'https://www.bls.gov/emp/',
    definitionsUrl: 'https://www.bls.gov/emp/documentation/',
    license: 'Public domain (U.S. government work)',
    citation: 'U.S. Bureau of Labor Statistics, Employment Projections 2025-35.',
    definition: 'Projected change in employment.',
    coverage: 'National.',
  },
];

export function briefDto(overrides: Partial<MarketBriefDto> = {}): MarketBriefDto {
  return {
    id: BRIEF_ID,
    runId: RUN_ID,
    status: 'complete',
    occupation: {
      code: '15-1252.00',
      title: 'Software Developers',
      published: {
        oews: { code: '15-1252', match: 'exact' },
        projections: { code: '15-1252', match: 'exact' },
      },
    },
    location: {
      input: 'Denver, CO',
      resolution: 'metro',
      local: { code: '19740', title: DEN.areaTitle, type: 'metro' },
    },
    pinned: {
      profileVersion: 1,
      goalVersion: 2,
      oewsRelease: '2025-05',
      projectionsRelease: '2025-2035',
    },
    stale: false,
    staleReasons: [],
    dataStale: false,
    sections: [wages, employment, outlook, alternatives],
    nextAction: null,
    sources,
    createdAt: '2026-10-05T10:00:00Z',
    ...overrides,
  };
}

export const goal = {
  id: 'g1',
  version: 2,
  etag: '"goal-v2"',
  goal: { targetRole: 'Backend lead', targetLocation: 'Denver, CO' },
  basedOnProfileVersion: 1,
  isStale: false,
  occupation: {
    code: '15-1252.00',
    title: 'Software Developers',
    referenceRelease: '30.0',
    matchId: 'm1',
  },
  provenance: { source: 'manual', confirmedAt: '2026-10-01T00:00:00Z' },
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: '2026-10-01T00:00:00Z',
};

export function runDto(status: string, stepCount: number, extra: Record<string, unknown> = {}) {
  return {
    id: RUN_ID,
    task: 'market_brief',
    status,
    createdAt: '2026-10-05T10:00:00Z',
    updatedAt: '2026-10-05T10:00:00Z',
    completedAt: null,
    pinnedProfileVersion: 1,
    pinnedGoalVersion: 2,
    steps: labels.slice(0, stepCount).map((label, i) => ({
      ordinal: i + 1,
      kind: 'tool',
      name: `step_${i}`,
      label,
      status: 'completed',
      completedAt: '2026-10-05T10:00:01Z',
    })),
    question: null,
    proposalId: null,
    marketBriefId: null,
    profileChanged: false,
    errorCode: null,
    allowance,
    ...extra,
  };
}

export interface Options {
  brief?: MarketBriefDto;
  goal?: unknown;
  startFailure?: { status: number; code: string };
  runFails?: boolean;
}

export async function mockBackend(page: Page, options: Options = {}) {
  const brief = options.brief ?? briefDto();
  const state = { gets: 0, startBody: null as unknown, key: null as string | null };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    const method = route.request().method();
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: true, isAuthenticated: true, data, error: null } });
    const fail = (status: number, code: string) =>
      route.fulfill({ status, json: { success: false, error: { code, message: code } } });

    if (url === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (url === '/api/career/journey')
      return send({
        profile: { version: 1, confirmed: true },
        goal: { version: 1, occupationCode: '15-1252.00', occupationTitle: 'Software Developers' },
        nextAction: { key: 'build_brief', route: '/app/career/market' },
        latestResult: null,
        activeRuns: [],
        stale: [],
      });
    if (url === '/api/career/profile')
      return send({
        id: 'p1',
        version: 1,
        etag: '"p1"',
        facts: { currentTitle: 'Analyst', skills: [], highlights: [] },
        provenance: { source: 'manual', confirmedAt: '2026-10-01T00:00:00Z' },
        createdAt: '2026-10-01T00:00:00Z',
        updatedAt: '2026-10-01T00:00:00Z',
      });
    if (url === '/api/career/goals') return send(options.goal === undefined ? goal : options.goal);
    if (url === '/api/career/market-briefs') {
      return send({
        briefs: [
          {
            id: BRIEF_ID,
            occupationCode: '15-1252.00',
            occupationTitle: 'Software Developers',
            areaTitle: DEN.areaTitle,
            status: 'complete',
            stale: brief.stale,
            createdAt: '2026-10-05T10:00:00Z',
          },
        ],
      });
    }
    if (url === `/api/career/market-briefs/${BRIEF_ID}`) return send(brief);
    if (url === '/api/career/runs' && method === 'POST') {
      state.startBody = route.request().postDataJSON();
      state.key = route.request().headers()['idempotency-key'] ?? null;
      if (options.startFailure) return fail(options.startFailure.status, options.startFailure.code);
      return send(runDto('queued', 0), 202);
    }
    if (url === `/api/career/runs/${RUN_ID}` && method === 'GET') {
      state.gets++;
      if (state.gets === 1) return send(runDto('queued', 0));
      if (state.gets === 2) return send(runDto('working', 2));
      if (options.runFails)
        return send(runDto('failed', 1, { errorCode: 'CareerReferenceUnavailable' }));
      return send(runDto('completed', 5, { marketBriefId: BRIEF_ID }));
    }
    if (url.startsWith('/api/career/')) return send([]);
    return route.continue();
  });
  return state;
}

export async function open(page: Page, query = '') {
  await page.goto(`/app/career/market?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Career market brief' })).toBeVisible();
}

/** Every figure in the fixture must be on the page, as the formatter says it reads. */
export async function expectAllFigures(page: Page, brief: MarketBriefDto) {
  for (const section of brief.sections) {
    for (const f of section.figures) {
      const id = `${section.key}:${f.key}:${f.areaCode}`;
      await expect(page.locator(`[data-figure="${id}"]`)).toHaveText(formatFigure(f));
    }
    for (const item of section.items) {
      for (const f of item.figures) {
        const id = `${section.key}:${item.code}:${f.key}:${f.areaCode}`;
        await expect(page.locator(`[data-figure="${id}"]`)).toHaveText(formatFigure(f));
      }
    }
  }
}
