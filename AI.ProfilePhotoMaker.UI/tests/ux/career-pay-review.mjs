// Live-stack UX review for #384 comparable-pay analysis (no API mocks; real BLS May 2025 snapshot).
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-pay-review.mjs [outDir]
// Synthetic, fictional profiles. Benchmark figures are checked against the raw snapshot.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/384');
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
// A software user with a confirmed occupation, a location and a requested salary.
async function softwareUser(prefix, { location = 'Denver, CO', pay = 150000 } = {}) {
  await register(prefix);
  await api('PUT', '/api/career/profile', { currentTitle: 'Code Ninja', skills: ['Python', 'SQL'], confirmed: true, highlights: [
    'Designed and developed software applications and modified existing programs to meet user needs',
    'Analyzed user requirements and tested software systems to correct errors',
    'Wrote and maintained documentation for application code and database systems'] });
  await api('POST', '/api/career/goals', { targetRole: 'Senior developer', targetLocation: location, workArrangement: 'hybrid',
    desiredPayMin: pay, weeklyEffortHours: 6, confirmed: true });
  const run = await waitRun((await (await api('POST', '/api/career/runs', { task: 'occupation_match' }, { 'Idempotency-Key': `${prefix}-occ-${Date.now()}` })).json()).data.id);
  const match = (await (await api('GET', `/api/career/occupation-matches/${run.occupationMatchId}`)).json()).data;
  const goal = (await (await api('GET', '/api/career/goals')).json()).data;
  await api('POST', `/api/career/occupation-matches/${match.id}/confirm`, { occupationCode: '15-1252.00' }, { 'If-Match': goal.etag });
}

// --- User B first, for cross-user checks.
await softwareUser('ux-pay-b');
const bRun = await waitRun((await (await api('POST', '/api/career/runs', { task: 'pay_analysis' }, { 'Idempotency-Key': `b-pay-${Date.now()}` })).json()).data.id);
const bAnalysisId = bRun.payAnalysisId;

// --- User A from the page.
await softwareUser('ux-pay-a');
await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i });
if (await reject.count()) await reject.first().click();
await page.goto(`${BASE}/app/career/pay`);
await page.getByRole('heading', { name: 'Comparable pay analysis' }).waitFor();
await page.waitForTimeout(600);
await shoot('01-start');
await page.getByRole('button', { name: 'Build my pay analysis' }).click();
await page.waitForURL(/analysis=/, { timeout: 30000 });
await page.locator('[data-figure]').first().waitFor({ timeout: 15000 });
await page.waitForTimeout(700);
const analysisId = new URL(page.url()).searchParams.get('analysis');
const analysis = (await (await api('GET', `/api/career/pay-analyses/${analysisId}`)).json()).data;
const runs = (await (await api('GET', '/api/career/runs')).json()).data;

// Benchmark figures against the raw snapshot.
const mismatches = [];
let checked = 0;
const benchmark = analysis.sections.find(s => s.key === 'benchmark');
const published = snapshot.crosswalk.oews['15-1252.00'].code;
for (const f of benchmark.figures) {
  const field = { medianAnnual: 'A_MEDIAN', pct25Annual: 'A_PCT25', pct75Annual: 'A_PCT75' }[f.key];
  const expected = raw(f.areaCode, published, field);
  checked++;
  if (f.value !== expected) mismatches.push(`${f.key}@${f.areaCode} ${f.value} != ${expected}`);
}
const personalized = analysis.sections.find(s => s.key === 'personalized');
const scenario = analysis.sections.find(s => s.key === 'scenario');
const median = scenario.benchmarkMedianAnnual; // the area the scenario names, local when resolved
const personalizedCard = await page.locator('[data-section="personalized"]').innerText();
results.push({
  label: 'analysis vs snapshot', status: analysis.status, checked, mismatches,
  benchmarkLabel: benchmark.label, benchmarkStatus: benchmark.status,
  personalized: { status: personalized.status, reason: personalized.reason, interval: personalized.interval, cohort: personalized.cohort },
  blockedReasons: analysis.blockedReasons, personalizedAllowed: analysis.qualification.personalizedAllowed,
  blockedBannerShowsRawCode: (await page.locator('[data-blocked]').innerText()).includes('provider_rights_unverified'),
  gateCount: analysis.qualification.gates.length,
  scenario: { requested: scenario.requestedAnnual, median: scenario.benchmarkMedianAnnual, gap: scenario.gapAnnual, percent: scenario.gapPercent },
  scenarioCheck: scenario.gapAnnual === scenario.requestedAnnual - median && scenario.gapPercent === Math.round((scenario.gapAnnual / median) * 1000) / 10,
  dollarSignsInPersonalizedCard: (personalizedCard.match(/\$/g) ?? []).length,
  allowance: runs.allowance, inputHash: analysis.inputHash?.slice(0, 12),
  stale: analysis.stale, sources: analysis.sources.map(s => `${s.id} ${s.referencePeriod}`),
});
await shoot('02-analysis');

