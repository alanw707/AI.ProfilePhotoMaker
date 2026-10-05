// Live-stack UX review for #382 market brief (no API mocks; real BLS May 2025 + 2025-35 snapshot).
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-market-review.mjs [outDir]
// Synthetic, fictional profiles. Every displayed national/local wage and employment figure is
// checked against the raw snapshot, read here independently of the API.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/382');
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

async function measure(label) {
  const checks = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    small: [...document.querySelectorAll('main a, main button')]
      .filter(e => e.offsetParent)
      .map(e => [e, e.getBoundingClientRect()])
      .filter(([, r]) => r.height < 24)
      .map(([e, r]) => `${e.tagName} "${(e.textContent || '').trim().slice(0, 30)}" ${Math.round(r.height)}`),
    h1: document.querySelectorAll('h1').length,
    title: document.title,
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
    await page.waitForTimeout(250);
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
// A software profile with a confirmed occupation, at the given goal location.
async function softwareUser(prefix, location) {
  await register(prefix);
  await api('PUT', '/api/career/profile', { currentTitle: 'Code Ninja', skills: ['Python', 'SQL'], confirmed: true, highlights: [
    'Designed and developed software applications and modified existing programs to meet user needs',
    'Analyzed user requirements and tested software systems to correct errors',
    'Wrote and maintained documentation for application code and database systems'] });
  await api('POST', '/api/career/goals', { targetRole: 'Senior developer', targetLocation: location, workArrangement: 'hybrid', weeklyEffortHours: 6, confirmed: true });
  const run = await waitRun((await (await api('POST', '/api/career/runs', { task: 'occupation_match' }, { 'Idempotency-Key': `${prefix}-occ-${Date.now()}` })).json()).data.id);
  const match = (await (await api('GET', `/api/career/occupation-matches/${run.occupationMatchId}`)).json()).data;
  const goal = (await (await api('GET', '/api/career/goals')).json()).data;
  const confirm = await api('POST', `/api/career/occupation-matches/${match.id}/confirm`, { occupationCode: '15-1252.00' }, { 'If-Match': goal.etag });
  return { candidates: match.candidates.map(c => c.code), confirm: confirm.status() };
}

// --- No occupation yet.
await register('ux-mkt-none');
await api('PUT', '/api/career/profile', { currentTitle: 'Analyst', skills: [], highlights: [], confirmed: true });
await api('POST', '/api/career/goals', { targetRole: 'x', confirmed: true });
const noOcc = await api('POST', '/api/career/runs', { task: 'market_brief' }, { 'Idempotency-Key': `none-${Date.now()}` });
results.push({ label: 'start without occupation', status: noOcc.status(), code: (await noOcc.json()).error?.code });

// --- Denver user B (for cross-user).
const b = await softwareUser('ux-mkt-b', 'Denver, CO');
const bRun = await waitRun((await (await api('POST', '/api/career/runs', { task: 'market_brief' }, { 'Idempotency-Key': `b-mkt-${Date.now()}` })).json()).data.id);
const bBriefId = bRun.marketBriefId;

// --- Denver user A, from the page.
const a = await softwareUser('ux-mkt-a', 'Denver, CO');
results.push({ label: 'occupation confirmed', ...a });
await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i });
if (await reject.count()) await reject.first().click();
await page.goto(`${BASE}/app/career/market`);
await page.getByRole('heading', { name: 'Career market brief' }).waitFor();
await page.waitForTimeout(600);
await shoot('01-start');
await page.getByRole('button', { name: 'Build my market brief' }).click();
await page.waitForURL(/brief=/, { timeout: 30000 });
await page.locator('[data-figure]').first().waitFor({ timeout: 15000 });
await page.waitForTimeout(600);
const briefId = new URL(page.url()).searchParams.get('brief');
const brief = (await (await api('GET', `/api/career/market-briefs/${briefId}`)).json()).data;
const runs = (await (await api('GET', '/api/career/runs')).json()).data;

// Every wage/employment figure against the raw snapshot.
const fieldOf = { medianAnnual: 'A_MEDIAN', pct10Annual: 'A_PCT10', pct25Annual: 'A_PCT25', pct75Annual: 'A_PCT75', pct90Annual: 'A_PCT90',
  meanAnnual: 'A_MEAN', medianHourly: 'H_MEDIAN', meanPrse: 'MEAN_PRSE', employment: 'TOT_EMP', employmentPrse: 'EMP_PRSE',
  jobsPer1000: 'JOBS_1000', locationQuotient: 'LOC_QUOTIENT' };
