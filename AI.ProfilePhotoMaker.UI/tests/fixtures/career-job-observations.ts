// Shared populated-state mocks; imported by the feature spec and the axe contrast gate.
import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import type {
  JobObservation,
  JobObservations,
} from '../../src/app/services/career-profile.service';

export const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
export const MALICIOUS = '<img src=x onerror=alert(1)>';

export const observation = (id: string, over: Partial<JobObservation> = {}): JobObservation => ({
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
export const multi = observation('2', {
  title: 'Developer in two places',
  multiLocation: true,
  locations: [
    { city: 'Denver', state: 'CO', areaCode: '19740', match: 'user_area' },
    { city: 'Colorado Springs', state: 'CO', areaCode: null, match: 'other' },
  ],
  pay: { min: 40, max: 55.5, unit: 'usd_per_hour', basis: 'hourly', status: 'available' },
});
export const unknownRemote = observation('3', {
  title: 'Analyst with no remote statement',
  remoteEligibility: 'unknown',
  pay: { min: null, max: null, unit: 'usd_per_year', basis: 'annual', status: 'not_available' },
});
export const all = [observation('1', { title: 'Remote developer' }), multi, unknownRemote];

export interface Options {
  reason?: 'source_not_configured' | 'source_unavailable' | 'occupation_required';
  /** No confirmed occupation: the API searched by the goal's target role. */
  byTargetRole?: string;
  /** The API widened the search: keyword without seniority words, then statewide. */
  broadened?: { keyword: string; areaTitle?: string };
  stale?: boolean;
  titles?: string[];
}
export function payload(url: URL, options: Options): JobObservations {
  const eligibleOnly = url.searchParams.get('eligibleOnly') === 'true';
  const q = (url.searchParams.get('q') ?? '').toLowerCase();
  let list = options.titles
    ? options.titles.map((title, i) => observation(`m${i}`, { title }))
    : all;
  list = list.filter(o => o.title.toLowerCase().includes(q));
  const unknown = list.filter(o => o.remoteEligibility === 'unknown').length;
  if (eligibleOnly) list = list.filter(o => o.remoteEligibility === 'eligible');
  const available = !options.reason;
  const shown = available ? list : [];
  const noOccupation =
    !!options.byTargetRole || !!options.broadened || options.reason === 'occupation_required';
  return {
    occupation: noOccupation ? null : { code: '15-1252.00', title: 'Software Developers' },
    search: options.broadened
      ? {
          basis: 'target_role',
          keyword: options.broadened.keyword,
          areaTitle: options.broadened.areaTitle ?? 'Denver-Aurora-Centennial, CO',
          broadened: options.broadened.areaTitle ? ['keyword', 'area'] : ['keyword'],
        }
      : options.byTargetRole
        ? { basis: 'target_role', keyword: options.byTargetRole }
        : noOccupation
          ? null
          : { basis: 'occupation', keyword: 'Software Developers' },
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
      postedFrom: available ? '2026-09-20' : null,
      postedTo: available ? '2026-09-28' : null,
      counts: {
        fetched: shown.length + (eligibleOnly ? unknown : 0) + 4,
        matched: shown.length,
        shown: shown.length,
        duplicateIds: 0,
        duplicateReposts: 0,
        expired: 0,
        remoteUnknownExcluded: eligibleOnly ? unknown : 0,
        remoteIneligibleExcluded: 0,
        otherLocationExcluded: available ? 4 : 0,
        keywordExcluded: 0,
        remoteFilterExcluded: 0,
        cappedByLimit: 0,
      },
    },
    preferences: { areaCode: '19740', stalePreference: !!options.stale, note: null },
    observations: shown,
    truncated: false,
    note: 'Postings are observations, not employment totals or an outlook.',
  };
}

export async function mock(page: Page, options: Options = {}) {
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
export async function open(page: Page, query = '') {
  await page.goto(`/app/career/jobs?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Open postings' })).toBeVisible();
}
export const cards = (page: Page) => page.locator('[data-observation]');
