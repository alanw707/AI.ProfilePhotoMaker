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
const blogDataPath = path.join(projectRoot, 'src', 'app', 'pages', 'blog', 'blog.data.ts');
const templatePath = path.join(projectRoot, 'src', 'index.html');
const publicRoot = path.join(projectRoot, 'public');
const distRoot = path.join(projectRoot, 'dist', 'ai.profile-photo-maker.ui');
const distBrowserRoot = path.join(distRoot, 'browser');
const distIndexPath = path.join(distRoot, 'index.html');
const distBrowserIndexPath = path.join(distBrowserRoot, 'index.html');

const CANONICAL_BASE = 'https://aiprofilephotomaker.com';
const OG_IMAGE = 'https://aiprofilephotomaker.com/assets/og-image.png?v=2';
const TWITTER_IMAGE = 'https://aiprofilephotomaker.com/assets/twitter-card.png';

const distIndexCandidate = fs.existsSync(distBrowserIndexPath)
  ? distBrowserIndexPath
  : distIndexPath;
const useDistTemplate = fs.existsSync(distIndexCandidate);
const templateSource = useDistTemplate ? distIndexCandidate : templatePath;
const template = fs.readFileSync(templateSource, 'utf8');
const outputRoot = useDistTemplate
  ? path.dirname(distIndexCandidate)
  : publicRoot;
const seoPages = loadSeoPages(dataPath);
const blogPosts = loadBlogPosts(blogDataPath);

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

  html = replaceMeta(html, 'name', 'twitter:card', 'summary_large_image');
  html = replaceMeta(html, 'name', 'twitter:title', page.title);
  html = replaceMeta(html, 'name', 'twitter:description', page.description);
  html = replaceMeta(html, 'name', 'twitter:image', TWITTER_IMAGE);
  html = replaceMeta(html, 'name', 'twitter:url', canonicalUrl);
  html = replaceMeta(html, 'name', 'twitter:creator', '@aiprofilephoto');

  html = replaceCanonical(html, canonicalUrl);
  html = injectStructuredData(html, structuredData, faqStructuredData);
  html = injectStaticBody(html, page.h1, page.description);
  // Ensure CSS/JS/assets resolve from the site root on /slug refreshes.
  html = normalizeAssetUrls(html);

  const outputDir = path.join(outputRoot, slug);
  fs.mkdirSync(outputDir, { recursive: true });
  fs.writeFileSync(path.join(outputDir, 'index.html'), html, 'utf8');
});

const blogListCanonicalUrl = `${CANONICAL_BASE}/blog`;
const blogListTitle = 'Blog - AI Profile Photo Maker';
const blogListDescription =
  'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.';
let blogListHtml = template;
blogListHtml = replaceTitle(blogListHtml, blogListTitle);
blogListHtml = replaceMeta(blogListHtml, 'name', 'title', blogListTitle);
blogListHtml = replaceMeta(blogListHtml, 'name', 'description', blogListDescription);
blogListHtml = replaceMeta(blogListHtml, 'name', 'keywords', 'AI headshots, LinkedIn photos, profile branding, blog');
blogListHtml = replaceMeta(blogListHtml, 'name', 'robots', 'index, follow', {
  insertAfter: 'description',
});
blogListHtml = replaceMeta(blogListHtml, 'property', 'og:title', blogListTitle);
blogListHtml = replaceMeta(blogListHtml, 'property', 'og:description', blogListDescription);
blogListHtml = replaceMeta(blogListHtml, 'property', 'og:type', 'website');
blogListHtml = replaceMeta(blogListHtml, 'property', 'og:url', blogListCanonicalUrl);
blogListHtml = replaceMeta(blogListHtml, 'property', 'og:image', OG_IMAGE);
blogListHtml = replaceMeta(blogListHtml, 'property', 'og:site_name', 'AI Profile Photo Maker');
blogListHtml = replaceMeta(blogListHtml, 'name', 'twitter:card', 'summary_large_image');
blogListHtml = replaceMeta(blogListHtml, 'name', 'twitter:title', blogListTitle);
blogListHtml = replaceMeta(blogListHtml, 'name', 'twitter:description', blogListDescription);
blogListHtml = replaceMeta(blogListHtml, 'name', 'twitter:image', TWITTER_IMAGE);
blogListHtml = replaceMeta(blogListHtml, 'name', 'twitter:url', blogListCanonicalUrl);
blogListHtml = replaceMeta(blogListHtml, 'name', 'twitter:creator', '@aiprofilephoto');
blogListHtml = replaceCanonical(blogListHtml, blogListCanonicalUrl);
blogListHtml = injectStructuredData(
  blogListHtml,
  buildBlogCollectionStructuredData(blogListCanonicalUrl),
  null
);
blogListHtml = injectStaticBody(blogListHtml, 'AI Profile Photo Maker Blog', blogListDescription);
blogListHtml = normalizeAssetUrls(blogListHtml);
const blogListOutputDir = path.join(outputRoot, 'blog');
fs.mkdirSync(blogListOutputDir, { recursive: true });
fs.writeFileSync(path.join(blogListOutputDir, 'index.html'), blogListHtml, 'utf8');

