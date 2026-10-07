// Live-stack UX review for #385 market comparison (no API mocks; real BLS May 2025 snapshot).
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-markets-review.mjs [outDir]
// Synthetic, fictional profiles. Values are checked against the raw snapshot and against the
// served payload; heatmap/table parity is asserted for every area.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/385');
const AXE = process.env.AXE_PATH;
fs.mkdirSync(OUT, { recursive: true });
const snapshot = JSON.parse(zlib.gunzipSync(fs.readFileSync('../AI.ProfilePhotoMaker.API/Data/Reference/bls-snapshot.json.gz')));
const fields = snapshot.sources.oews.fields;
const raw = (area, soc, field) => snapshot.wages[area]?.[soc]?.[fields.indexOf(field)];

const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined });
const context = await browser.newContext();
const page = await context.newPage();
const errors = [];
page.on('pageerror', e => errors.push(e.message));
const results = [];

async function axeViolations() {
  if (!AXE) return null;
  await page.addScriptTag({ path: AXE });
  return page.evaluate(async () =>
    (await window.axe.run(document.querySelector('main') ?? document, { runOnly: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'] }))
      .violations.map(v => `${v.id}: ${v.nodes.slice(0, 3).map(n => n.target.join(' ')).join(' | ')}`));
}
async function measure(label) {
  const checks = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    small: [...document.querySelectorAll('main button, main a, main input[type=checkbox], main select')]
      .filter(e => e.offsetParent)
      .map(e => [e, e.getBoundingClientRect()])
      .filter(([, r]) => r.height < 24)
      .map(([e, r]) => `${e.tagName} "${(e.textContent || '').trim().slice(0, 26)}" ${Math.round(r.height)}`),
    h1: document.querySelectorAll('h1').length,
    title: document.title,
  }));
  results.push({ label, ...checks, axe: await axeViolations() });
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
async function softwareUser(prefix) {
  await register(prefix);
  await api('PUT', '/api/career/profile', { currentTitle: 'Code Ninja', skills: ['Python'], confirmed: true, highlights: [
    'Designed and developed software applications and modified existing programs to meet user needs',
    'Analyzed user requirements and tested software systems to correct errors',
    'Wrote and maintained documentation for application code and database systems'] });
  await api('POST', '/api/career/goals', { targetRole: 'Senior developer', targetLocation: 'Denver, CO', workArrangement: 'hybrid',
    desiredPayMin: 150000, weeklyEffortHours: 6, confirmed: true });
  const run = await waitRun((await (await api('POST', '/api/career/runs', { task: 'occupation_match' }, { 'Idempotency-Key': `${prefix}-occ-${Date.now()}` })).json()).data.id);
  const match = (await (await api('GET', `/api/career/occupation-matches/${run.occupationMatchId}`)).json()).data;
  const goal = (await (await api('GET', '/api/career/goals')).json()).data;
  await api('POST', `/api/career/occupation-matches/${match.id}/confirm`, { occupationCode: '15-1252.00' }, { 'If-Match': goal.etag });
}

// --- No occupation yet.
await register('ux-mkt385-none');
await api('PUT', '/api/career/profile', { currentTitle: 'Analyst', skills: [], highlights: [], confirmed: true });
await api('POST', '/api/career/goals', { targetRole: 'x', confirmed: true });
const noneCompare = await api('GET', '/api/career/markets/compare?metric=median_wage&level=state');
results.push({ label: 'compare without occupation', status: noneCompare.status(), code: (await noneCompare.json()).error?.code });

// --- A real user.
await softwareUser('ux-mkt385-a');
await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i });
if (await reject.count()) await reject.first().click();
await page.goto(`${BASE}/app/career/markets`);
await page.getByRole('heading', { name: 'Compare U.S. markets' }).waitFor();
await page.waitForTimeout(900);
await shoot('01-states');

