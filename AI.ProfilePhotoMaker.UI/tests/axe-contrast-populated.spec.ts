import AxeBuilder from '@axe-core/playwright';
import { test, expect, Page } from '@playwright/test';
import { mock as mockJourney, J } from './fixtures/career-journey';
import { fakeBackend } from './fixtures/career-profile';
import { mock as mockMaterials, open as openMaterials } from './fixtures/career-materials-exports';
import { mock as mockRoadmap, open as openRoadmap } from './fixtures/career-roadmap';
import { mock as mockPay, open as openPay } from './fixtures/career-pay-analysis';
import {
  mockBackend as mockBrief,
  open as openBrief,
  BRIEF_ID,
} from './fixtures/career-market-brief';
import { mock as mockMarkets, open as openMarkets } from './fixtures/career-market-comparison';
import { mock as mockJobs, open as openJobs } from './fixtures/career-job-observations';

// CI contrast gate, populated states: the signed-in pages with realistic data (the same
// fixtures the feature specs use), so axe measures cards, figures, tables and chips rather
// than empty states. Each case first asserts that the populated content rendered.
type Case = { name: string; run: (page: Page) => Promise<void> };

const IMAGE = (id: number) => ({
  id,
  originalImageUrl: `/uploads/${id}.jpg`,
  processedImageUrl: `/uploads/${id}.jpg`,
  style: 'linkedin',
  isGenerated: true,
  createdAt: '2026-10-01T00:00:00Z',
});

const CASES: Case[] = [
  {
    name: 'gallery with photos',
    run: async page => {
      await page.route('**/uploads/**', r =>
        r.fulfill({
          contentType: 'image/svg+xml',
          body: '<svg xmlns="http://www.w3.org/2000/svg" width="8" height="8"><rect width="8" height="8" fill="#888"/></svg>',
        })
      );
      await page.route('**/api/**', route => {
        const url = new URL(route.request().url()).pathname;
        const data = url.endsWith('/image/images')
          ? { images: [1, 2, 3, 4].map(IMAGE), totalImages: 4 }
          : url.endsWith('/config/client')
            ? { features: { careerWorkspace: true } }
            : {};
        return route.fulfill({ json: { success: true, isAuthenticated: true, data, error: null } });
      });
      await page.goto('/app/gallery?e2eAuthBypass=1');
      await expect(page.locator('app-photo-gallery')).toBeVisible();
    },
  },
  {
    name: 'career home with a goal',
    run: async page => {
      await mockJourney(page, [J()]);
      await page.goto('/app/career?e2eAuthBypass=1');
      await expect(page.getByText('Software Developers').first()).toBeVisible();
    },
  },
  {
    name: 'completed profile',
    run: async page => {
      await fakeBackend(page);
      await page.goto('/app/career/profile?e2eAuthBypass=1');
      await expect(page.locator('form, [data-profile]').first()).toBeVisible();
    },
  },
  {
    name: 'materials',
    run: async page => {
      await mockMaterials(page);
      await openMaterials(page, '/app/career/materials?e2eAuthBypass=1', 'Career materials');
    },
  },
  {
    name: 'roadmap with three options',
    run: async page => {
      await mockRoadmap(page);
      await openRoadmap(page, '&roadmap=rm-1');
      await expect(page.locator('[data-option]')).toHaveCount(3);
    },
  },
  {
    name: 'pay analysis',
    run: async page => {
      await mockPay(page);
      await openPay(page, '&analysis=pay-1');
      await expect(page.locator('[data-figure="benchmark:medianAnnual:99"]')).toHaveText(
        '$135,980'
      );
    },
  },
  {
    name: 'market brief',
    run: async page => {
      await mockBrief(page);
      await openBrief(page, `&brief=${BRIEF_ID}`);
      await expect(page.locator('[data-figure="wages:medianAnnual:99"]')).toHaveText('$135,980');
    },
  },
  {
    name: 'market comparison',
    run: async page => {
      await mockMarkets(page);
      await openMarkets(page);
      await expect(page.locator('[data-intro]')).toBeVisible();
    },
  },
  {
    name: 'job observations',
    run: async page => {
      await mockJobs(page);
      await openJobs(page);
      await expect(page.locator('[data-observation]').first()).toBeVisible();
    },
  },
];

for (const theme of ['light', 'dark'] as const) {
  for (const width of [1280, 390]) {
    for (const c of CASES) {
      test(`populated ${c.name}: no color-contrast violations (${theme}, ${width})`, async ({
        page,
      }) => {
        await page.route('**/*', route => {
          const host = new URL(route.request().url()).hostname;
          return host === 'localhost' || host === '127.0.0.1' ? route.continue() : route.abort();
        });
        await page.addInitScript(t => {
          localStorage.setItem('theme', t);
          localStorage.setItem('e2eAuthBypass', 'true');
        }, theme);
        await page.setViewportSize({ width, height: 900 });
        await c.run(page);
        await page.waitForTimeout(600);
        const result = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();
        const failures = result.violations.flatMap(v =>
          v.nodes.map(n => `${n.target.join(' ')} :: ${n.any[0]?.message ?? ''}`)
        );
        expect(failures).toEqual([]);
      });
    }
  }
}
