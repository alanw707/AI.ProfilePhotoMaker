import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { seoPages } from '../src/app/pages/marketing/seo-pages.data';

const BASE_URL = process.env.E2E_BASE_URL ?? 'http://localhost:4200';
const CANONICAL_BASE = process.env.CANONICAL_BASE ?? 'https://aiprofilephotomaker.com';
const STATIC_SOURCE =
  process.env.E2E_STATIC_SOURCE ??
  (BASE_URL.includes('localhost') || BASE_URL.includes('127.0.0.1') ? 'file' : 'http');

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

  for (const pageContent of seoPageEntries) {
    const staticPath = pageContent.slug ? `/${pageContent.slug}/index.html` : '/index.html';
    const expectedCanonical = pageContent.slug
      ? `${CANONICAL_BASE}/${pageContent.slug}`
      : `${CANONICAL_BASE}/`;

    test(`server-rendered HTML includes SEO tags for ${pageContent.slug}`, async ({ request }) => {
      const body = await loadStaticHtml(request, staticPath, pageContent.slug);

      expect(extractTitle(body)).toBe(pageContent.title);
      expect(extractMetaContent(body, 'name', 'description')).toBe(pageContent.description);
      expect(extractMetaContent(body, 'name', 'robots')).toBe('index, follow');
      expect(extractLinkHref(body, 'canonical')).toBe(expectedCanonical);

      const structuredData = extractJsonLd(body, 'seo-structured-data');
      expect(structuredData).not.toBeNull();
      expect(structuredData?.['@type']).toBe('WebPage');
      expect(structuredData?.name).toBe(pageContent.h1);
      expect(structuredData?.url).toBe(expectedCanonical);

      const faqSection = pageContent.sections.find(section => section.type === 'faq') as
        | { items?: unknown[] }
        | undefined;
      const shouldHaveFaq = !!faqSection?.items?.length;

      const faqData = extractJsonLd(body, 'seo-faq-structured-data');
      if (shouldHaveFaq) {
        expect(faqData).not.toBeNull();
      } else {
        expect(faqData).toBeNull();
      }
    });
  }

  test('robots.txt references sitemap and blocks private routes', async ({ request }) => {
    const response = await request.get(`${BASE_URL}/robots.txt`);
    expect(response.ok()).toBe(true);

    const body = await response.text();
    expect(body).toContain('Sitemap: https://aiprofilephotomaker.com/sitemap.xml');
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

function extractTitle(html: string): string | null {
  const match = html.match(/<title>([^<]*)<\/title>/i);
  return match ? decodeHtml(match[1]) : null;
}

async function loadStaticHtml(
  request: { get: (url: string) => Promise<{ ok: () => boolean; text: () => Promise<string> }> },
  staticPath: string,
  slug: string
): Promise<string> {
  if (STATIC_SOURCE === 'http') {
    const response = await request.get(`${BASE_URL}${staticPath}`);
    expect(response.ok()).toBe(true);
    return response.text();
  }

  const slugPath = slug
    .split('/')
    .filter(Boolean)
    .join(path.sep);
  const htmlPath = path.join(__dirname, '..', 'public', slugPath, 'index.html');
  if (!fs.existsSync(htmlPath)) {
    throw new Error(
      `Static HTML not found at ${htmlPath}. Run \"npm run generate:seo-static\" before this test.`
    );
  }
  return fs.readFileSync(htmlPath, 'utf8');
}

function extractMetaContent(html: string, attr: string, key: string): string | null {
  const regex = new RegExp(
    `<meta[^>]*${escapeRegex(attr)}="${escapeRegex(key)}"[^>]*content="([^"]*)"[^>]*>`,
    'i'
  );
  const match = html.match(regex);
  return match ? decodeHtml(match[1]) : null;
}

function extractLinkHref(html: string, rel: string): string | null {
  const regex = new RegExp(
    `<link[^>]*rel="${escapeRegex(rel)}"[^>]*href="([^"]*)"[^>]*>`,
    'i'
  );
  const match = html.match(regex);
  return match ? decodeHtml(match[1]) : null;
}

function extractJsonLd(html: string, id: string): Record<string, unknown> | null {
  const regex = new RegExp(
    `<script[^>]*id="${escapeRegex(id)}"[^>]*type="application/ld\\+json">([\\s\\S]*?)<\\/script>`,
    'i'
  );
  const match = html.match(regex);
  if (!match) {
    return null;
  }
  try {
    return JSON.parse(match[1]);
  } catch {
    return null;
  }
}

function decodeHtml(value: string): string {
  return value
    .replace(/&quot;/g, '"')
    .replace(/&gt;/g, '>')
    .replace(/&lt;/g, '<')
    .replace(/&amp;/g, '&');
}

function escapeRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
