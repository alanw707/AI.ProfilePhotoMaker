// Shared populated-state mocks; imported by the feature spec and the axe contrast gate.
import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

export const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
export const HOME = '/app/career?e2eAuthBypass=1';
export const J = (over: Record<string, unknown> = {}) => ({
  profile: { version: 1, confirmed: true },
  goal: {
    version: 1,
    occupationCode: '15-1252.00',
    occupationTitle: 'Software Developers',
    location: 'Denver, CO',
  },
  nextAction: { key: 'none', route: '/app/career' },
  latestResult: null,
  activeRuns: [],
  stale: [],
  ...over,
});
export const at = '2026-10-05T10:00:00Z';
export const sparsePay = {
  id: 'pay-1',
  runId: 'r',
  status: 'complete',
  occupation: {
    code: '15-1252.00',
    title: 'Software Developers',
    publishedCode: '15-1252',
    mapping: 'exact',
  },
  location: { input: 'Denver, CO', resolution: 'metro', local: null },
  pinned: {
    profileVersion: 1,
    goalVersion: 1,
    oewsRelease: '2025-05',
    oewsSnapshotSha256: 's',
    projectionsRelease: '',
    ruleVersion: 'v',
    observationSourceId: null,
  },
  inputHash: 'abcdef0123456789',
  stale: false,
  staleReasons: [],
  blockedReasons: [],
  qualification: { personalizedAllowed: true, gates: [] },
  sections: [
    {
      key: 'benchmark',
      title: 'b',
      status: 'complete',
      reason: null,
      label: 'Benchmark',
      note: 'n',
      figures: [],
    },
    {
      key: 'personalized',
      title: 'p',
      status: 'insufficient_evidence',
      reason: 'insufficient_observations',
      interval: null,
      note: '',
      cohort: {
        included: 2,
        excluded: 0,
        employers: 1,
        largestEmployerShare: 1,
        concentrated: true,
        sensitive: false,
        exclusionReasons: {},
      },
    },
  ],
  sources: [],
  createdAt: at,
};

export async function mock(page: Page, journeys: unknown[]) {
  const calls = { writes: [] as string[], idx: 0, journeyGets: 0 };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown) =>
      route.fulfill({ json: { success: true, isAuthenticated: true, data } });
    if (!['GET', 'HEAD'].includes(req.method())) calls.writes.push(`${req.method()} ${path}`);
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/journey') {
      calls.journeyGets++;
      return send(journeys[Math.min(calls.idx, journeys.length - 1)]);
    }
    if (path === '/api/career/goals')
      return send({ occupation: { code: '15-1252.00' }, goal: { targetRole: 'Developer' } });
    if (path === '/api/career/profile')
      return send({
        facts: { currentTitle: 'Developer', skills: [], highlights: [] },
        provenance: { source: 'manual', confirmedAt: at },
      });
    if (path.startsWith('/api/career/pay-analyses')) return send(sparsePay);
    return send([]);
  });
  return calls;
}
export async function open(page: Page, url = HOME) {
  await page.goto(url);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { name: 'Career workspace' })).toBeVisible();
}

export const steps: [string, Record<string, unknown>, string, RegExp][] = [
  [
    'create_profile',
    { profile: null, goal: null },
    '/app/career/setup',
    /Set up your career workspace/,
  ],
  ['set_goal', { goal: null }, '/app/career/setup', /Set up your career workspace/],
  ['build_brief', {}, '/app/career/market', /Career market brief/],
  ['analyze_pay', {}, '/app/career/pay', /Comparable pay analysis/],
  ['build_roadmap', {}, '/app/career/roadmap', /Your career roadmap/],
  ['draft_resume', {}, '/app/career/resume', /Your targeted resume/],
  ['export_material', {}, '/app/career/materials', /Career materials/],
];