// Reproducibility: recompute must reproduce exactly.
const recompute = await api('POST', `/api/career/pay-analyses/${analysisId}/recompute`);
const recomputeBody = await recompute.json();
await page.getByRole('button', { name: 'Recompute' }).click();
await page.waitForTimeout(1500);
results.push({ label: 'recompute', httpStatus: recompute.status(), matches: recomputeBody.data?.matches,
  hashEqual: recomputeBody.data?.inputHash === analysis.inputHash, differences: recomputeBody.data?.differences,
  pageText: (await page.locator('[data-recompute]').innerText().catch(() => '')).slice(0, 120) });
await shoot('03-recompute');

// Cross-user.
const cross = await api('GET', `/api/career/pay-analyses/${bAnalysisId}`);
const crossRecompute = await api('POST', `/api/career/pay-analyses/${bAnalysisId}/recompute`);
results.push({ label: 'cross-user', get: cross.status(), recompute: crossRecompute.status() });

// Stale after a profile change.
const pEtag = (await (await api('GET', '/api/career/profile')).json()).data?.etag;
await api('PUT', '/api/career/profile', { currentTitle: 'Senior Code Ninja', skills: ['Python'], confirmed: true, highlights: [
  'Designed and developed software applications and modified existing programs to meet user needs'] }, { 'If-Match': pEtag });
await page.reload();
await page.waitForTimeout(2000);
const after = (await (await api('GET', `/api/career/pay-analyses/${analysisId}`)).json()).data;
results.push({ label: 'stale after profile change', stale: after.stale, reasons: after.staleReasons,
  sectionsUnchanged: JSON.stringify(after.sections) === JSON.stringify(analysis.sections),
  banner: await page.getByText('Your profile or goal changed after this analysis').count() });
await shoot('04-stale');

// A goal with no requested pay -> scenario unavailable.
await softwareUser('ux-pay-c', { pay: null });
await page.goto(`${BASE}/app/career/pay`);
await page.getByRole('button', { name: 'Build my pay analysis' }).click();
await page.waitForURL(/analysis=/, { timeout: 30000 });
await page.waitForTimeout(1500);
const cId = new URL(page.url()).searchParams.get('analysis');
const cAnalysis = (await (await api('GET', `/api/career/pay-analyses/${cId}`)).json()).data;
results.push({ label: 'no requested pay', scenarioStatus: cAnalysis.sections.find(s => s.key === 'scenario').status,
  personalized: cAnalysis.sections.find(s => s.key === 'personalized').status });

// Qualification endpoint.
const qual = await api('GET', '/api/career/pay/qualification');
const qualBody = await qual.json();
results.push({ label: 'qualification', status: qual.status(), allowed: qualBody.data?.personalizedAllowed, blocked: qualBody.data?.blockedReasons, gates: qualBody.data?.gates?.length });

// Expired session.
await context.clearCookies();
await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(`${BASE}/app/career/pay`);
await page.waitForTimeout(2500);
results.push({ label: 'expired session', url: decodeURIComponent(page.url().replace(BASE, '')) });

fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify({ results, errors }, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results to ${OUT}; page errors: ${errors.length}`);
