// Shared populated-state mocks; imported by the feature spec and the axe contrast gate.
import { test, expect, Page } from '@playwright/test';
import { formatFigure } from '../../src/app/pages/career/market-format';
import type {
  MarketFigure,
  PayAnalysisDto,
  PayBenchmarkSection,
} from '../../src/app/services/career-profile.service';

export const figure = (
  key: string,
  label: string,
  value: number,
  areaCode = '99',
  areaTitle = 'U.S.'
): MarketFigure => ({
  key,
  label,
  value,
  areaCode,
  areaTitle,
  unit: 'usd_per_year',
  status: 'available',
  sourceId: 'oews',
});
export const benchmark: PayBenchmarkSection = {
  key: 'benchmark',
  title: 'Occupational benchmark',
  status: 'complete',
  reason: null,
  label: 'Occupational wage benchmark',
  note: 'Published BLS wages, not advertised pay and not a prediction.',
  figures: [
    figure('medianAnnual', 'Median annual wage', 135980),
    figure('pct25Annual', '25th percentile annual wage', 105210),
    figure('pct75Annual', '75th percentile annual wage', 171980),
    figure('medianAnnual', 'Median annual wage', 137610, '19740', 'Denver-Aurora-Centennial, CO'),
    figure(
      'pct25Annual',
      '25th percentile annual wage',
      123640,
      '19740',
      'Denver-Aurora-Centennial, CO'
    ),
    figure(
      'pct75Annual',
      '75th percentile annual wage',
      171350,
      '19740',
      'Denver-Aurora-Centennial, CO'
    ),
  ],
};
export const gates = Array.from({ length: 8 }, (_, i) => ({
  gateId: `G${i + 1}`,
  requirement: `Written right ${i + 1}`,
  status: 'Unverified',
  evidence: 'No agreement',
}));
export const analysis: PayAnalysisDto = {
  id: 'pay-1',
  runId: 'run-1',
  status: 'complete',
  occupation: {
    code: '15-1252.00',
    title: 'Software Developers',
    publishedCode: '15-1252',
    mapping: 'exact',
  },
  location: {
    input: 'Denver, CO',
    resolution: 'metro',
    local: { code: '19740', title: 'Denver-Aurora-Centennial, CO', type: 'metro' },
  },
  pinned: {
    profileVersion: 3,
    goalVersion: 2,
    oewsRelease: '2025-05',
    oewsSnapshotSha256: 'snapshot',
    projectionsRelease: '2025-2035',
    ruleVersion: 'candidate-1.0',
    observationSourceId: null,
  },
  inputHash: 'abcdef0123456789',
  stale: false,
  staleReasons: [],
  blockedReasons: ['provider_rights_unverified'],
  qualification: { personalizedAllowed: false, gates },
  sections: [
    benchmark,
    {
      key: 'personalized',
      title: 'Advertised pay from a qualified cohort',
      status: 'unavailable',
      reason: 'provider_rights_unverified',
      interval: null,
      cohort: {
        included: 0,
        excluded: 0,
        employers: 0,
        largestEmployerShare: 0,
        concentrated: false,
        sensitive: false,
        exclusionReasons: {},
      },
      note: 'No qualified source: no provider has granted written rights for ongoing commercial use.',
    },
    {
      key: 'scenario',
      title: 'Your requested pay',
      status: 'complete',
      requestedAnnual: 150000,
      benchmarkMedianAnnual: 135980,
      gapAnnual: 14020,
      gapPercent: 10.3,
      benchmarkAreaCode: '19740',
      benchmarkAreaTitle: 'Denver-Aurora-Centennial, CO',
      requestedPaySource: 'minimum',
      note: 'Your target is a preference, not evidence about what employers pay.',
    },
  ],
  sources: [
    {
      id: 'oews',
      name: 'OEWS May 2025',
      publisher: 'U.S. Bureau of Labor Statistics',
      referencePeriod: '2025-05',
      publishedOn: '2026-05-15',
      url: 'https://www.bls.gov/oes/',
      definitionsUrl: 'https://www.bls.gov/oes/oes_ques.htm',
      license: 'Public domain',
      citation: 'BLS OEWS 2025',
      definition: 'Wages before taxes',
      coverage: 'Nonfarm wage and salary jobs',
    },
  ],
  createdAt: '2026-10-05T10:00:00Z',
};
export const labels = [
  'Read your career goal',
  'Looked up the occupational benchmark',
  'Evaluated advertised-pay observations',
  'Compared your requested pay',
  'Saved your pay analysis',
];
export const insufficient = {
  ...analysis,
  sections: analysis.sections.map(s =>
    s.key === 'personalized'
      ? {
          ...s,
          status: 'insufficient_evidence' as const,
          reason: 'insufficient_observations',
          cohort: {
            included: 4,
            excluded: 6,
            employers: 2,
            largestEmployerShare: 0.42,
            concentrated: true,
            sensitive: true,
            exclusionReasons: {
              'duplicate requisition': 2,
              'different geography': 3,
              'work-location ineligible': 1,
            },
          },
        }
      : s
  ),
} as PayAnalysisDto;
export async function mock(
  page: Page,
  options: {
    stale?: boolean;
    divergent?: boolean;
    missing?: boolean;
    insufficient?: boolean;
  } = {}
) {
  let polls = 0;
  let startBody: unknown;
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    const send = (data: unknown) =>
      route.fulfill({ json: { success: true, isAuthenticated: true, data } });
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/goals')
      return send({
        occupation: options.missing ? null : { code: '15-1252.00' },
        goal: { targetRole: 'Developer' },
      });
    if (path === '/api/career/profile')
      return send({
        facts: { currentTitle: 'Developer', skills: [], highlights: [] },
        provenance: { source: 'manual', confirmedAt: analysis.createdAt },
      });
    if (path === '/api/career/pay/qualification')
      return send({ personalizedAllowed: false, blockedReasons: analysis.blockedReasons, gates });
    if (path === '/api/career/pay-analyses')
      return send({
        analyses: [
          {
            id: analysis.id,
            occupationTitle: 'Software Developers',
            areaTitle: analysis.location.local?.title,
            stale: options.stale ?? false,
            createdAt: analysis.createdAt,
          },
        ],
      });
    if (path === '/api/career/pay-analyses/pay-1/recompute')
      return send({
        matches: !options.divergent,
        inputHash: analysis.inputHash,
        storedInputHash: analysis.inputHash,
        differences: options.divergent ? ['benchmark.medianAnnual'] : [],
        sections: [],
      });
    if (path === '/api/career/pay-analyses/pay-1')
      return send({
        ...(options.insufficient ? insufficient : analysis),
        stale: options.stale ?? false,
      });
    if (path === '/api/career/runs' && route.request().method() === 'POST') {
      startBody = route.request().postDataJSON();
      return send({ id: 'run-1', status: 'queued', steps: [] });
    }
    if (path === '/api/career/runs/run-1') {
      polls++;
      const count = polls === 1 ? 0 : polls === 2 ? 2 : 5;
      return send({
        id: 'run-1',
        status: count === 5 ? 'completed' : 'working',
        updatedAt: analysis.createdAt,
        payAnalysisId: count === 5 ? analysis.id : null,
        steps: labels.slice(0, count).map((label, i) => ({ ordinal: i + 1, label })),
      });
    }
    return send([]);
  });
  return () => startBody;
}
export async function open(page: Page, query = '') {
  await page.goto(`/app/career/pay?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(
    page.getByRole('heading', { level: 1, name: 'Comparable pay analysis' })
  ).toBeVisible();
}
