const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const projectRoot = path.resolve(__dirname, '..', '..');
const uiRoot = path.join(projectRoot, 'AI.ProfilePhotoMaker.UI');
const publicRoot = path.join(uiRoot, 'public');
const distRoot = path.join(uiRoot, 'dist', 'ai.profile-photo-maker.ui');
const distBrowserRoot = path.join(distRoot, 'browser');
const distIndexPath = path.join(distRoot, 'index.html');
const distBrowserIndexPath = path.join(distBrowserRoot, 'index.html');
const distIndexCandidate = fs.existsSync(distBrowserIndexPath)
  ? distBrowserIndexPath
  : distIndexPath;
const outputRoot = fs.existsSync(distIndexCandidate) ? path.dirname(distIndexCandidate) : publicRoot;
const compareRoot = path.join(outputRoot, 'compare');

const pages = [
  {
    slug: 'aragon-ai',
    title: 'AI Profile Photo Maker vs Aragon AI',
  },
  {
    slug: 'headshotpro',
    title: 'AI Profile Photo Maker vs HeadshotPro',
  },
];

test.describe('SEO static compare pages', () => {
  for (const pageInfo of pages) {
    test(`static ${pageInfo.slug} page has expected metadata`, async ({ page }) => {
      const filePath = path.join(compareRoot, pageInfo.slug, 'index.html');
      const fileUrl = `file://${filePath}`;

      await page.goto(fileUrl);
      await expect(page.locator('h1')).toHaveText(pageInfo.title);
      await expect(page.locator('link[rel=\"canonical\"]')).toHaveAttribute(
        'href',
        `https://app.aiprofilephotomaker.com/compare/${pageInfo.slug}`
      );
    });
  }
});