// Payload vs the page, and parity between the heatmap and the table.
const payload = (await (await api('GET', '/api/career/markets/compare?metric=median_wage&level=state')).json()).data;
const published = payload.occupation.publishedCode;
const mismatches = [];
let valueChecked = 0;
for (const area of payload.areas.filter(a => a.status === 'available')) {
  valueChecked++;
  const expected = raw(area.areaCode, published, 'A_MEDIAN');
  if (area.value !== expected) mismatches.push(`${area.areaCode} ${area.value} != ${expected}`);
}
const parity = await page.evaluate(() => {
  const cells = new Map();
  document.querySelectorAll('[data-area]').forEach(el => {
    if (el.dataset.value !== undefined && !cells.has(el.dataset.area)) cells.set(el.dataset.area, el.dataset.value);
  });
  const rows = new Map();
  document.querySelectorAll('tbody tr[data-area]').forEach(el => {
    if (el.dataset.value !== undefined) rows.set(el.dataset.area, el.dataset.value);
  });
  const diffs = [];
  for (const [area, value] of rows) if (cells.has(area) && cells.get(area) !== value) diffs.push(`${area} cell ${cells.get(area)} != row ${value}`);
  return { cells: cells.size, rows: rows.size, diffs };
});
const colorado = payload.areas.find(a => a.areaCode === '08');
const suppressed = payload.areas.filter(a => a.status !== 'available');
results.push({ label: 'states payload', areas: payload.areas.length, national: payload.national,
  colorado: colorado && { value: colorado.value, rank: colorado.rank, share: colorado.shareOfNationalEmployment },
  valueChecked, mismatches, parity, nonAvailable: suppressed.length,
  reference: payload.reference.release + ' ' + payload.reference.publishedOn, truncated: payload.truncated });

// Metrics: unsupported ones are disabled with a reason.
const metrics = (await (await api('GET', '/api/career/markets/metrics')).json()).data.metrics;
const metricControls = await page.evaluate(() => ({
  radios: [...document.querySelectorAll('[data-metrics] input[type=radio]')].map(i => ({ value: i.value, disabled: i.disabled, describedby: i.getAttribute('aria-describedby') })),
  reasons: [...document.querySelectorAll('[data-metric-reason]')].map(el => el.textContent.trim()),
}));
results.push({ label: 'metrics', keys: metrics.map(m => `${m.key}${m.supported ? '' : ':unsupported'}`),
  unsupportedReason: metrics.find(m => !m.supported)?.reason,
  disabled: metricControls.radios.filter(r => r.disabled).map(r => r.value),
  disabledReason: metricControls.reasons[0]?.slice(0, 90),
  describedbyWired: metricControls.radios.filter(r => r.disabled).every(r => !!r.describedby) });

// Suppressed / top-coded rendering differs from low values.
const statuses = await page.evaluate(() => ({
  patterns: document.querySelectorAll('[data-status="not_available"], [data-status="top_coded"], [data-status="not_published"]').length,
  legendText: (document.querySelector('[data-legend]')?.textContent ?? '').slice(0, 200),
}));
results.push({ label: 'missing cells', ...statuses });

// Metro level, including Alaska and Hawaii.
const metros = (await (await api('GET', '/api/career/markets/compare?metric=median_wage&level=metro')).json()).data;
const ak = metros.areas.filter(a => /, AK$/.test(a.areaTitle));
const hi = metros.areas.filter(a => /, HI$/.test(a.areaTitle));
results.push({ label: 'metro coverage', areas: metros.areas.length, alaska: ak.length, hawaii: hi.length,
  hawaiiTitles: hi.map(a => a.areaTitle).slice(0, 2),
  nonMetroIncluded: metros.areas.filter(a => a.type !== 'metro').length });

