// Live-stack review for #386 job observations (no API mocks; no USAJOBS key configured, which is
// the objective's explicit "no key shows the coverage-unavailable state" path).
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-jobs-review.mjs [outDir]
// Synthetic, fictional profiles. Simulated review, not user evidence.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/386');
const AXE = process.env.AXE_PATH;
fs.mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined });
const context = await browser.newContext();
const page = await context.newPage();
const errors = [];
page.on('pageerror', e => errors.push(e.message));
const results = [];

async function measure(label) {
  const checks = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    // Measure a checkbox's label (the real pointer target), not the bare input box.
    small: [...document.querySelectorAll('main button, main a, main select, main input[type=checkbox], main input[type=text], main input[type=search]')]
      .filter(e => e.offsetParent)
      .map(e => [e, (e.type === 'checkbox' ? e.closest('label') ?? e : e).getBoundingClientRect()])
      .filter(([, r]) => r.height < 24)
      .map(([e, r]) => `${e.tagName} "${(e.textContent || '').trim().slice(0, 26)}" ${Math.round(r.height)}`),
    h1: document.querySelectorAll('h1').length,
    title: document.title,
    text: (document.querySelector('main')?.innerText ?? '').slice(0, 400),
  }));
  let axe = [];
  if (AXE) {
    await page.addScriptTag({ path: AXE });
    axe = await page.evaluate(async () =>
      (await window.axe.run(document.querySelector('main') ?? document, { runOnly: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'] }))
        .violations.map(v => `${v.id}: ${v.nodes.slice(0, 3).map(n => n.target.join(' ')).join(' | ')}`));
  }
  results.push({ label, ...checks, axe });
}
async function shoot(name) {
  for (const [vp, w, h] of [['desktop', 1280, 900], ['mobile-390', 390, 844], ['mobile-320', 320, 720]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.waitForTimeout(300);
    await page.screenshot({ path: path.join(OUT, `${name}-${vp}.png`), fullPage: true });
    await measure(`${name} @${vp}`);
  }
  await page.setViewportSize({ width: 1280, height: 900 });
}
const api = (method, url, data, headers) => page.request.fetch(`${BASE}${url}`, { method, data, headers });
async function register(prefix) {
  await context.clearCookies();
  const email = `${prefix}-${Date.now()}@example.com`, pw = 'Passw0rd!Career';
  const reg = await page.request.post(`${BASE}/api/auth/register`, {
    data: { email, password: pw, confirmPassword: pw, firstName: 'Riley', lastName: 'Synthetic', gender: 'Prefer not to say',
      ethnicity: 'Prefer not to say', ageConfirmed: true, acceptTerms: true, turnstileToken: 'local' },
  });
  if (!reg.ok()) throw new Error('register ' + reg.status());
  await page.request.post(`${BASE}/api/auth/dev/confirm-email`);
}
async function waitRun(id) {
  for (let i = 0; i < 40; i++) {
    const run = (await (await api('GET', `/api/career/runs/${id}`)).json()).data;
    if (!['queued', 'working'].includes(run.status)) return run;
    await page.waitForTimeout(700);
  }
  throw new Error('run did not finish');
}

// --- API surface with no key configured.
await register('ux-jobs-a');
const source = await api('GET', '/api/career/jobs/source');
const sourceBody = await source.json();
const noGoal = await api('GET', '/api/career/jobs/observations');
const noGoalBody = await noGoal.json();
results.push({ label: 'source endpoint (no key)', status: source.status(), configured: sourceBody.data?.configured,
  name: sourceBody.data?.name, coverage: sourceBody.data?.coverage, attributionPresent: !!sourceBody.data?.attribution,
  observationsStatus: noGoal.status(), available: noGoalBody.data?.coverage?.available,
  reason: noGoalBody.data?.coverage?.reason, listEmpty: (noGoalBody.data?.observations ?? []).length === 0,
  sourceUrl: noGoalBody.data?.coverage?.sourceUrl });

// A confirmed occupation + goal, then the page.
await api('PUT', '/api/career/profile', { currentTitle: 'Code Ninja', skills: ['Python'], confirmed: true, highlights: [
  'Designed and developed software applications and modified existing programs to meet user needs',
  'Analyzed user requirements and tested software systems to correct errors',
  'Wrote and maintained documentation for application code and database systems'] });
await api('POST', '/api/career/goals', { targetRole: 'Senior developer', targetLocation: 'Denver, CO', workArrangement: 'hybrid',
  desiredPayMin: 150000, weeklyEffortHours: 6, confirmed: true });
const run = await waitRun((await (await api('POST', '/api/career/runs', { task: 'occupation_match' }, { 'Idempotency-Key': `jobs-occ-${Date.now()}` })).json()).data.id);
const match = (await (await api('GET', `/api/career/occupation-matches/${run.occupationMatchId}`)).json()).data;
const goal = (await (await api('GET', '/api/career/goals')).json()).data;
await api('POST', `/api/career/occupation-matches/${match.id}/confirm`, { occupationCode: '15-1252.00' }, { 'If-Match': goal.etag });

const withGoal = (await (await api('GET', '/api/career/jobs/observations')).json()).data;
const badArea = await api('GET', '/api/career/jobs/observations?area=Atlantis%2C%20ZZ');
results.push({ label: 'observations with goal', available: withGoal.coverage.available, reason: withGoal.coverage.reason,
  area: withGoal.area, counts: withGoal.coverage.counts, stale: withGoal.preferences.stalePreference,
  note: withGoal.note?.slice(0, 90), unknownAreaStatus: badArea.status() });

await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i });
if (await reject.count()) await reject.first().click();
await page.goto(`${BASE}/app/career/jobs`);
await page.getByRole('heading', { name: 'Open postings' }).waitFor();
await page.waitForTimeout(900);
await shoot('01-unavailable');
const pageText = await page.locator('main').innerText();
results.push({ label: 'unavailable state', explainsConfigured: /not configured/i.test(pageText),
  namesSource: /USAJOBS/i.test(pageText),
  attribution: /USAJOBS/i.test(pageText) && /unchanged/i.test(pageText),
  benchmarkLinks: await page.getByRole('link', { name: /benchmark|market brief|pay analysis|compare/i }).count(),
  noBareEmptyList: await page.locator('main ul li').count(),
  statesNotTotals: /not employment totals|not an outlook|not a count of all vacancies/i.test(pageText) });

// Filters persist in the URL.
await page.goto(`${BASE}/app/career/jobs?area=Denver%2C%20CO&eligibleOnly=true&remote=unknown&q=software`);
await page.waitForTimeout(1200);
const filterState = await page.evaluate(() => ({
  area: document.querySelector('#job-area')?.value ?? null,
  eligible: !!document.querySelector('input[type=checkbox]:checked'),
  remote: document.querySelector('select')?.value ?? null,
  q: document.querySelector('input[type="search"], input[data-search]')?.value ?? null,
}));
await page.reload();
await page.waitForTimeout(1200);
const afterReload = await page.evaluate(() => ({
  area: document.querySelector('#job-area')?.value ?? null,
  q: document.querySelector('input[type="search"], input[data-search]')?.value ?? null,
}));
results.push({ label: 'filters persist', filterState, afterReload, url: page.url().replace(BASE, '') });
await shoot('02-filters');

// The benchmark pages must keep working while the feed is unavailable.
const bench = {};
for (const [key, url, expect] of [['brief', '/app/career/market', /Career market brief|Comparable pay/i],
  ['pay', '/app/career/pay', /Comparable pay analysis/i], ['compare', '/app/career/markets', /Compare U.S. markets/i]]) {
  await page.goto(`${BASE}${url}`);
  await page.waitForTimeout(1500);
  const text = await page.locator('main').innerText();
  bench[key] = { ok: expect.test(text), hasError: /something went wrong|error/i.test(text.slice(0, 200)) };
}
results.push({ label: 'benchmark pages unaffected', ...bench });

// Expired session.
await context.clearCookies();
await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(`${BASE}/app/career/jobs`);
await page.waitForTimeout(2500);
results.push({ label: 'expired session', url: decodeURIComponent(page.url().replace(BASE, '')) });

fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify({ results, errors }, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results to ${OUT}; page errors: ${errors.length}`);
