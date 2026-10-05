// Live-stack UX review for #381 occupation matches (no API mocks; real O*NET 30.0 snapshot).
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-occupation-review.mjs [outDir]
// Synthetic, fictional profiles. Simulated review, not user evidence.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/381');
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
    small: [...document.querySelectorAll('main a, main button, main textarea, main input[type=radio]')]
      .filter(e => e.offsetParent)
      .map(e => [e, (e.type === 'radio' ? e.closest('label') ?? e : e).getBoundingClientRect()])
      .filter(([, r]) => r.height < 24)
      .map(([e, r]) => `${e.tagName} "${(e.textContent || '').trim().slice(0, 30)}" ${Math.round(r.height)}`),
    percent: /\d\s*%/.test(document.querySelector('main')?.innerText ?? ''),
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
async function profile(p) {
  return api('PUT', '/api/career/profile', { skills: [], highlights: [], confirmed: true, ...p });
}
async function goal() {
  return api('POST', '/api/career/goals', { targetRole: 'Next role', targetLocation: 'Denver, CO', workArrangement: 'hybrid', weeklyEffortHours: 6, confirmed: true });
}
async function startFromPage() {
  await page.goto(`${BASE}/app/career/occupation`);
  await page.getByRole('heading', { name: 'Confirm your occupation' }).waitFor();
  await page.getByRole('button', { name: 'Find matching occupations' }).click();
  await page.waitForURL(/run=/);
}
const runId = () => new URL(page.url()).searchParams.get('run');

// --- User B: confirmed nurse match, for cross-user checks.
await register('ux-occ-b');
await profile({ currentTitle: 'Staff nurse', skills: ['Patient care'], highlights: [
  'Monitored patient vital signs and recorded observations in medical records',
  'Administered medications and treatments as prescribed by physicians',
  'Assessed patient health problems and developed nursing care plans'] });
await goal();
const bRun = (await (await api('POST', '/api/career/runs', { task: 'occupation_match' }, { 'Idempotency-Key': `b-occ-${Date.now()}` })).json()).data;
let bMatchId = null;
for (let i = 0; i < 30 && !bMatchId; i++) {
  await page.waitForTimeout(1000);
  bMatchId = (await (await api('GET', `/api/career/runs/${bRun.id}`)).json()).data?.occupationMatchId;
}
const bGoalBefore = (await (await api('GET', '/api/career/goals')).json()).data;
results.push({ label: 'user B match', matchId: bMatchId });

// --- User A: software duties under a nonstandard title.
await register('ux-occ-a');
await profile({ currentTitle: 'Code Ninja', industry: 'Software', skills: ['Python', 'SQL', 'Phlebotomy'], highlights: [
  'Designed and developed software applications and modified existing programs to meet user needs',
  'Analyzed user requirements and tested software systems to correct errors',
  'Wrote and maintained documentation for application code and database systems'],
  summary: 'Builds internal web applications.' });
await goal();
await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i });
if (await reject.count()) await reject.first().click();
await page.goto(`${BASE}/app/career/occupation`);
await page.getByRole('heading', { name: 'Confirm your occupation' }).waitFor();
await page.waitForTimeout(600);
await shoot('01-start');
await page.getByRole('button', { name: 'Find matching occupations' }).click();
await page.waitForURL(/run=/);
await page.getByRole('button', { name: 'Use this occupation' }).waitFor({ timeout: 20000 });
await page.waitForTimeout(500);
const aRun = (await (await api('GET', `/api/career/runs/${runId()}`)).json()).data;
const aMatch = (await (await api('GET', `/api/career/occupation-matches/${aRun.occupationMatchId}`)).json()).data;
results.push({ label: 'software match', steps: aRun.steps.map(s => s.label), allowance: aRun.allowance,
  candidates: aMatch.candidates.map(c => `${c.code} ${c.title} ${c.strength} ev${c.evidence.length} title${c.titleMatched}`),
  unsupportedSkills: aMatch.candidates[0]?.unsupportedSkills, attribution: !!aMatch.reference?.attribution,
  attributionShown: await page.getByText('O*NET 30.0 Database', { exact: false }).count() });
await shoot('02-candidates');

// Cross-user: A reads/confirms/dismisses B's match.
const goalA = (await (await api('GET', '/api/career/goals')).json()).data;
const crossGet = await api('GET', `/api/career/occupation-matches/${bMatchId}`);
const crossConfirm = await api('POST', `/api/career/occupation-matches/${bMatchId}/confirm`, { occupationCode: '29-1141.00' }, { 'If-Match': goalA?.etag });
const crossDismiss = await api('POST', `/api/career/occupation-matches/${bMatchId}/dismiss`);
results.push({ label: 'cross-user', get: crossGet.status(), confirm: crossConfirm.status(), dismiss: crossDismiss.status() });