// Filters persist across a reload; selection is keyboard operable.
await page.goto(`${BASE}/app/career/markets?metric=median_wage&level=state&q=colo`);
await page.waitForTimeout(1200);
const filtered = await page.locator('tbody tr[data-area]').count();
await page.reload();
await page.waitForTimeout(1200);
const afterReload = await page.locator('tbody tr[data-area]').count();
const searchValue = await page.locator('input[type="search"], input[data-search]').first().inputValue().catch(() => '');
const firstCell = page.locator('[data-area]').filter({ has: page.locator('') }).first();
await page.locator('main button[data-area]').first().focus();
await page.keyboard.press('Enter');
await page.waitForTimeout(500);
const selectedAfterKeyboard = await page.locator('tbody input[type=checkbox]:checked').count();
results.push({ label: 'filters and keyboard', filtered, afterReload, searchValue, selectedAfterKeyboard,
  urlHasFilters: page.url().includes('metric=median_wage') && page.url().includes('q=colo') });
await shoot('02-filtered');

// Save the location preference through the confirmation step.
await page.goto(`${BASE}/app/career/markets?metric=median_wage&level=state&q=colo`);
await page.waitForTimeout(1200);
await page.locator('tbody input[type=checkbox]').first().check();
const goalBefore = (await (await api('GET', '/api/career/goals')).json()).data;
await page.getByRole('button', { name: /save this location/i }).click();
await page.waitForTimeout(400);
const confirmVisible = await page.getByRole('dialog').isVisible().catch(() => false);
await page.getByRole('button', { name: /^(yes|confirm|save)/i }).last().click();
await page.waitForTimeout(1500);
const goalAfter = (await (await api('GET', '/api/career/goals')).json()).data;
results.push({ label: 'save preference', confirmVisible, versionBefore: goalBefore.version, versionAfter: goalAfter.version,
  targetLocation: goalAfter.targetLocation, preferredArea: goalAfter.preferredArea,
  savedText: (await page.locator('main').innerText()).includes('Saved to your career goal.') });
await shoot('03-saved');

// Mobile: searchable list, no overflow.
await page.setViewportSize({ width: 390, height: 844 });
await page.goto(`${BASE}/app/career/markets?metric=median_wage&level=state`);
await page.waitForTimeout(1500);
const mobile = await page.evaluate(() => ({
  tiles: document.querySelectorAll('button[data-area]').length,
  listItems: document.querySelectorAll('[data-list] li[data-area]').length,
  listValues: [...document.querySelectorAll('[data-list] li[data-area]')].slice(0, 2).map(li => `${li.dataset.area}=${li.dataset.value}`),
  tableBehindDetails: !!document.querySelector('[data-table-details]'),
  searchVisible: !!document.querySelector('input[type="search"], input[data-search]'),
  overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
}));
results.push({ label: 'mobile', ...mobile });
await measure('mobile-list');
await page.setViewportSize({ width: 320, height: 720 });
await page.waitForTimeout(500);
const mobile320 = await page.evaluate(() => ({
  listItems: document.querySelectorAll('[data-list] li[data-area]').length,
  tiles: document.querySelectorAll('button[data-area]').length,
  overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
}));
results.push({ label: 'mobile 320', ...mobile320 });

// Formatted dates and no bare area codes anywhere on the page.
const textChecks = await page.evaluate(() => {
  const text = document.querySelector('main')?.innerText ?? '';
  return {
    isoDates: (text.match(/\d{4}-\d{2}-\d{2}/g) ?? []).slice(0, 3),
    hasReadableDate: /15 May 2026/.test(text),
    hasReadablePeriod: /May 2025/.test(text),
  };
});
await page.goto(`${BASE}/app/career/markets?metric=median_wage&level=state&q=colo&areas=06`);
await page.waitForTimeout(1500);
const hiddenSelection = await page.evaluate(() => {
  const text = document.querySelector('main')?.innerText ?? '';
  return { mentionsNeutralPhrase: /not shown by the current filter/.test(text), showsBareCode: /\b06\b/.test(text) };
});
results.push({ label: 'dates and codes', ...textChecks, ...hiddenSelection });

// Expired session.
await context.clearCookies();
await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(`${BASE}/app/career/markets`);
await page.waitForTimeout(2500);
results.push({ label: 'expired session', url: decodeURIComponent(page.url().replace(BASE, '')) });

fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify({ results, errors }, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results to ${OUT}; page errors: ${errors.length}`);
