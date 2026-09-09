// Reproducible local browser evidence collector.
// Requires the isolated audit API/UI and /tmp/photo-workspace-audit/account.json.
const { chromium, expect } = require('../../AI.ProfilePhotoMaker.UI/node_modules/@playwright/test');
const fs = require('fs');
const assert = require('assert/strict');

const evidenceDir = 'docs/testing/evidence/photo-workspace-design-audit';
const baseUrl = 'http://localhost:4200';
const account = JSON.parse(fs.readFileSync('/tmp/photo-workspace-audit/account.json', 'utf8'));
const report = { generatedAt: new Date().toISOString(), networkPolicy: 'localhost/127.0.0.1 only', checks: [] };

function safeRoute(route) {
  const host = new URL(route.request().url()).hostname;
  return ['localhost', '127.0.0.1'].includes(host) ? route.continue() : route.abort();
}

async function rejectCookies(page) {
  const reject = page.getByRole('button', { name: 'Reject Non-Essential' });
  if (await reject.count() && await reject.isVisible()) await reject.click();
}

async function pageFacts(page, tabCount = 20) {
  const headings = await page.locator('h1,h2,h3,h4,h5,h6').evaluateAll(nodes => nodes.filter(n => n.getClientRects().length).map(n => ({ level: Number(n.tagName.slice(1)), text: (n.textContent || '').trim() })));
  const controls = await page.locator('a[href],button,input,select,textarea,[tabindex]:not([tabindex="-1"])').evaluateAll(nodes => {
    const name = el => {
      const labelledBy = (el.getAttribute('aria-labelledby') || '').split(/\s+/).filter(Boolean).map(id => document.getElementById(id)?.textContent || '').join(' ').trim();
      const labels = 'labels' in el && el.labels ? [...el.labels].map(l => l.textContent || '').join(' ').trim() : '';
      return el.getAttribute('aria-label') || labelledBy || labels || el.getAttribute('alt') || el.getAttribute('title') || (el.textContent || '').trim();
    };
    return nodes.filter(el => el.getClientRects().length && getComputedStyle(el).visibility !== 'hidden').map(el => ({ tag: el.tagName, type: el.getAttribute('type'), role: el.getAttribute('role'), name: name(el).replace(/\s+/g, ' ').slice(0, 180), disabled: !!el.disabled }));
  });
  await page.locator('body').click({ position: { x: 1, y: 1 } }).catch(() => {});
  const focusOrder = [];
  for (let i = 0; i < tabCount; i++) {
    await page.keyboard.press('Tab');
    focusOrder.push(await page.evaluate(() => {
      const el = document.activeElement;
      if (!el) return null;
      const labels = 'labels' in el && el.labels ? [...el.labels].map(l => l.textContent || '').join(' ').trim() : '';
      const style = getComputedStyle(el);
      return { tag: el.tagName, name: (el.getAttribute('aria-label') || labels || el.getAttribute('title') || el.textContent || '').trim().replace(/\s+/g, ' ').slice(0, 140), outline: `${style.outlineStyle} ${style.outlineWidth} ${style.outlineColor}` };
    }));
  }
  return {
    url: new URL(page.url()).pathname,
    title: await page.title(),
    viewport: page.viewportSize(),
    horizontalOverflow: await page.evaluate(() => document.documentElement.scrollWidth > innerWidth),
    headings,
    visibleControlCount: controls.length,
    unnamedControls: controls.filter(c => !c.name),
    controls,
    focusOrder,
    ariaSnapshot: await page.locator('body').ariaSnapshot({ timeout: 5000 })
  };
}

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: '/usr/bin/google-chrome', args: ['--no-sandbox'] });
  try {
    const anonymous = await browser.newContext({ viewport: { width: 390, height: 844 } });
    await anonymous.tracing.start({ screenshots: true, snapshots: true, sources: true });
    const page = await anonymous.newPage();
    await page.route('**/*', safeRoute);

    await page.goto(`${baseUrl}/app/enhance`);
    await page.waitForURL('**/auth/login**');
    await rejectCookies(page);
    await page.screenshot({ path: `${evidenceDir}/account-anonymous-redirect.png`, fullPage: true });
    report.checks.push({ name: 'anonymous workspace access control', requested: '/app/enhance', resultUrl: new URL(page.url()).pathname, status: 'redirected' });

    await page.goto(`${baseUrl}/auth/register`);
    await rejectCookies(page);
    for (const field of await page.locator('form input, form select').all()) {
      await field.focus();
      await field.blur();
    }
    const validationMessages = await page.locator('.invalid-feedback').allTextContents();
    assert(validationMessages.length >= 5);
    await page.screenshot({ path: `${evidenceDir}/account-registration-validation-390.png`, fullPage: true });
    report.checks.push({ name: 'registration validation at 390px', messages: validationMessages.map(x => x.trim()).filter(Boolean), facts: await pageFacts(page, 24) });
    await anonymous.tracing.stop({ path: `${evidenceDir}/account-access-trace.zip` });
    await anonymous.close();

    const auth = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    const app = await auth.newPage();
    await app.route('**/*', safeRoute);
    const loginResponses = [];
    app.on('response', response => { if (/login/i.test(response.url())) loginResponses.push({ method: response.request().method(), path: new URL(response.url()).pathname, status: response.status() }); });
    await app.goto(`${baseUrl}/auth/login`);
    await rejectCookies(app);
    await app.getByLabel('Email Address', { exact: true }).fill(account.email);
    await app.getByLabel('Password', { exact: true }).fill(account.password);
    await app.locator('input[formcontrolname="ageConfirmed"]').check();
    await app.getByRole('button', { name: 'Sign In', exact: true }).click();
    await app.waitForURL('**/app/**');
    await expect(app.getByRole('heading', { name: 'Create your professional profile photo' })).toBeVisible();
    await app.screenshot({ path: `${evidenceDir}/account-login-workspace-1440.png`, fullPage: true });
    report.checks.push({ name: 'real local password login', resultUrl: new URL(app.url()).pathname, responses: loginResponses, facts: await pageFacts(app, 26) });
    // Start authenticated tracing only after login so no credential input enters the artifact.
    await auth.tracing.start({ screenshots: true, snapshots: true, sources: true });

    await app.setViewportSize({ width: 390, height: 844 });
    const theme = app.getByRole('button', { name: /Switch to (dark|light) theme/ });
    await expect(theme).toBeVisible();
    await theme.focus();
    await app.keyboard.press('Enter');
    const mobileMenu = app.getByRole('button', { name: 'Toggle navigation' });
    await mobileMenu.focus();
    await app.keyboard.press('Enter');
    await expect(app.getByRole('navigation', { name: 'Primary navigation' })).toBeVisible();
    await app.screenshot({ path: `${evidenceDir}/account-workspace-keyboard-mobile.png`, fullPage: true });
    report.checks.push({ name: 'mobile keyboard controls', theme: await app.locator('html').getAttribute('data-theme'), menuExpanded: await mobileMenu.getAttribute('aria-expanded'), facts: await pageFacts(app, 26) });

    // Delayed pass-through: real package response, with the loading state kept visible long enough to inspect.
    await app.unroute('**/*');
    await app.route('**/*', safeRoute);
    await app.route('**/api/profilephotoworkflow/packages', async route => { await new Promise(resolve => setTimeout(resolve, 1200)); await route.continue(); });
    const pricingNavigation = app.goto(`${baseUrl}/pricing`);
    await expect(app.getByText('Loading profile photo packages...')).toBeVisible();
    await app.screenshot({ path: `${evidenceDir}/pricing-loading-390.png`, fullPage: true });
    await pricingNavigation;
    await expect(app.getByRole('heading', { name: 'Starter Package', exact: true })).toBeVisible();
    report.checks.push({ name: 'pricing delayed-real-response loading state', loadingText: 'Loading profile photo packages...', packageCards: await app.locator('.package-card').allTextContents(), facts: await pageFacts(app, 26) });
    await app.screenshot({ path: `${evidenceDir}/pricing-final-390.png`, fullPage: true });
    await app.setViewportSize({ width: 1440, height: 1000 });
    await app.screenshot({ path: `${evidenceDir}/pricing-final-1440.png`, fullPage: true });

    // Explicitly mocked empty response; Retry then reaches the real local API.
    const emptyPage = await auth.newPage();
    let returnEmpty = true;
    await emptyPage.route('**/*', safeRoute);
    await emptyPage.route('**/api/profilephotoworkflow/packages', async route => returnEmpty
      ? route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ success: true, data: [] }) })
      : route.continue());
    await emptyPage.goto(`${baseUrl}/pricing`);
    await expect(emptyPage.getByText('No profile photo packages available at this time.')).toBeVisible();
    await emptyPage.screenshot({ path: `${evidenceDir}/pricing-empty-mocked.png`, fullPage: true });
    returnEmpty = false;
    await emptyPage.getByRole('button', { name: 'Retry' }).click();
    await expect(emptyPage.getByRole('heading', { name: 'Starter Package', exact: true })).toBeVisible();
    report.checks.push({ name: 'pricing empty/retry', fixture: 'first profilephotoworkflow/packages response mocked to empty; retry passed through to real local API', recoveredCards: await emptyPage.locator('.package-card').count() });
    await emptyPage.close();

    // Explicitly injected HTTP failure; Retry then reaches the real local API.
    const errorPage = await auth.newPage();
    let returnError = true;
    await errorPage.route('**/*', safeRoute);
    await errorPage.route('**/api/profilephotoworkflow/packages', async route => returnError
      ? route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({ success: false, error: { code: 'AuditInjected', message: 'Injected audit failure' } }) })
      : route.continue());
    await errorPage.goto(`${baseUrl}/pricing`);
    await expect(errorPage.getByRole('button', { name: 'Retry' })).toBeVisible();
    const errorText = await errorPage.locator('body').innerText();
    await errorPage.screenshot({ path: `${evidenceDir}/pricing-error-injected.png`, fullPage: true });
    returnError = false;
    await errorPage.getByRole('button', { name: 'Retry' }).click();
    await expect(errorPage.getByRole('heading', { name: 'Starter Package', exact: true })).toBeVisible();
    report.checks.push({ name: 'pricing error/retry', fixture: 'first profilephotoworkflow/packages response injected HTTP 503; retry passed through to real local API', visibleErrorExcerpt: errorText.split('\n').filter(x => /unable|error|retry/i.test(x)).slice(0, 8), recoveredCards: await errorPage.locator('.package-card').count() });
    await errorPage.close();

    // Delayed pass-through: real score endpoint and inspectable loading overlay.
    await app.unroute('**/*');
    await app.route('**/*', safeRoute);
    await app.route('**/api/profilephotoworkflow/score', async route => { await new Promise(resolve => setTimeout(resolve, 1200)); await route.continue(); });
    await app.goto(`${baseUrl}/app/enhance`);
    const scoreResponse = app.waitForResponse(r => r.url().endsWith('/api/profilephotoworkflow/score'));
    await app.locator('input[type="file"]').setInputFiles('AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/set-1-before.jpg');
    await expect(app.getByText('Checking lighting, framing, and face clarity')).toBeVisible();
    await app.screenshot({ path: `${evidenceDir}/workspace-score-loading.png`, fullPage: true });
    const scored = await scoreResponse;
    await expect(app.getByText('Source photo score', { exact: true })).toBeVisible();
    report.checks.push({ name: 'workspace delayed-real-score loading state', response: { path: new URL(scored.url()).pathname, status: scored.status() }, loadingLiveText: 'Checking lighting, framing, and face clarity', facts: await pageFacts(app, 30) });

    await auth.tracing.stop({ path: `${evidenceDir}/authenticated-workspace-trace.zip` });
    await auth.close();

    fs.writeFileSync(`${evidenceDir}/browser-verification-final.json`, JSON.stringify(report, null, 2));
    const text = report.checks.map((check, index) => `CHECK ${index + 1}: ${check.name}\n${JSON.stringify(check, null, 2)}\n`).join('\n');
    fs.writeFileSync(`${evidenceDir}/browser-verification-final.txt`, text);
    console.log(`Recorded ${report.checks.length} browser checks`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error.stack || error.message); process.exitCode = 1; });
