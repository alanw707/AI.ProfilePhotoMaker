import { test, expect, Page } from '@playwright/test';

function fakeBackend(page: Page, enabled = true) {
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
async function setup(page: Page) {
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
test('setup saves profile and goal; reload restores both', async ({ page }) => {
  fakeBackend(page);
  await setup(page);
  await page.reload();
  await expect(page.getByText('Data analyst')).toBeVisible();
  await expect(page.getByText('Senior analyst')).toBeVisible();
});
test('editing profile marks goal stale', async ({ page }) => {
  fakeBackend(page);
  await setup(page);
  await page.getByRole('link', { name: 'View and edit profile' }).click();
  await page.getByLabel('Current title').fill('Principal analyst');
  await page.getByLabel('I confirm these facts are accurate').first().check();
  await page.getByRole('button', { name: 'Save professional facts' }).click();
  await expect(page.getByText('Professional facts saved.')).toBeVisible();
  await page.getByRole('link', { name: 'Back to career workspace' }).click();
  await expect(page.getByText(/Needs review/)).toBeVisible();
});
test('stale update keeps unsaved input until reload', async ({ page }) => {
  const backend = fakeBackend(page);
  await setup(page);
  await page.goto('/app/career/profile?e2eAuthBypass=1');
  await page.getByLabel('Current title').fill('My unsaved title');
  await page.getByLabel('I confirm these facts are accurate').first().check();
  backend.conflict();
  await page.getByRole('button', { name: 'Save professional facts' }).click();
  await expect(page.getByText(/changed in another tab or device/)).toBeVisible();
  await expect(page.getByLabel('Current title')).toHaveValue('My unsaved title');
  await expect(page.getByRole('button', { name: 'Reload the latest version' })).toBeVisible();
});
test('empty title has summary and invalid control', async ({ page }) => {
  fakeBackend(page);
  await page.goto('/app/career/setup?e2eAuthBypass=1');
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await page.getByRole('button', { name: 'Save facts and continue' }).click();
  await expect(page.locator('[role=alert]')).toContainText('Current title: Required.');
  await expect(page.locator('[role=alert]')).not.toContainText('currentTitle');
  await expect(page.getByLabel('Current title')).toHaveAttribute('aria-invalid', 'true');
});
test('disabled career redirects but photo routes remain available', async ({ page }) => {
  fakeBackend(page, false);
  await page.goto('/app/career?e2eAuthBypass=1');
  await expect(page).toHaveURL(/\/app\/enhance/);
  await page.goto('/app/gallery?e2eAuthBypass=1');
  await expect(page).toHaveURL(/\/app\/gallery/);
});
test('restore older version appends a new version', async ({ page }) => {
  fakeBackend(page);
  await setup(page);
  await page.goto('/app/career/profile?e2eAuthBypass=1');
  await page.getByLabel('Current title').fill('Second title');
  await page.getByLabel('I confirm these facts are accurate').first().check();
  await page.getByRole('button', { name: 'Save professional facts' }).click();
  await page.getByRole('button', { name: 'View version 1' }).click();
  await page.getByRole('button', { name: 'Restore this version' }).click();
  await expect(page.getByLabel('Current title')).toHaveValue('Data analyst');
  await expect(page.getByRole('button', { name: 'View version 3' })).toBeVisible();
});
test('profile fits a 320px viewport', async ({ page }) => {
  fakeBackend(page);
  await page.setViewportSize({ width: 320, height: 700 });
  await setup(page);
  await page.goto('/app/career/profile?e2eAuthBypass=1');
  await expect(page.getByRole('heading', { name: 'Your professional profile' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
});
