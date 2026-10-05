// Live-stack UX review for #380 career agent run (no API mocks; fake text model in LocalDev).
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-agent-review.mjs [outDir]
// Simulated review, not user evidence.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/380');
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
    small: [...document.querySelectorAll('main a, main button, main textarea')]
      .filter(e => e.offsetParent)
      .map(e => [e, e.getBoundingClientRect()])
      .filter(([, r]) => r.height < 24)
      .map(([e, r]) => `${e.tagName} "${(e.textContent || '').trim().slice(0, 30)}" ${Math.round(r.height)}`),
    percent: /\d\s*%/.test(document.querySelector('main')?.innerText ?? ''),
    progressbars: document.querySelectorAll('main progress, main [role=progressbar]').length,
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
  const email = `${prefix}-${Date.now()}@example.com`, pw = 'Passw0rd!Career';
  const reg = await page.request.post(`${BASE}/api/auth/register`, {
    data: { email, password: pw, confirmPassword: pw, firstName: 'Riley', lastName: 'Synthetic', gender: 'Prefer not to say',
      ethnicity: 'Prefer not to say', ageConfirmed: true, acceptTerms: true, turnstileToken: 'local' },
  });
  if (!reg.ok()) throw new Error('register ' + reg.status());
  await page.request.post(`${BASE}/api/auth/dev/confirm-email`);
}

// --- User B (other owner) creates a run first, for cross-user checks later.
await register('ux-agent-b');
await api('PUT', '/api/career/profile', { currentTitle: 'Analyst', skills: ['Excel'], highlights: [], confirmed: true });
const bRun = await (await api('POST', '/api/career/runs', { task: 'profile_summary' }, { 'Idempotency-Key': 'other-owner-key-0001' })).json();
const bRunId = bRun.data?.id;
await context.clearCookies();

// --- User A.
await register('ux-agent-a');
const noProfile = await api('POST', '/api/career/runs', { task: 'profile_summary' }, { 'Idempotency-Key': 'no-profile-key-0001' });
results.push({ label: 'start without profile', status: noProfile.status(), code: (await noProfile.json()).error?.code });
await api('PUT', '/api/career/profile', {
  currentTitle: 'Senior Operations Lead', industry: 'Healthcare', yearsExperience: 9, location: 'Denver, CO',
  skills: ['SQL', 'Tableau', 'Process improvement'], highlights: ['Reduced scheduling backlog by 30 percent across 12 clinics'], confirmed: true,
});

// Real API: idempotency + cross-user.
const k = 'review-idem-key-0001';
const r1 = await api('POST', '/api/career/runs', { task: 'profile_summary' }, { 'Idempotency-Key': k });
const r1Body = await r1.json();
const r2 = await api('POST', '/api/career/runs', { task: 'profile_summary' }, { 'Idempotency-Key': k });
const r3 = await api('POST', '/api/career/runs', { task: 'other_task' }, { 'Idempotency-Key': k });
const missingKey = await api('POST', '/api/career/runs', { task: 'profile_summary' });
const listBefore = (await (await api('GET', '/api/career/runs')).json()).data?.allowance;
await api('GET', `/api/career/runs/${r1Body.data?.id}`);
const listAfter = (await (await api('GET', '/api/career/runs')).json()).data?.allowance;
const cross = await api('GET', `/api/career/runs/${bRunId}`);
const crossCancel = await api('POST', `/api/career/runs/${bRunId}/cancel`);
results.push({
  label: 'real api idempotency/ownership',
  first: r1.status(), replaySameId: r2.status() === 202 && (await r2.json()).data?.id === r1Body.data?.id,
  changedPayload: [r3.status(), (await r3.json()).error?.code], missingKey: missingKey.status(),
  readsFree: JSON.stringify(listBefore) === JSON.stringify(listAfter), allowance: listAfter,
  crossUserGet: cross.status(), crossUserCancel: crossCancel.status(),
});

await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i });
if (await reject.count()) await reject.first().click();