const mismatches = [];
let checked = 0;
for (const section of brief.sections.filter(s => s.key === 'wages' || s.key === 'employment')) {
  for (const f of section.figures) {
    if (f.key === 'medianDifferenceAnnual') {
      const expected = raw(f.areaCode, '15-1252', 'A_MEDIAN') - raw('99', '15-1252', 'A_MEDIAN');
      checked++; if (f.value !== expected) mismatches.push(`${f.key}@${f.areaCode} ${f.value} != ${expected}`);
      continue;
    }
    const expected = raw(f.areaCode, '15-1252', fieldOf[f.key]);
    checked++;
    if (typeof expected === 'number' ? f.value !== expected : f.status === 'available') mismatches.push(`${f.key}@${f.areaCode} ${f.value} != ${expected}`);
  }
}
const proj = snapshot.projections['15-1252'];
const outlook = Object.fromEntries(brief.sections.find(s => s.key === 'outlook').figures.map(f => [f.key, f.value]));
for (const [k, e] of [['employment2025', proj.employment2025Thousands], ['employment2035', proj.employment2035Thousands],
  ['changePercent', proj.changePercent], ['annualOpenings', proj.annualOpeningsThousands], ['typicalEducation', proj.education]]) {
  checked++; if (outlook[k] !== e) mismatches.push(`outlook ${k} ${outlook[k]} != ${e}`);
}
const domFigures = await page.locator('[data-figure]').count();
results.push({ label: 'brief vs snapshot', status: brief.status, location: brief.location, checked, mismatches, domFigures,
  sections: brief.sections.map(s => `${s.key}:${s.status}`), alternatives: brief.sections.find(s => s.key === 'alternatives').items?.map(i => i.code),
  sources: brief.sources.map(s => `${s.id} ${s.referencePeriod} ${s.publishedOn}`), nextAction: brief.nextAction,
  allowance: runs.allowance,
  spot: { nationalMedian: await page.locator('[data-figure="wages:medianAnnual:99"]').innerText(),
    denverMedian: await page.locator('[data-figure="wages:medianAnnual:19740"]').innerText(),
    difference: await page.locator('[data-figure="wages:medianDifferenceAnnual:19740"]').innerText().catch(() => null) } });
await shoot('02-brief');

// Source drawer.
await page.getByRole('button', { name: 'Source', exact: true }).first().click();
await page.getByRole('heading', { name: 'Source', exact: true }).waitFor();
const drawer = page.locator('dialog[open]');
results.push({ label: 'source drawer', sources: await drawer.locator('[data-source]').count(),
  citation: (await drawer.innerText()).includes('Occupational Employment and Wage Statistics, May 2025'),
  box: await drawer.boundingBox() });
await shoot('03-source-drawer');
await page.keyboard.press('Escape');
await page.waitForTimeout(300);
results.push({ label: 'drawer closed', open: await page.locator('dialog[open]').count() });

// Cross-user.
const cross = await api('GET', `/api/career/market-briefs/${bBriefId}`);
results.push({ label: 'cross-user', status: cross.status(), bConfirm: b.confirm });

// Reload keeps the brief, then a profile change marks it stale but unchanged.
await page.reload();
await page.locator('[data-figure]').first().waitFor({ timeout: 15000 });
const pEtag = (await (await api('GET', '/api/career/profile')).json()).data?.etag;
await api('PUT', '/api/career/profile', { currentTitle: 'Senior Code Ninja', skills: ['Python'], confirmed: true, highlights: [
  'Designed and developed software applications and modified existing programs to meet user needs'] }, { 'If-Match': pEtag });
await page.reload();
await page.waitForTimeout(2000);
const after = (await (await api('GET', `/api/career/market-briefs/${briefId}`)).json()).data;
results.push({ label: 'stale after profile change', stale: after.stale, reasons: after.staleReasons,
  sectionsUnchanged: JSON.stringify(after.sections) === JSON.stringify(brief.sections),
  banner: await page.getByText('Your goal or profile changed after this brief').count() });
await shoot('04-stale');

// Unresolved location.
await softwareUser('ux-mkt-c', 'Atlantis, ZZ');
await page.goto(`${BASE}/app/career/market`);
await page.getByRole('button', { name: 'Build my market brief' }).click();
await page.waitForURL(/brief=/, { timeout: 30000 });
await page.waitForTimeout(1500);
const cBrief = (await (await api('GET', `/api/career/market-briefs/${new URL(page.url()).searchParams.get('brief')}`)).json()).data;
results.push({ label: 'unresolved location', resolution: cBrief.location.resolution, sections: cBrief.sections.map(s => `${s.key}:${s.status}:${s.reason}`),
  nextAction: cBrief.nextAction, couldNotPlace: await page.getByText('We could not place').count() });
await shoot('05-unresolved');

// Expired session.
await context.clearCookies();
await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(`${BASE}/app/career/market`);
await page.waitForTimeout(2500);
results.push({ label: 'expired session', url: decodeURIComponent(page.url().replace(BASE, '')) });

fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify({ results, errors }, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results to ${OUT}; page errors: ${errors.length}`);