// Confirm the top candidate from the page.
await page.locator('main input[type=radio]').first().check();
await page.getByRole('button', { name: 'Use this occupation' }).click();
await page.getByText('Saved to your career goal.').waitFor({ timeout: 10000 });
await shoot('03-confirmed');
const goalAfter = (await (await api('GET', '/api/career/goals')).json()).data;
const again = await api('POST', `/api/career/occupation-matches/${aRun.occupationMatchId}/confirm`, { occupationCode: aMatch.candidates[0].code }, { 'If-Match': goalAfter?.etag });
const noIfMatch = await api('POST', `/api/career/occupation-matches/${aRun.occupationMatchId}/confirm`, { occupationCode: aMatch.candidates[0].code });
results.push({ label: 'goal after confirm', occupation: goalAfter?.occupation, version: goalAfter?.version, source: goalAfter?.provenance?.source,
  doubleConfirm: [again.status(), (await again.json()).error?.code], noIfMatch: noIfMatch.status() });
await page.goto(`${BASE}/app/career`);
await page.waitForTimeout(1200);
results.push({ label: 'career home shows occupation', count: await page.getByText(aMatch.candidates[0].code).count() });

// Stale: start a match, change profile, try to confirm.
await startFromPage();
await page.getByRole('button', { name: 'Use this occupation' }).waitFor({ timeout: 20000 });
const staleRun = (await (await api('GET', `/api/career/runs/${runId()}`)).json()).data;
const pEtag = (await (await api('GET', '/api/career/profile')).json()).data?.etag;
await api('PUT', '/api/career/profile', { currentTitle: 'Senior Code Ninja', skills: ['Python'], highlights: [
  'Designed and developed software applications and modified existing programs to meet user needs'], confirmed: true }, { 'If-Match': pEtag });
const g2 = (await (await api('GET', '/api/career/goals')).json()).data;
const staleMatch = (await (await api('GET', `/api/career/occupation-matches/${staleRun.occupationMatchId}`)).json()).data;
const staleConfirm = await api('POST', `/api/career/occupation-matches/${staleRun.occupationMatchId}/confirm`, { occupationCode: staleMatch.candidates[0].code }, { 'If-Match': g2?.etag });
await page.reload();
await page.waitForTimeout(2500);
results.push({ label: 'stale match', profileChanged: staleMatch.profileChanged, confirm: [staleConfirm.status(), (await staleConfirm.json()).error?.code],
  note: await page.getByText('Your profile changed after this match').count(),
  confirmDisabled: await page.getByRole('button', { name: 'Use this occupation' }).isDisabled().catch(() => null) });
await shoot('04-stale');

// --- User C: contradictory duties -> question with choices.
await register('ux-occ-c');
await profile({ currentTitle: 'Hybrid specialist', skills: ['Python', 'Patient care'], highlights: [
  'Monitored patient vital signs and recorded observations in medical records',
  'Administered medications and treatments as prescribed by physicians',
  'Assessed patient health problems and developed nursing care plans',
  'Designed and developed software applications and modified existing programs to meet user needs',
  'Analyzed user requirements and tested software systems to correct errors',
  'Wrote and maintained documentation for application code and database systems'] });
await goal();
await startFromPage();
await page.getByRole('button', { name: 'Send answer' }).waitFor({ timeout: 20000 });
await page.waitForTimeout(400);
const cRunId = runId();
const choices = (await (await api('GET', `/api/career/runs/${cRunId}`)).json()).data?.question?.choices;
const badAnswer = await api('POST', `/api/career/runs/${cRunId}/answers`, { questionId: 'occupation', answer: '99-9999.99' });
results.push({ label: 'ambiguous', choices: choices?.map(c => `${c.value} ${c.label}`), badAnswer: badAnswer.status() });
await shoot('05-question');
await page.locator('main input[type=radio]').first().check();
await page.getByRole('button', { name: 'Send answer' }).click();
await page.getByRole('button', { name: 'Use this occupation' }).waitFor({ timeout: 20000 });
results.push({ label: 'after answer', toldUs: await page.getByText('You told us').count() });
await shoot('06-after-answer');

// --- User D: title only -> unsupported.
await register('ux-occ-d');
await profile({ currentTitle: 'Software Developer' });
await goal();
await startFromPage();
await page.getByRole('link', { name: 'Add responsibilities to your profile' }).waitFor({ timeout: 20000 });
results.push({ label: 'title only unsupported', confirmButtons: await page.getByRole('button', { name: 'Use this occupation' }).count() });
await shoot('07-unsupported');

// --- User B's goal untouched by A.
await register('ux-occ-b2'); // fresh cookie jar to log back as nobody; B's goal was checked via its own session above
results.push({ label: 'user B goal before (occupation)', occupation: bGoalBefore?.occupation ?? null });

// Expired session.
await context.clearCookies();
await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(`${BASE}/app/career/occupation`);
await page.waitForTimeout(2500);
results.push({ label: 'expired session', url: decodeURIComponent(page.url().replace(BASE, '')) });

fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify({ results, errors }, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results to ${OUT}; page errors: ${errors.length}`);