// Flow 1: no goal -> question -> answer -> draft ready -> review.
await page.goto(`${BASE}/app/career/summary?run=${r1Body.data.id}`);
await page.getByRole('heading', { name: 'Draft a profile summary' }).waitFor();
await page.getByRole('button', { name: 'Send answer' }).waitFor({ timeout: 20000 });
await shoot('01-needs-answer');
await page.locator('main textarea').fill('Hiring managers for clinic operations roles');
await page.getByRole('button', { name: 'Send answer' }).click();
await page.getByRole('link', { name: 'Review the draft' }).waitFor({ timeout: 20000 });
await shoot('02-draft-ready');
const stepsText = await page.locator('main ol').first().innerText().catch(() => '');
results.push({ label: 'steps shown', steps: stepsText.split('\n') });
await page.getByRole('link', { name: 'Review the draft' }).click();
await page.waitForURL(/career\/import\?proposal=/);
await page.waitForTimeout(1200);
results.push({ label: 'review page', text: (await page.locator('main').innerText()).slice(0, 500) });
await shoot('03-review-agent-proposal');
const profile = (await (await api('GET', '/api/career/profile')).json()).data;
results.push({ label: 'profile facts unchanged by run', summary: profile?.summary ?? null, version: profile?.activeVersion ?? profile?.version });

// Flow 2: with a goal, start from the page, then cancel a fresh run while it waits.
await api('POST', '/api/career/goals', { targetRole: 'Operations manager', targetLocation: 'Denver, CO', workArrangement: 'hybrid', weeklyEffortHours: 6, confirmed: true });
await page.goto(`${BASE}/app/career/summary`);
await page.getByRole('button', { name: 'Start draft' }).waitFor();
await shoot('04-start');
await page.getByRole('button', { name: 'Start draft' }).click();
await page.waitForURL(/run=/);
await page.getByRole('link', { name: 'Review the draft' }).waitFor({ timeout: 20000 });
// Reconnect: reload a finished run by URL.
await page.reload();
await page.getByRole('link', { name: 'Review the draft' }).waitFor({ timeout: 10000 });
results.push({ label: 'reconnect after reload', ok: true, url: page.url().replace(BASE, '') });

// Stale profile: change profile after the run finished -> profileChanged note + 412 on accept.
const etag = (await (await api('GET', '/api/career/profile')).json()).data?.etag;
const stalePut = await api('PUT', '/api/career/profile', { currentTitle: 'Operations Director', skills: ['SQL'], highlights: [], confirmed: true },
  { 'If-Match': etag });
results.push({ label: 'profile updated after run', status: stalePut.status() });
await page.reload();
await page.waitForTimeout(2500);
results.push({ label: 'profile changed note', count: await page.getByText('Your profile changed after this draft started').count() });
const changedRun = (await (await api('GET', `/api/career/runs/${new URL(page.url()).searchParams.get('run')}`)).json()).data;
const newEtag = (await (await api('GET', '/api/career/profile')).json()).data?.etag;
const proposal = (await (await api('GET', `/api/career/profile/proposals/${changedRun?.proposalId}`)).json()).data;
const acceptStale = await api('POST', `/api/career/profile/proposals/${changedRun?.proposalId}/accept`,
  { itemIds: proposal?.items?.map(i => i.id) ?? [] }, { 'If-Match': newEtag });
results.push({ label: 'accept stale proposal', profileChanged: changedRun?.profileChanged, status: acceptStale.status(),
  code: (await acceptStale.json().catch(() => ({}))).error?.code });
await shoot('05-profile-changed');

// Cancel: a fresh user with no goal waits for an answer, so Cancel is reliably reachable.
await context.clearCookies();
await register('ux-agent-c');
await api('PUT', '/api/career/profile', { currentTitle: 'Data analyst', skills: ['SQL'], highlights: [], confirmed: true });
await page.goto(`${BASE}/app/career/summary`);
await page.getByRole('button', { name: 'Start draft' }).click();
await page.waitForURL(/run=/);
await page.getByRole('button', { name: 'Send answer' }).waitFor({ timeout: 20000 });
await page.getByRole('button', { name: 'Cancel' }).click();
await page.getByText('Stopped. Nothing was changed on your profile.').waitFor({ timeout: 10000 });
const cancelledId = new URL(page.url()).searchParams.get('run');
const again = await api('POST', `/api/career/runs/${cancelledId}/cancel`);
results.push({ label: 'cancel', againStatus: again.status(), status: (await again.json()).data?.status,
  proposalId: (await (await api('GET', `/api/career/runs/${cancelledId}`)).json()).data?.proposalId ?? null });
await shoot('06-cancelled');
const runs = (await (await api('GET', '/api/career/runs')).json()).data;
results.push({ label: 'runs list', statuses: runs?.runs?.map(r => r.status), allowance: runs?.allowance });

// Expired session.
await context.clearCookies();
await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(`${BASE}/app/career/summary`);
await page.waitForTimeout(2500);
results.push({ label: 'expired session', url: decodeURIComponent(page.url().replace(BASE, '')) });

fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify({ results, errors }, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results to ${OUT}; page errors: ${errors.length}`);
