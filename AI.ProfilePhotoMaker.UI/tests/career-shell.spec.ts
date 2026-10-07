import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

// Shared header and career step rail (docs/design/career-shell-brief.md).
const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const journey = {
  profile: { version: 1, confirmed: true },
  goal: {
    version: 1,
    occupationCode: '15-1252.00',
    occupationTitle: 'Software Developers',
    location: 'Denver, CO',
  },
  nextAction: { key: 'analyze_pay', route: '/app/career/pay' },
  latestResult: null,
  activeRuns: [],
  stale: [{ kind: 'market_brief', id: 'b1', reasons: ['goal_changed'] }],
};

async function mock(page: Page, opts: { theme?: 'dark'; journey?: unknown } = {}) {
  await page.addInitScript(theme => {
    localStorage.setItem('e2eAuthBypass', 'true');
    if (theme) localStorage.setItem('theme', theme);
  }, opts.theme);
  await page.route('**/api/**', route => {
    const path = new URL(route.request().url()).pathname;
    const send = (data: unknown) =>
      route.fulfill({ json: { success: true, isAuthenticated: true, data } });
    if (path === '/api/config/client') return send({ features: { careerWorkspace: true } });
    if (path === '/api/career/journey') return send(opts.journey ?? journey);
    return send([]);
  });
}
async function open(page: Page, url = '/app/career?e2eAuthBypass=1') {
  await page.goto(url);
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
}

test('career pages carry the shared header with an active Career link', async ({ page }) => {
  await mock(page);
  await open(page);
  const primary = page.getByRole('navigation', { name: 'Primary navigation' });
  await expect(primary.getByRole('link', { name: 'Career' })).toHaveAttribute(
    'aria-current',
    'page'
  );
  await expect(primary.getByRole('link', { name: 'Photo workspace' })).toBeVisible();
  await expect(page.getByRole('heading', { level: 1, name: 'Career workspace' })).toBeVisible();
});

test('desktop rail shows step states in text and follows navigation', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await mock(page);
  await open(page);
  const rail = page.getByRole('navigation', { name: 'Career steps' });
  await expect(rail.locator('[data-step-state="current"]')).toContainText('Pay analysis');
  await expect(rail.locator('[data-step-state="current"]')).toContainText('Next');
  await expect(rail.locator('[data-step-state="review"]')).toContainText('Market brief');
  await expect(rail.locator('[data-step-state="review"]')).toContainText('Needs review');
  await expect(rail.locator('[data-step-state="done"]')).toHaveCount(3);
  await rail.getByRole('link', { name: /Pay analysis/ }).click();
  await expect(page).toHaveURL(/\/app\/career\/pay/);
  await expect(rail.getByRole('link', { name: /Pay analysis/ })).toHaveAttribute(
    'aria-current',
    'page'
  );
  await expect(
    page
      .getByRole('navigation', { name: 'Primary navigation' })
      .getByRole('link', { name: 'Career' })
  ).toHaveAttribute('aria-current', 'page');
});

test('narrow screens collapse the rail to a step control', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mock(page);
  await open(page);
  const toggle = page.getByRole('button', { name: 'Step 5 of 7: Pay analysis' });
  await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  const list = page.locator('#career-step-list');
  await expect(list).toBeHidden();
  // The next step is reachable without opening the rail.
  await expect(page.locator('[data-next]')).toBeInViewport();
  await toggle.click();
  await expect(list).toBeVisible();
  await list.getByRole('link', { name: /Roadmap/ }).click();
  await expect(page).toHaveURL(/\/app\/career\/roadmap/);
  await expect(list).toBeHidden();
});

for (const theme of [undefined, 'dark'] as const) {
  for (const [w, h] of [
    [1280, 800],
    [320, 640],
  ]) {
    test(`shell axe and no overflow at ${w} (${theme ?? 'light'})`, async ({ page }) => {
      test.skip(!existsSync(AXE_PATH), 'axe-core not installed');
      await page.setViewportSize({ width: w, height: h });
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await mock(page, { theme });
      await open(page);
      await expect(page.locator('[data-next]')).toBeVisible();
      if (w < 900) await page.getByRole('button', { name: /Step 5 of 7/ }).click();
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
        w
      );
      await page.addScriptTag({ path: AXE_PATH });
      const violations = await page.evaluate(async () => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const r = await (window as any).axe.run(document, {
          runOnly: ['wcag2a', 'wcag2aa', 'wcag21aa'],
        });
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return r.violations.map(
          (v: any) => `${v.id}: ${v.nodes.map((n: any) => n.target.join(' ')).join(', ')}`
        );
      });
      expect(violations).toEqual([]);
    });
  }
}

test('without a profile only one rail link is current on setup', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await mock(page, {
    journey: {
      profile: null,
      goal: null,
      nextAction: { key: 'create_profile' },
      latestResult: null,
      activeRuns: [],
      stale: [],
    },
  });
  await open(page, '/app/career/setup?e2eAuthBypass=1');
  const rail = page.getByRole('navigation', { name: 'Career steps' });
  await expect(rail.locator('[aria-current="page"]')).toHaveCount(1);
});

test('closing the narrow rail after navigation keeps focus on the step control', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mock(page);
  await open(page);
  const toggle = page.getByRole('button', { name: /Step 5 of 7/ });
  await toggle.click();
  await page
    .locator('#career-step-list')
    .getByRole('link', { name: /Roadmap/ })
    .focus();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/app\/career\/roadmap/);
  await expect(page.locator('.rail__toggle')).toBeFocused();
});
