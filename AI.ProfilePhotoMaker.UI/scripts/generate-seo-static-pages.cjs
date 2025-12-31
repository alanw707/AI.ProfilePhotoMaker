const fs = require('fs');
const path = require('path');
const ts = require('typescript');

const projectRoot = path.resolve(__dirname, '..');
const dataPath = path.join(
  projectRoot,
  'src',
  'app',
  'pages',
  'marketing',
  'seo-pages.data.ts'
);
const templatePath = path.join(projectRoot, 'src', 'index.html');
const publicRoot = path.join(projectRoot, 'public');

const CANONICAL_BASE = 'https://app.aiprofilephotomaker.com';
const OG_IMAGE = 'https://app.aiprofilephotomaker.com/assets/og-image.svg';
const TWITTER_IMAGE = 'https://app.aiprofilephotomaker.com/assets/twitter-card.svg';

const template = fs.readFileSync(templatePath, 'utf8');
const seoPages = loadSeoPages(dataPath);

Object.values(seoPages).forEach(page => {
  const slug = String(page.slug || '').replace(/^\/+|\/+$/g, '');
  if (!slug) {
    return;
  }

  const canonicalUrl = `${CANONICAL_BASE}/${slug}`;
  const structuredData = buildStructuredData(page, canonicalUrl);
  const faqItems = collectFaqItems(page);
  const faqStructuredData = faqItems.length > 0 ? buildFaqStructuredData(faqItems) : null;

  let html = template;
  html = replaceTitle(html, page.title);
  html = replaceMeta(html, 'name', 'title', page.title);
  html = replaceMeta(html, 'name', 'description', page.description);
  html = replaceMeta(html, 'name', 'keywords', page.keywords);
  html = replaceMeta(html, 'name', 'robots', 'index, follow', { insertAfter: 'description' });

  html = replaceMeta(html, 'property', 'og:title', page.title);
  html = replaceMeta(html, 'property', 'og:description', page.description);
  html = replaceMeta(html, 'property', 'og:type', 'website');
  html = replaceMeta(html, 'property', 'og:url', canonicalUrl);
  html = replaceMeta(html, 'property', 'og:image', OG_IMAGE);
  html = replaceMeta(html, 'property', 'og:site_name', 'AI Profile Photo Maker');

  html = replaceMeta(html, 'property', 'twitter:card', 'summary_large_image');
  html = replaceMeta(html, 'property', 'twitter:title', page.title);
  html = replaceMeta(html, 'property', 'twitter:description', page.description);
  html = replaceMeta(html, 'property', 'twitter:image', TWITTER_IMAGE);
  html = replaceMeta(html, 'property', 'twitter:url', canonicalUrl);
  html = replaceMeta(html, 'name', 'twitter:creator', '@aiprofilephoto');

  html = replaceCanonical(html, canonicalUrl);
  html = injectStructuredData(html, structuredData, faqStructuredData);
  html = injectStaticBody(html, page.h1, page.description);

  const outputDir = path.join(publicRoot, slug);
  fs.mkdirSync(outputDir, { recursive: true });
  fs.writeFileSync(path.join(outputDir, 'index.html'), html, 'utf8');
});

console.log(`Generated ${Object.keys(seoPages).length} SEO static pages.`);

function loadSeoPages(filePath) {
  const source = fs.readFileSync(filePath, 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
  });

  const moduleShim = { exports: {} };
  const wrapper = new Function('require', 'module', 'exports', outputText);
  wrapper(require, moduleShim, moduleShim.exports);

  if (!moduleShim.exports.seoPages) {
    throw new Error('seoPages export not found in seo-pages.data.ts');
  }
  return moduleShim.exports.seoPages;
}

function buildStructuredData(page, canonicalUrl) {
  return {
    '@context': 'https://schema.org',
    '@type': 'WebPage',
    name: page.h1,
    description: page.description,
    url: canonicalUrl,
    isPartOf: {
      '@type': 'WebSite',
      name: 'AI Profile Photo Maker',
      url: `${CANONICAL_BASE}/`,
    },
    publisher: {
      '@type': 'Organization',
      name: 'AI Profile Photo Maker',
      url: `${CANONICAL_BASE}/`,
    },
  };
}

function collectFaqItems(page) {
  if (!Array.isArray(page.sections)) {
    return [];
  }
  return page.sections
    .filter(section => section.type === 'faq' && Array.isArray(section.items))
    .flatMap(section => section.items);
}

function buildFaqStructuredData(items) {
  return {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    mainEntity: items.map(item => ({
      '@type': 'Question',
      name: item.question,
      acceptedAnswer: {
        '@type': 'Answer',
        text: item.answer,
      },
    })),
  };
}

function replaceTitle(html, value) {
  return html.replace(/<title>[^<]*<\/title>/i, `<title>${escapeHtml(value)}</title>`);
}

function replaceCanonical(html, url) {
  const escaped = escapeHtml(url);
  const canonicalTag = `<link rel="canonical" href="${escaped}" />`;
  if (/<link\s+rel="canonical"[^>]*>/i.test(html)) {
    return html.replace(/<link\s+rel="canonical"[^>]*>/i, canonicalTag);
  }
  return html.replace('</head>', `  ${canonicalTag}\n</head>`);
}

function replaceMeta(html, attrName, attrValue, content, options = {}) {
  const { insertAfter } = options;
  const escapedValue = escapeHtml(attrValue);
  const escapedContent = escapeHtml(content);
  const metaTag = `<meta ${attrName}="${escapedValue}" content="${escapedContent}" />`;
  const regex = new RegExp(`<meta[^>]*${attrName}="${escapeRegExp(attrValue)}"[^>]*>`, 'i');

  if (regex.test(html)) {
    return html.replace(regex, metaTag);
  }

  if (insertAfter) {
    const afterRegex = new RegExp(
      `<meta[^>]*name="${escapeRegExp(insertAfter)}"[^>]*>`,
      'i'
    );
    if (afterRegex.test(html)) {
      return html.replace(afterRegex, match => `${match}\n    ${metaTag}`);
    }
  }

  return html.replace('</head>', `  ${metaTag}\n</head>`);
}

function injectStructuredData(html, structuredData, faqStructuredData) {
  const structuredTag = `<script id="seo-structured-data" type="application/ld+json">${JSON.stringify(
    structuredData
  )}</script>`;
  const faqTag = faqStructuredData
    ? `<script id="seo-faq-structured-data" type="application/ld+json">${JSON.stringify(
        faqStructuredData
      )}</script>`
    : '';
  const combined = faqTag ? `${structuredTag}\n    ${faqTag}` : structuredTag;
  return html.replace('</head>', `  ${combined}\n</head>`);
}

function injectStaticBody(html, h1, description) {
  const safeH1 = escapeHtml(h1);
  const safeDescription = escapeHtml(description);
  const body = `<app-root><main class="seo-static"><h1>${safeH1}</h1><p>${safeDescription}</p></main></app-root>`;
  return html.replace('<app-root></app-root>', body);
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
