// Shared populated-state mocks; imported by the feature spec and the axe contrast gate.
import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import type { RoadmapDto, RoadmapOption } from '../../src/app/services/career-profile.service';

export const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
export const createdAt = '2026-10-05T10:00:00Z';
export const labels = [
  'Read your career goal',
  'Read your market and pay evidence',
  'Compared possible paths',
  'Planned tasks around your available time',
  'Saved your roadmap',
];
export const option = (key: RoadmapOption['key'], title: string): RoadmapOption => ({
  key,
  occupationCode: key === 'closest_fit' ? '15-1252.00' : '15-2051.00',
  title,
  rationale: [
    { text: 'Median annual wage is $135,980.', sourceId: 'oews', release: '2025-05' },
    { text: 'Employment is projected to grow 17%.', sourceId: 'projections', release: '2025-2035' },
  ],
  assumptions: ['You can spend your weekly hours on this.'],
  missingEvidence: ['No advertised pay from a qualified source yet.'],
  timelineNote: 'A scenario, not a promise.',
  thisWeek: [
    { id: 't1', title: 'List three projects you could show', effortHours: 2, dependsOn: [] },
    { id: 't2', title: 'Write one project summary', effortHours: 3, dependsOn: ['t1'] },
  ],
  milestones: [
    {
      day: 30,
      tasks: [
        { id: 't3', title: 'Share the summary with a peer', effortHours: 1, dependsOn: ['t2'] },
      ],
    },
    { day: 60, tasks: [] },
    { day: 90, tasks: [] },
  ],
});
export const three: RoadmapDto = {
  id: 'rm-1',
  version: 1,
  status: 'proposed',
  selectedOption: null,
  pinned: {
    profileVersion: 3,
    goalVersion: 2,
    occupationCode: '15-1252.00',
    marketBriefId: null,
    payAnalysisId: null,
  },
  stale: false,
  staleReasons: [],
  weeklyEffortHours: 6,
  options: [
    option('closest_fit', 'Software Developers'),
    option('higher_ambition', 'Data Scientists'),
    option('steadier_transition', 'Database Administrators'),
  ],
  omittedOptions: [],
  lowTimeNote: null,
};
export const one: RoadmapDto = {
  ...three,
  options: [three.options[0]],
  omittedOptions: [
    { key: 'higher_ambition', reason: 'no_supported_alternative' },
    { key: 'steadier_transition', reason: 'no_supported_alternative' },
  ],
};

export interface Opts {
  roadmap?: RoadmapDto;
  stale?: boolean;
  lowTime?: boolean;
  acceptStatus?: number;
  cycle?: boolean;
}
export async function mock(page: Page, o: Opts = {}) {
  let polls = 0;
  let current: RoadmapDto = {
    ...(o.roadmap ?? three),
    stale: o.stale ?? false,
    lowTimeNote: o.lowTime ? 'With under 2 hours a week, the plan keeps one task per week.' : null,
  };
  const calls = {
    accept: [] as { body: unknown; ifMatch: string | undefined }[],
    goals: 0,
    put: [] as unknown[],
    start: undefined as unknown,
  };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: status < 400, isAuthenticated: true, data } });
    const fail = (status: number, code?: string) =>
      route.fulfill({ status, json: { success: false, error: { code, message: code } } });
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/goals') {
      calls.goals++;
      return route.fulfill({
        headers: { ETag: `"goal-v${calls.goals}"` },
        json: {
          success: true,
          isAuthenticated: true,
          data: {
            id: 'g1',
            etag: `"goal-v${calls.goals}"`,
            occupation: { code: '15-1252.00' },
            goal: {},
          },
        },
      });
    }
    if (path === '/api/career/profile')
      return send({ facts: { currentTitle: 'Developer', skills: [], highlights: [] } });
    if (path === '/api/career/roadmaps')
      return send({
        roadmaps: [
          {
            id: 'rm-1',
            version: 1,
            status: current.status,
            stale: current.stale,
            optionCount: current.options.length,
            createdAt,
          },
        ],
      });
    if (path === '/api/career/roadmaps/rm-1/accept') {
      calls.accept.push({ body: req.postDataJSON(), ifMatch: req.headers()['if-match'] });
      if (o.acceptStatus) return fail(o.acceptStatus, 'CareerVersionConflict');
      current = {
        ...current,
        status: 'accepted',
        selectedOption: (req.postDataJSON() as { optionKey: RoadmapOption['key'] }).optionKey,
        goalUnchanged: true,
      };
      return send(current);
    }
    if (path === '/api/career/roadmaps/rm-1/tasks/t1') {
      const body = req.postDataJSON() as { effortHours: number };
      calls.put.push(body);
      if (o.cycle) return fail(409, 'CareerRoadmapCycle');
      current = {
        ...current,
        version: current.version + 1,
        options: current.options.map(op => ({
          ...op,
          thisWeek: op.thisWeek.map(t =>
            t.id === 't1' ? { ...t, effortHours: body.effortHours } : t
          ),
        })),
      };
      return send(current);
    }
    if (path === '/api/career/roadmaps/rm-1') return send(current);
    if (path === '/api/career/runs' && req.method() === 'POST') {
      calls.start = req.postDataJSON();
      return send({ id: 'run-1', status: 'queued', steps: [] });
    }
    if (path === '/api/career/runs/run-1') {
      polls++;
      const count = polls === 1 ? 0 : polls === 2 ? 2 : 5;
      return send({
        id: 'run-1',
        status: count === 5 ? 'completed' : 'working',
        updatedAt: createdAt,
        roadmapId: count === 5 ? 'rm-1' : null,
        steps: labels.slice(0, count).map((label, i) => ({ ordinal: i + 1, label })),
      });
    }
    return send([]);
  });
  return calls;
}
export async function open(page: Page, query = '') {
  await page.goto(`/app/career/roadmap?e2eAuthBypass=1${query}`);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { level: 1, name: 'Your career roadmap' })).toBeVisible();
}
export const overflow = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