blogPosts.forEach(post => {
  const slug = String(post.slug || '').replace(/^\/+|\/+$/g, '');
  if (!slug) {
    return;
  }

  const routeSlug = `blog/${slug}`;
  const canonicalUrl = `${CANONICAL_BASE}/${routeSlug}`;
  const title = `${post.title} - AI Profile Photo Maker`;
  const description = String(post.description || '').trim();
  const keywords = Array.isArray(post.tags) ? post.tags.join(', ') : 'AI headshots, LinkedIn photos';

  let html = template;
  html = replaceTitle(html, title);
  html = replaceMeta(html, 'name', 'title', title);
  html = replaceMeta(html, 'name', 'description', description);
  html = replaceMeta(html, 'name', 'keywords', keywords);
  html = replaceMeta(html, 'name', 'robots', 'index, follow', { insertAfter: 'description' });
  html = replaceMeta(html, 'property', 'og:title', post.title);
  html = replaceMeta(html, 'property', 'og:description', description);
  html = replaceMeta(html, 'property', 'og:type', 'article');
  html = replaceMeta(html, 'property', 'og:url', canonicalUrl);
  html = replaceMeta(html, 'property', 'og:image', OG_IMAGE);
  html = replaceMeta(html, 'property', 'og:site_name', 'AI Profile Photo Maker');
  html = replaceMeta(html, 'name', 'twitter:card', 'summary_large_image');
  html = replaceMeta(html, 'name', 'twitter:title', post.title);
  html = replaceMeta(html, 'name', 'twitter:description', description);
  html = replaceMeta(html, 'name', 'twitter:image', TWITTER_IMAGE);
  html = replaceMeta(html, 'name', 'twitter:url', canonicalUrl);
  html = replaceMeta(html, 'name', 'twitter:creator', '@aiprofilephoto');
  html = replaceCanonical(html, canonicalUrl);
  html = injectStructuredData(html, buildBlogPostStructuredData(post, canonicalUrl), null);
  html = injectStaticBody(html, post.title, description);
  html = normalizeAssetUrls(html);

  const outputDir = path.join(outputRoot, routeSlug);
  fs.mkdirSync(outputDir, { recursive: true });
  fs.writeFileSync(path.join(outputDir, 'index.html'), html, 'utf8');
});

console.log(
  `Generated ${Object.keys(seoPages).length + blogPosts.length + 1} SEO static pages in ${outputRoot}.`
);

function loadSeoPages(filePath) {
  // Preferred path: evaluate the TS module graph via ts-node so relative imports resolve.
  try {
    const tsNode = require('ts-node');
    tsNode.register({
      transpileOnly: true,
      compilerOptions: {
        module: 'commonjs',
        target: 'es2022',
      },
    });

    const resolvedPath = require.resolve(filePath);
    delete require.cache[resolvedPath];
    const tsModule = require(resolvedPath);
    if (tsModule && tsModule.seoPages) {
      return tsModule.seoPages;
    }
  } catch (error) {
    // Fallback below retains legacy behavior when ts-node is unavailable.
  }

  // Fallback path: transpile and eval a single file.
  const loadedModule = loadTsModule(filePath, new Map());
  if (!loadedModule || !loadedModule.seoPages) {
    throw new Error('seoPages export not found in seo-pages.data.ts');
  }
  return loadedModule.seoPages;
}

