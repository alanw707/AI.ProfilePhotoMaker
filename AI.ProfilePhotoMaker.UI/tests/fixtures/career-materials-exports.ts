// Shared populated-state mocks; imported by the feature spec and the axe contrast gate.
import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

export const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
export const IMAGE =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==';
export const gen = (id: string, text: string, factIds: string[]) => ({
  id,
  text,
  factIds,
  origin: 'generated',
});
export const summary = (o: Record<string, unknown> = {}) => ({
  id: 'sum-1',
  kind: 'summary',
  title: 'Professional summary',
  etag: '"material-v1"',
  currentVersion: 1,
  pinned: { profileVersion: 1, goalVersion: 1, occupationCode: '15-1252.00' },
  stale: false,
  staleReasons: [],
  contact: { name: true, email: true, phone: false, location: false, links: false },
  sections: [
    { key: 'short', lines: [gen('s1', 'Software developer with 5 years of experience.', ['f1'])] },
    {
      key: 'long',
      lines: [
        gen('s2', 'I build reliable web apps. I shipped a billing tool at Acme.', ['f1', 'f2']),
      ],
    },
  ],
  questions: [],
  facts: [
    { id: 'f1', text: 'Worked as a developer for 5 years' },
    { id: 'f2', text: 'Built a billing tool at Acme' },
  ],
  ...o,
});
export const resumeSummary = {
  id: 'res-1',
  kind: 'resume',
  title: 'Targeted resume',
  stale: true,
  currentVersion: 2,
  updatedAt: '2026-10-05T10:00:00Z',
};
export const summaryItem = {
  id: 'sum-1',
  kind: 'summary',
  title: 'Professional summary',
  stale: false,
  currentVersion: 1,
  updatedAt: '2026-10-04T09:00:00Z',
};
export interface Opts {
  photoSelected?: boolean;
  runStatus?: number;
  exportStatus?: number;
  downloadStatus?: number;
  exportCode?: string;
}
export async function mock(page: Page, o: Opts = {}) {
  const calls = { runs: [] as any[], exports: [] as any[], downloads: [] as string[] };
  await page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  await page.route('**/api/**', route => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const send = (data: unknown, status = 200) =>
      route.fulfill({ status, json: { success: status < 400, isAuthenticated: true, data } });
    const fail = (status: number, code?: string) =>
      route.fulfill({ status, json: { success: false, error: { code, message: code } } });
    const now = new Date().toISOString();
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/goals')
      return send({ id: 'g1', etag: '"g1"', occupation: { code: '15-1252.00' }, goal: {} });
    if (path === '/api/career/photos')
      return send({
        photos: [
          {
            id: 42,
            imageUrl: IMAGE,
            createdAt: '2026-09-01T10:00:00Z',
            style: 'linkedin',
            isWatermarkedPreview: false,
          },
        ],
        selectedPhotoId: o.photoSelected ? 42 : null,
        selectedPhotoAvailable: true,
        entitlements: [],
      });
    if (path === '/api/career/materials' && req.method() === 'GET')
      return send({ materials: [resumeSummary, summaryItem] });
    if (path === '/api/career/runs' && req.method() === 'POST') {
      calls.runs.push(req.postDataJSON());
      if (o.runStatus) return fail(o.runStatus, 'CareerAllowanceExhausted');
      return send({ id: 'run-1' }, 201);
    }
    if (path === '/api/career/runs/run-1')
      return send({
        id: 'run-1',
        task: 'professional_summary',
        status: 'completed',
        createdAt: now,
        updatedAt: now,
        completedAt: now,
        steps: [],
        question: null,
        proposalId: null,
        materialId: 'sum-1',
        profileChanged: false,
        errorCode: null,
        allowance: { used: 0, reserved: 0, limit: 10, periodStart: now },
      });
    if (path === '/api/career/materials/sum-1' && req.method() === 'GET') return send(summary());
    if (path === '/api/career/materials/sum-1/versions')
      return send({
        versions: [{ number: 1, author: 'agent', createdAt: '2026-10-04T09:00:00Z' }],
        total: 1,
      });
    if (path === '/api/career/materials/sum-1/exports' && req.method() === 'POST') {
      calls.exports.push(req.postDataJSON());
      if (o.exportStatus) return fail(o.exportStatus, o.exportCode);
      const body = req.postDataJSON();
      const isPdf = body.format === 'pdf';
      return send(
        {
          id: isPdf ? 'e-pdf' : 'e-docx',
          format: body.format,
          version: 1,
          includesPhoto: !!body.includePhoto,
          fileName: isPdf ? 'summary.pdf' : 'summary.docx',
          expiresAt: new Date(Date.now() + 23 * 3600_000).toISOString(),
          downloadUrl: '/api/career/exports/x',
        },
        201
      );
    }
    if (path === '/api/career/materials/sum-1/exports' && req.method() === 'GET')
      return send({
        exports: [
          {
            id: 'e-old',
            format: 'pdf',
            version: 1,
            includesPhoto: false,
            fileName: 'old.pdf',
            expiresAt: new Date(Date.now() + 5 * 3600_000).toISOString(),
            downloadUrl: '/api/career/exports/e-old',
          },
        ],
      });
    if (path.startsWith('/api/career/exports/')) {
      calls.downloads.push(path.split('/').pop()!);
      if (o.downloadStatus) return fail(o.downloadStatus, 'CareerExportExpired');
      return route.fulfill({
        status: 200,
        contentType: path.endsWith('docx') ? 'application/octet-stream' : 'application/pdf',
        body: 'file-bytes',
      });
    }
    return send([]);
  });
  return calls;
}
export async function open(page: Page, url: string, heading: string) {
  await page.goto(url);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible();
}
export const editor = (page: Page) =>
  open(
    page,
    '/app/career/summary-draft?e2eAuthBypass=1&material=sum-1',
    'Your professional summary'
  );
export const overflow = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
