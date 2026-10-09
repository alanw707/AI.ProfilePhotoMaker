// Shared populated-state mocks; imported by the feature spec and the axe contrast gate.
import { test, expect, Page } from '@playwright/test';

export function fakeBackend(page: Page, enabled = true) {
  let profile: any = null;
  let goal: any = null;
  let history: any[] = [];
  let conflictNext = false;
  const send = (route: any, data: unknown, status = 200, etag?: string) =>
    route.fulfill({
      status,
      headers: etag ? { ETag: etag } : {},
      json: { success: true, data, error: null },
    });
  const fail = (route: any, status: number, code: string, fieldErrors?: Record<string, string>) =>
    route.fulfill({
      status,
      json: { success: false, error: { code, message: code, fieldErrors } },
    });
  page.addInitScript(() => localStorage.setItem('e2eAuthBypass', 'true'));
  page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    const method = route.request().method();
    if (url === '/api/config/client')
      return send(route, { features: { careerWorkspace: enabled } });
    if (url.startsWith('/api/career/')) {
      if (!enabled) return fail(route, 403, 'CareerWorkspaceDisabled');
      const path = url.slice('/api/career/'.length);
      if (path === 'allowance')
        return send(route, {
          policyVersion: 'v1',
          limit: 20,
          used: 1,
          reserved: 0,
          remaining: 19,
          resetsAt: '2026-11-01T00:00:00Z',
        });
      if (path === 'journey')
        return profile
          ? send(route, {
              profile: { version: profile.version, confirmed: true },
              goal: goal
                ? { version: goal.version, occupationTitle: goal.goal.targetRole, location: null }
                : null,
              nextAction: { key: 'none', route: '/app/career' },
              latestResult: null,
              activeRuns: [],
              stale:
                goal && goal.basedOnProfileVersion !== profile.version
                  ? [{ id: 'm1', kind: 'market_brief' }]
                  : [],
            })
          : fail(route, 404, 'CareerProfileNotFound');
      if (path === 'profile' && method === 'GET')
        return profile
          ? send(route, profile, 200, profile.etag)
          : fail(route, 404, 'CareerProfileNotFound');
      if (path === 'profile' && method === 'PUT') {
        if (conflictNext) {
          conflictNext = false;
          return fail(route, 412, 'CareerVersionConflict');
        }
        const body = route.request().postDataJSON();
        if (!body.currentTitle?.trim() || body.confirmed !== true)
          return fail(route, 400, 'ValidationError', { currentTitle: 'Required.' });
        if (profile && !route.request().headers()['if-match'])
          return fail(route, 428, 'CareerPreconditionRequired');
        if (profile && route.request().headers()['if-match'] !== profile.etag)
          return fail(route, 412, 'CareerVersionConflict');
        const version = history.length + 1;
        const { confirmed, ...facts } = body;
        profile = {
          id: 'profile-id',
          version,
          etag: `"profile-v${version}"`,
          facts,
          provenance: { source: 'manual', confirmedAt: new Date().toISOString() },
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        };
        history.unshift({
          version,
          facts,
          provenance: profile.provenance,
          createdAt: profile.createdAt,
        });
        return send(route, profile, 200, profile.etag);
      }
      if (path === 'profile/versions')
        return send(
          route,
          history.map(v => ({
            ...v,
            currentTitle: v.facts.currentTitle,
            source: 'manual',
            isActive: v.version === profile?.version,
          }))
        );
      const versionPath = path.match(/^profile\/versions\/(\d+)(\/restore)?$/);
      if (versionPath) {
        const version = history.find(v => v.version === Number(versionPath[1]));
        if (!version) return fail(route, 404, 'CareerVersionNotFound');
        if (!versionPath[2])
          return send(route, { ...version, isActive: version.version === profile?.version });
        if (!route.request().headers()['if-match'])
          return fail(route, 428, 'CareerPreconditionRequired');
        if (route.request().headers()['if-match'] !== profile?.etag)
          return fail(route, 412, 'CareerVersionConflict');
        const next = history.length + 1;
        profile = {
          ...profile,
          facts: version.facts,
          version: next,
          etag: `"profile-v${next}"`,
          provenance: { source: 'manual', confirmedAt: new Date().toISOString() },
        };
        history.unshift({
          version: next,
          facts: profile.facts,
          provenance: profile.provenance,
          createdAt: new Date().toISOString(),
        });
        return send(route, profile, 200, profile.etag);
      }
      if (path === 'goals' && method === 'GET')
        return goal
          ? send(
              route,
              { ...goal, isStale: profile && goal.basedOnProfileVersion !== profile.version },
              200,
              goal.etag
            )
          : fail(route, 404, 'CareerGoalNotFound');
      if (
        (path === 'goals' && method === 'POST') ||
        (path.startsWith('goals/') && method === 'PATCH')
      ) {
        if (method === 'POST' && goal) return fail(route, 409, 'CareerGoalAlreadyExists');
        if (method === 'PATCH' && !goal) return fail(route, 404, 'CareerGoalNotFound');
        if (method === 'PATCH' && !route.request().headers()['if-match'])
          return fail(route, 428, 'CareerPreconditionRequired');
        if (method === 'PATCH' && route.request().headers()['if-match'] !== goal.etag)
          return fail(route, 412, 'CareerVersionConflict');
        const body = route.request().postDataJSON();
        if (!body.targetRole?.trim() || body.confirmed !== true)
          return fail(route, 400, 'ValidationError', { targetRole: 'Required.' });
        const { confirmed, ...facts } = body;
        const version = (goal?.version ?? 0) + 1;
        goal = {
          id: 'goal-id',
          version,
          etag: `"goal-v${version}"`,
          goal: facts,
          basedOnProfileVersion: profile?.version ?? null,
          isStale: false,
          provenance: { source: 'manual', confirmedAt: new Date().toISOString() },
        };
        return send(route, goal, method === 'POST' ? 201 : 200, goal.etag);
      }
      if (path === 'goals/goal-id/versions')
        return send(route, [
          {
            version: goal.version,
            targetRole: goal.goal.targetRole,
            isActive: true,
            createdAt: new Date().toISOString(),
          },
        ]);
      return fail(route, 404, 'CareerVersionNotFound');
    }
    const responses: Record<string, unknown> = {
      '/auth/account-status': { emailConfirmed: true },
      '/auth/user-roles': [],
      '/profile': { firstName: 'Test', lastName: 'User' },
      '/credit/status': { credits: 5 },
      '/style': [],
      '/profilephotoworkflow/packages': [],
      '/profilephotoworkflow/entitlements': [],
      '/profilephotoworkflow/export-options': [],
    };
    const key = Object.keys(responses).find(k => url.endsWith(k));
    return send(route, key ? responses[key] : {});
  });
  return {
    conflict: () => {
      conflictNext = true;
    },
  };
}
export async function setup(page: Page) {
  await page.goto('/app/career/setup?e2eAuthBypass=1');
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await page.getByLabel('Current title').fill('Data analyst');
  await page.getByLabel('I confirm these facts are accurate').check();
  await page.getByRole('button', { name: 'Save facts and continue' }).click();
  await expect(page.getByRole('heading', { name: /Step 2/ })).toBeVisible();
  await page.getByLabel('Target role').fill('Senior analyst');
  await page.getByLabel('I confirm these facts are accurate').check();
  await page.getByRole('button', { name: 'Save goal' }).click();
  await expect(page).toHaveURL(/\/app\/career$/);
}