function loadBlogPosts(filePath) {
  try {
    const tsNode = require('ts-node');
    tsNode.register({
      transpileOnly: true,
      compilerOptions: {
        module: 'commonjs',
        target: 'es2022',
      },
    });

    const resolvedPath = require.resolve(filePath);
    delete require.cache[resolvedPath];
    const tsModule = require(resolvedPath);
    if (tsModule && Array.isArray(tsModule.blogPosts)) {
      return tsModule.blogPosts;
    }
  } catch (error) {
    // Fallback below retains behavior when ts-node is unavailable.
  }

  const loadedModule = loadTsModule(filePath, new Map());
  if (!loadedModule || !Array.isArray(loadedModule.blogPosts)) {
    throw new Error('blogPosts export not found in blog.data.ts');
  }
  return loadedModule.blogPosts;
}

function loadTsModule(filePath, moduleCache) {
  const resolvedFilePath = path.resolve(filePath);
  if (moduleCache.has(resolvedFilePath)) {
    return moduleCache.get(resolvedFilePath).exports;
  }

  const source = fs.readFileSync(resolvedFilePath, 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
  });

  const moduleShim = { exports: {} };
  moduleCache.set(resolvedFilePath, moduleShim);

  const localRequire = moduleSpecifier => {
    if (!moduleSpecifier.startsWith('.')) {
      return require(moduleSpecifier);
    }

    const localPath = resolveLocalModulePath(path.dirname(resolvedFilePath), moduleSpecifier);
    if (localPath.endsWith('.ts')) {
      return loadTsModule(localPath, moduleCache);
    }
    return require(localPath);
  };

  const wrapper = new Function(
    'require',
    'module',
    'exports',
    '__filename',
    '__dirname',
    outputText
  );
  wrapper(
    localRequire,
    moduleShim,
    moduleShim.exports,
    resolvedFilePath,
    path.dirname(resolvedFilePath)
  );

  return moduleShim.exports;
}

function resolveLocalModulePath(baseDir, moduleSpecifier) {
  const candidateBase = path.resolve(baseDir, moduleSpecifier);
  const candidates = [
    candidateBase,
    `${candidateBase}.ts`,
    `${candidateBase}.js`,
    `${candidateBase}.json`,
    path.join(candidateBase, 'index.ts'),
    path.join(candidateBase, 'index.js'),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }

  throw new Error(`Cannot resolve local module "${moduleSpecifier}" from "${baseDir}"`);
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

function buildBlogCollectionStructuredData(canonicalUrl) {
  return {
    '@context': 'https://schema.org',
    '@type': 'Blog',
    name: 'AI Profile Photo Maker Blog',
    description:
      'Practical guides and tips for better AI headshots, LinkedIn photos, and professional profile pictures.',
    url: canonicalUrl,
    publisher: {
      '@type': 'Organization',
      name: 'AI Profile Photo Maker',
      url: `${CANONICAL_BASE}/`,
    },
  };
}

function buildBlogPostStructuredData(post, canonicalUrl) {
  return {
    '@context': 'https://schema.org',
    '@type': 'BlogPosting',
    headline: post.title,
    description: post.description,
    datePublished: post.dateIso,
    dateModified: post.dateIso,
    mainEntityOfPage: canonicalUrl,
    author: {
      '@type': 'Organization',
      name: post.author || 'AI Profile Photo Maker',
    },
    publisher: {
      '@type': 'Organization',
      name: 'AI Profile Photo Maker',
      url: `${CANONICAL_BASE}/`,
    },
    image: OG_IMAGE,
    keywords: Array.isArray(post.tags) ? post.tags.join(', ') : undefined,
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

function normalizeAssetUrls(html) {
  return html.replace(
    /(href|src)="(?!https?:|\/|data:|mailto:|tel:|#)([^"]+)"/g,
    '$1="/$2"'
  );
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
