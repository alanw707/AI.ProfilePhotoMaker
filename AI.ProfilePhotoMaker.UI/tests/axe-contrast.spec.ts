import AxeBuilder from '@axe-core/playwright';
import { test, expect, Page } from '@playwright/test';

// CI contrast gate: axe color-contrast on public routes in both themes and both widths.
// Every API call is mocked so this runs against `ng serve` alone, with no backend.
const ROUTES = [
  '/',
  '/pricing',
  '/how-it-works',
  '/examples',
  '/compare/aragon-ai',
  '/linkedin-headshots',
  '/blog',
  '/legal/privacy',
  '/auth/login',
  '/auth/register',
  '/this-page-does-not-exist',
];

const PACKAGES = [
  {
    id: 1,
    code: 'free_preview',
    name: 'Free Preview',
    includedCandidateCount: 1,
    price: 0,
    currency: 'USD',
    highlights: ['Watermarked preview'],
  },
  {
    id: 2,
    code: 'starter',
    name: 'Starter',
    includedCandidateCount: 10,
    price: 19,
    currency: 'USD',
    highlights: ['10 headshots', 'Bonus refinements'],
  },
  {
    id: 3,
    code: 'pro',
    name: 'Pro',
    includedCandidateCount: 40,
    price: 39,
    currency: 'USD',
    highlights: ['40 headshots', 'Priority'],
  },
];

async function mockApi(page: Page) {
  await page.route('**/*', route => {
    const host = new URL(route.request().url()).hostname;
    return host === 'localhost' || host === '127.0.0.1' ? route.continue() : route.abort();
  });
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url()).pathname;
    const data = url.endsWith('/profilephotoworkflow/packages')
      ? PACKAGES
      : url.endsWith('/config/client')
        ? { features: {} }
        : {};
    return route.fulfill({ json: { success: true, isAuthenticated: false, data, error: null } });
  });
}

for (const theme of ['light', 'dark'] as const) {
  for (const width of [1280, 390]) {
    test(`no color-contrast violations on public routes (${theme}, ${width})`, async ({ page }) => {
      test.setTimeout(180_000);
      await mockApi(page);
      await page.addInitScript(t => localStorage.setItem('theme', t), theme);
      await page.setViewportSize({ width, height: 900 });
      const failures: string[] = [];
      for (const route of ROUTES) {
        await page.goto(route);
        await page.waitForLoadState('networkidle');
        // Reveal scroll-triggered sections before measuring.
        for (let y = 0; y < 12_000; y += 800) {
          await page.mouse.wheel(0, 800);
          await page.waitForTimeout(40);
        }
        await page.waitForTimeout(600);
        const result = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();
        for (const v of result.violations)
          for (const n of v.nodes) failures.push(`${route} ${n.target.join(' ')}`);
      }
      expect(failures).toEqual([]);
    });
  }
}
