import { test, expect } from '@playwright/test';
import { seoPages } from '../src/app/pages/marketing/seo-pages.data';

const BASE_URL = 'http://localhost:4200';
const CANONICAL_BASE = 'https://app.aiprofilephotomaker.com';

const seoPageEntries = Object.values(seoPages);

test.describe('SEO metadata smoke checks', () => {
  for (const pageContent of seoPageEntries) {
    const routePath = pageContent.slug ? `/${pageContent.slug}` : '/';
    const expectedCanonical = pageContent.slug
      ? `${CANONICAL_BASE}/${pageContent.slug}`
      : `${CANONICAL_BASE}/`;

    test(`has SEO metadata for ${pageContent.slug}`, async ({ page }) => {
      await page.goto(`${BASE_URL}${routePath}`);

      await expect(page.locator('h1')).toHaveText(pageContent.h1);
      await expect(page.locator('meta[name="description"]')).toHaveAttribute(
        'content',
        pageContent.description
      );
      await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'index, follow');
      await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', expectedCanonical);

      const title = await page.title();
      expect(title).toBe(pageContent.title);

      const structuredDataText = await page.locator('script#seo-structured-data').textContent();
      expect(structuredDataText).not.toBeNull();

      const structuredData = JSON.parse(structuredDataText ?? '{}');
      expect(structuredData['@type']).toBe('WebPage');
      expect(structuredData.name).toBe(pageContent.h1);
      expect(structuredData.url).toBe(expectedCanonical);

      const faqSection = pageContent.sections.find(section => section.type === 'faq') as
        | { items?: unknown[] }
        | undefined;
      const shouldHaveFaq = !!faqSection?.items?.length;

      if (shouldHaveFaq) {
        await expect(page.locator('script#seo-faq-structured-data')).toHaveCount(1);
      } else {
        await expect(page.locator('script#seo-faq-structured-data')).toHaveCount(0);
      }
    });
  }

  test('robots.txt references sitemap and blocks private routes', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/robots.txt`);
    expect(response.ok()).toBe(true);

    const body = await response.text();
    expect(body).toContain('Sitemap: https://app.aiprofilephotomaker.com/sitemap.xml');
    expect(body).toContain('Disallow: /app/');
    expect(body).toContain('Disallow: /auth/');
    expect(body).toContain('Disallow: /admin/');
  });

  test('sitemap.xml lists SEO routes', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/sitemap.xml`);
    expect(response.ok()).toBe(true);

    const body = await response.text();
    for (const pageContent of seoPageEntries) {
      const expectedUrl = pageContent.slug
        ? `${CANONICAL_BASE}/${pageContent.slug}`
        : `${CANONICAL_BASE}/`;
      expect(body).toContain(`<loc>${expectedUrl}</loc>`);
    }
  });
});
