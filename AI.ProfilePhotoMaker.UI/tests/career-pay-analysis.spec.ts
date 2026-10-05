import { test, expect, Page } from '@playwright/test';
import { formatFigure } from '../src/app/pages/career/market-format';
import type {
  MarketFigure,
  PayAnalysisDto,
  PayBenchmarkSection,
} from '../src/app/services/career-profile.service';

const figure = (
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
const benchmark: PayBenchmarkSection = {
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
const gates = Array.from({ length: 8 }, (_, i) => ({
  gateId: `G${i + 1}`,
  requirement: `Written right ${i + 1}`,
  status: 'Unverified',
  evidence: 'No agreement',
}));
const analysis: PayAnalysisDto = {
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
const labels = [
  'Read your career goal',
  'Looked up the occupational benchmark',
  'Evaluated advertised-pay observations',
  'Compared your requested pay',
  'Saved your pay analysis',
];
const insufficient = {
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
async function mock(
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
async function open(page: Page, query = '') {
  await page.goto(`/app/career/pay?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(
    page.getByRole('heading', { level: 1, name: 'Comparable pay analysis' })
  ).toBeVisible();
}
test('start, real steps, saved figures, gates and scenario', async ({ page }) => {
  const started = await mock(page);
  await open(page);
  await page.getByRole('button', { name: 'Build my pay analysis' }).click();
  expect(started()).toEqual({ task: 'pay_analysis' });
  await expect(page).toHaveURL(/run=run-1/);
  await expect(page.getByText(labels[0])).toBeVisible({ timeout: 10000 });
  await expect(page).toHaveURL(/analysis=pay-1/, { timeout: 15000 });
  for (const f of benchmark.figures)
    await expect(page.locator(`[data-figure="benchmark:${f.key}:${f.areaCode}"]`)).toHaveText(
      formatFigure(f)
    );
  const personalized = page.locator('[data-section="personalized"]');
  await expect(personalized).toContainText(
    'No qualified source: no provider has granted written rights for ongoing commercial use. Nothing here is advertised pay.'
  );
  await expect(personalized).not.toContainText('$');
  for (const gate of gates) await expect(personalized).toContainText(gate.gateId);
  await expect(page.locator('[data-section="scenario"]')).toContainText('$14,020 (10.3%)');
  await expect(page.locator('[data-section="scenario"]')).toContainText('preference, not evidence');
  await expect(page.getByText('Recent analyses')).toBeVisible();
});
test('no machine code leaks to the page', async ({ page }) => {
  await mock(page, { insufficient: true });
  await open(page, '&analysis=pay-1');
  const body = page.locator('[data-analysis]');
  await expect(body).toContainText('42.0% of observations');
  const text = (await body.innerText()).replace(/\s+/g, ' ');
  for (const code of [
    'provider_rights_unverified',
    'insufficient_observations',
    'insufficient_employers',
    'source_unavailable',
    'location_unresolved',
    'not_published',
    'work-location',
    'duplicate requisition',
    'different geography',
    'Unverified',
    'Passed',
  ])
    expect(text).not.toContain(code);
  expect(text).not.toContain('0.42');
  expect(text).not.toContain('USD /');
  const personalized = page.locator('[data-section="personalized"]');
  await expect(personalized).toContainText('2 excluded for a duplicate posting');
  await expect(personalized).toContainText('3 excluded for a different location');
  await expect(personalized).toContainText('1 excluded for not open where you are');
  await expect(personalized).toContainText('Not enough independent current observations yet.');
  await expect(personalized).toContainText(
    'One employer supplies more than 40% of these observations.'
  );
  await expect(personalized).toContainText(
    'The range moves by more than 10% when the largest employer is removed.'
  );
  await expect(page.locator('[data-gates] li')).toHaveCount(8);
  await expect(page.locator('[data-gates]')).toContainText('Not yet verified');
  await expect(page.locator('[data-gates]')).not.toContainText('Failed');
  const scenario = page.locator('[data-section="scenario"]');
  await expect(scenario).toContainText('Benchmark median (Denver-Aurora-Centennial, CO)');
  await expect(scenario).toContainText("your goal's minimum desired pay");
});
test('recompute reports agreement and divergence without altering stored figures', async ({
  page,
}) => {
  await mock(page);
  await open(page, '&analysis=pay-1');
  await page.getByRole('button', { name: 'Recompute' }).click();
  await expect(page.locator('[data-recompute]')).toContainText(
    'Reproduced exactly (input hash abcdef012345)'
  );
  await expect(page.locator('[data-figure="benchmark:medianAnnual:99"]')).toHaveText('$135,980');
});
test('divergence, stale, source drawer and 320px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 800 });
  await mock(page, { stale: true, divergent: true });
  await open(page, '&analysis=pay-1');
  await expect(page.locator('[data-stale]')).toContainText(
    'Your profile or goal changed after this analysis. It is kept as it was; build a new one for current inputs.'
  );
  await page.getByRole('button', { name: 'Recompute' }).click();
  await expect(page.locator('[data-recompute]')).toContainText('disagree: benchmark.medianAnnual');
  await expect(page.locator('[data-figure="benchmark:medianAnnual:99"]')).toHaveText('$135,980');
  await page.getByRole('button', { name: 'Sources', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('BLS OEWS 2025');
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toBeHidden();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth
    )
  ).toBeLessThanOrEqual(0);
  await expect(page.locator('[data-section="personalized"]')).not.toContainText('%');
});
test('direct reload, missing occupation and home link', async ({ page }) => {
  await mock(page);
  await open(page, '&analysis=pay-1');
  await page.reload();
  await expect(page.locator('[data-figure="benchmark:medianAnnual:99"]')).toHaveText('$135,980');
  await page.goto('/app/career?e2eAuthBypass=1');
  await expect(page.getByRole('link', { name: 'Pay analysis' })).toHaveAttribute(
    'href',
    '/app/career/pay'
  );
});
test('missing occupation links to confirmation', async ({ page }) => {
  await mock(page, { missing: true });
  await open(page);
  await expect(page.getByText('Confirm your occupation first.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Confirm your occupation' })).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
});
