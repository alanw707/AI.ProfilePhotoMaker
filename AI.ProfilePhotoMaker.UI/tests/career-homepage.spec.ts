import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';

const AXE_PATH = process.env.AXE_PATH ?? '/tmp/axe/node_modules/axe-core/axe.min.js';
const FORBIDDEN =
  /unlimited|guarantee|all live jobs|live vacancies|comprehensive (live )?(jobs|vacancies)/i;

async function open(page: Page, careerWorkspace: boolean) {
  await page.route('**/api/config/client', route =>
    route.fulfill({
      json: { success: true, data: { features: { careerWorkspace } } },
    })
  );
  await page.route('**/api/**', route =>
    route.request().url().includes('/api/config/client')
      ? route.fallback()
      : route.fulfill({ status: 503, json: { success: false } })
  );
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await expect(page.getByRole('heading', { name: 'Studio', exact: true })).toBeAttached();
  const cookies = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await cookies.isVisible()) await cookies.click();
  await expect(page.locator('h1').first()).toBeVisible();
}

const pageSnapshot = (page: Page) =>
  page.evaluate(() => {
    const root = document.querySelector('.landing-page') as HTMLElement;
    const clone = root.cloneNode(true) as HTMLElement;
    clone.querySelector('[data-career-entry]')?.remove();
    return {
      text: clone.innerText,
      headings: [...clone.querySelectorAll('h1,h2,h3')].map(h => h.textContent?.trim()),
      links: [...clone.querySelectorAll('a')].map(a => a.getAttribute('href')),
    };
  });

test('flag off: no career section; page identical to flag-on page minus the section', async ({
  page,
}) => {
  await open(page, false);
  await expect(page.locator('[data-career-entry]')).toHaveCount(0);
  await expect(page.getByText('Plan my next career move')).toHaveCount(0);
  await expect(page.locator('#career-entry')).toHaveCount(0);
  const off = await pageSnapshot(page);
  expect(off.headings.length).toBeGreaterThan(3);
  expect(off.links.some(l => l?.includes('/auth/register'))).toBe(true);
  expect(off.links.some(l => l?.includes('/app/career'))).toBe(false);
  expect(off.text).not.toMatch(/career/i);

  await page.unroute('**/api/config/client');
  await open(page, true);
  await expect(page.locator('[data-career-entry]')).toHaveCount(1);
  expect(await pageSnapshot(page)).toEqual(off);
});

test('flag on: CTA, copy, photos entry, single h1, no forbidden claims', async ({ page }) => {
  await open(page, true);
  const section = page.locator('[data-career-entry]');
  await expect(section).toBeVisible();
  await expect(section.getByRole('heading', { level: 2 })).toHaveCount(1);
  await expect(page.locator('h1')).toHaveCount(1);
  await expect(section).toContainText('Career planning, now part of AI Profile Photo Maker');
  await expect(section).toContainText('Pay benchmarks from public BLS data');
  await expect(section).toContainText('monthly drafting allowance');
  await expect(section).toContainText('Your data stays private');
  await expect(section).toContainText('Existing pricing covers photos only');
  expect(await section.innerText()).not.toMatch(FORBIDDEN);
  await expect(section.locator('[data-career-photos]')).toBeVisible();
  await expect(page.locator('a[href*="/auth/register"]').first()).toBeAttached();
  const cta = section.getByRole('link', { name: 'Plan my next career move' });
  await expect(cta).toHaveAttribute('href', '/app/career');
  await cta.click();
  await expect(page).toHaveURL(/\/(app\/career|auth\/login)/);
});

test('flag on: CTA reaches /app/career with auth bypass', async ({ page }) => {
  await open(page, true);
  await page.route('**/api/career/**', route =>
    route.fulfill({ json: { success: true, data: null } })
  );
  await page.goto('/app/career?e2eAuthBypass=1');
  await expect(page).toHaveURL(/\/app\/career/);
});

for (const [w, h] of [
  [1280, 800],
  [390, 844],
  [320, 640],
]) {
  for (const theme of ['light', 'dark'])
    test(`axe, contrast and no overflow at ${w} ${theme}`, async ({ page }) => {
      test.skip(!existsSync(AXE_PATH), 'axe-core not installed');
      await page.setViewportSize({ width: w, height: h });
      await page.addInitScript(t => localStorage.setItem('theme', t), theme);
      await open(page, true);
      await page.locator('#career-entry').scrollIntoViewIfNeeded();
      // Regression: the section once had no styles and inherited the hero's white text on a light background.
      const ratio = await page.evaluate(() => {
        const rgb = (c: string) => (c.match(/[\d.]+/g) ?? []).slice(0, 3).map(Number);
        const lum = (c: string) => {
          const [r, g, b] = rgb(c).map(v => {
            v /= 255;
            return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
          });
          return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        };
        const section = document.querySelector('#career-entry') as HTMLElement;
        const bg = lum(getComputedStyle(section).backgroundColor);
        return ['h2', 'p', 'li', '[data-career-photos]'].map(sel => {
          const el = section.querySelector(sel) as HTMLElement;
          const [a, b] = [lum(getComputedStyle(el).color), bg].sort((x, y) => y - x);
          return Number(((a + 0.05) / (b + 0.05)).toFixed(2));
        });
      });
      for (const r of ratio) expect(r).toBeGreaterThanOrEqual(4.5);
      await page.addScriptTag({ path: AXE_PATH });
      const result = await page.evaluate(() =>
        (window as any).axe.run('#career-entry', {
          runOnly: {
            type: 'tag',
            values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
          },
        })
      );
      expect(
        result.violations.map((v: any) => [v.id, v.nodes.map((n: any) => n.target.join(' '))])
      ).toEqual([]);
      if (w === 320)
        expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
          true
        );
    });
}

test('flag-on 404 page does not show the career entry', async ({ page }) => {
  await page.route('**/api/config/client', route =>
    route.fulfill({ json: { success: true, data: { features: { careerWorkspace: true } } } })
  );
  await page.goto('/404');
  await page.waitForLoadState('networkidle');
  await expect(page.locator('[data-career-entry]')).toHaveCount(0);
});
