// Live-stack journey review for #393 (no API mocks; fake text model in LocalDev).
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-journey-review.mjs [outDir]
// Simulated review, not user evidence.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/393');
const AXE = process.env.AXE_PATH;
fs.mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({
  executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined,
});
const context = await browser.newContext({ acceptDownloads: true });
const page = await context.newPage();
const errors = [],
  dialogs = [],
  results = [],
  steps = [];
page.on('pageerror', e => errors.push(`${page.url().replace(BASE, '')}: ${e.message}`));
page.on('dialog', async d => {
  dialogs.push(d.message());
  await d.dismiss();
});

const DUTIES = [
  'Modified existing software to correct errors, adapt it to new hardware, or improve its performance',
  'Analyzed user needs and software requirements to determine feasibility of design within time and cost constraints',
  'Consulted with customers about software system design and maintenance',
];
const LABELS = {
  create_profile: 'Create your profile',
  confirm_profile: 'Confirm your profile',
  set_goal: 'Set your goal',
  confirm_occupation: 'Confirm your occupation',
  build_brief: 'Build your market brief',
  analyze_pay: 'Analyze pay',
  build_roadmap: 'Build your roadmap',
  accept_roadmap: 'Review and accept your roadmap',
  draft_resume: 'Draft your resume',
  export_material: 'Export your materials',
};

async function measure(label) {
  const checks = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    small: [...document.querySelectorAll('main a, main button, main textarea')]
      .filter(e => e.offsetParent)
      .map(e => [e, e.getBoundingClientRect()])
      .filter(([, r]) => r.height < 24)
      .map(
        ([e, r]) =>
          `${e.tagName} "${(e.textContent || '').trim().slice(0, 30)}" ${Math.round(r.height)}`
      ),
    percent:
      /\d\s*%/.test(document.querySelector('main')?.innerText ?? '') &&
      /progress|complete/i.test(document.querySelector('main')?.innerText ?? ''),
    h1: document.querySelectorAll('h1').length,
    title: document.title,
  }));
  let axe = [];
  if (AXE) {
    await page.addScriptTag({ path: AXE });
    axe = await page.evaluate(async () =>
      (
        await window.axe.run(document.querySelector('main') ?? document, {
          runOnly: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'],
        })
      ).violations.map(
        v =>
          `${v.id}: ${v.nodes
            .slice(0, 3)
            .map(n => n.target.join(' '))
            .join(' | ')}`
      )
    );
  }
  results.push({ label, ...checks, axe });
}
async function shoot(name, all = true) {
  const vps = all
    ? [
        ['desktop', 1280, 900],
        ['mobile-390', 390, 844],
        ['mobile-320', 320, 720],
      ]
    : [['desktop', 1280, 900]];
  for (const [vp, w, h] of vps) {
    await page.setViewportSize({ width: w, height: h });
    await page.waitForTimeout(250);
    await page.screenshot({ path: path.join(OUT, `${name}-${vp}.png`), fullPage: true });
    await measure(`${name} @${vp}`);
  }
  await page.setViewportSize({ width: 1280, height: 900 });
}
const api = async (method, url, data, headers) => {
  const r = await page.request.fetch(`${BASE}${url}`, { method, data, headers });
  return r;
};
const json = async (method, url, data, headers) =>
  (await (await api(method, url, data, headers)).json().catch(() => ({}))).data;
let keyN = 0;
const key = () => `journey-key-${Date.now()}-${++keyN}-xxxxxxxx`;

async function register(prefix) {
  const email = `${prefix}-${Date.now()}@example.com`,
    pw = 'Passw0rd!Career';
  const reg = await page.request.post(`${BASE}/api/auth/register`, {
    data: {
      email,
      password: pw,
      confirmPassword: pw,
      firstName: 'Riley',
      lastName: 'Synthetic',
      gender: 'Prefer not to say',
      ethnicity: 'Prefer not to say',
      ageConfirmed: true,
      acceptTerms: true,
      turnstileToken: 'local',
    },
  });
  if (!reg.ok()) throw new Error('register ' + reg.status());
  await page.request.post(`${BASE}/api/auth/dev/confirm-email`);
}
async function waitRun(
  id,
  until = ['completed', 'failed', 'needs_input', 'cancelled'],
  ms = 40000
) {
  const end = Date.now() + ms;
  let run;
  while (Date.now() < end) {
    run = await json('GET', `/api/career/runs/${id}`);
    if (until.includes(run?.status)) return run;
    await page.waitForTimeout(700);
  }
  return run;
}
async function journey() {
  return await json('GET', '/api/career/journey');
}

// Asserts the home "Next step" matches journey.nextAction, clicks it, checks landing page.
async function homeStep(step, shot) {
  const j = await journey();
  const k = j?.nextAction?.key ?? 'none(no journey)';
  await page.goto(`${BASE}/app/career`);
  await page.waitForSelector('main h1');
  await page.waitForTimeout(1200);
  const label = (
    await page
      .locator('[data-next]')
      .first()
      .innerText()
      .catch(() => '')
  ).trim();
  const expect = LABELS[k] ?? '';
  const route = j?.nextAction?.route ?? '/app/career/setup';
  let landed = '',
    ok = label === expect;
  if (shot) await shoot(`${shot}-home`, false);
  if (label) {
    await page.locator('[data-next]').first().click();
    await page.waitForTimeout(1200);
    landed = new URL(page.url()).pathname;
    ok = ok && landed === new URL(route, BASE).pathname;
  } else if (k !== 'none') ok = false;
  const row = {
    step,
    nextAction: k,
    route,
    label,
    landed,
    ok,
    screenshot: shot ? `${shot}-home-desktop.png` : '',
  };
  steps.push(row);
  results.push({ label: `home step: ${step}`, ...row });
  if (shot) await shoot(`${shot}-page`, false);
  return j;
}

async function setupUser(prefix, profile, goal) {
  await register(prefix);
  await api('PUT', '/api/career/profile', { ...profile, confirmed: true });
  await api('POST', '/api/career/goals', { ...goal, confirmed: true });
}
async function matchOccupation(pick) {
  const run = await json(
    'POST',
    '/api/career/runs',
    { task: 'occupation_match' },
    { 'Idempotency-Key': key() }
  );
  let r = await waitRun(run.id);
  if (r?.status === 'needs_input') {
    const choice =
      r.question?.choices?.find(c => c.value !== 'none' && (!pick || c.value === pick)) ??
      r.question?.choices?.find(c => c.value !== 'none');
    await api('POST', `/api/career/runs/${run.id}/answers`, {
      questionId: 'occupation',
      answer: choice?.value ?? 'none',
    });
    r = await waitRun(run.id);
  }
  const match = r?.occupationMatchId
    ? await json('GET', `/api/career/occupation-matches/${r.occupationMatchId}`)
    : null;
  return { run: r, match };
}
async function confirmMatch(match, code) {
  const goal = await json('GET', '/api/career/goals');
  const r = await api(
    'POST',
    `/api/career/occupation-matches/${match.id}/confirm`,
    { occupationCode: code },
    { 'If-Match': goal.etag }
  );
  return r.status();
}

// Run a page's primary start button, then wait for result in the UI.
async function uiRun(route, button, doneLocator, shot) {
  await page.goto(`${BASE}${route}`);
  await page.getByRole('button', { name: button }).first().waitFor({ timeout: 15000 });
  await page.getByRole('button', { name: button }).first().click();
  await page.locator(doneLocator).first().waitFor({ timeout: 45000 });
  await page.waitForTimeout(500);
  if (shot) await shoot(shot);
}

process.on('uncaughtException', async e => {
  results.push({
    label: 'SCRIPT ABORTED',
    error: String(e.message).slice(0, 400),
    url: page.url(),
  });
  try {
    await page.screenshot({ path: path.join(OUT, 'aborted.png'), fullPage: true });
    results.push({
      label: 'aborted text',
      text: (await page.locator('main').innerText()).slice(0, 600),
    });
  } catch {}
  fs.writeFileSync(
    path.join(OUT, 'checks.json'),
    JSON.stringify({ steps, results, errors, dialogs }, null, 2)
  );
  console.error('ABORT', e.message.slice(0, 300));
  process.exit(1);
});
const clientCfg = await (await page.request.get(`${BASE}/api/config/client`)).json();
results.push({
  label: 'client config careerWorkspace',
  value: clientCfg.data?.features?.careerWorkspace ?? clientCfg.features?.careerWorkspace,
});

await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i });
if (await reject.count()) await reject.first().click();

// ===== User B: other owner, created first for cross-user checks.
await register('ux-journey-b');
await api('PUT', '/api/career/profile', {
  currentTitle: 'Analyst',
  skills: ['Excel'],
  highlights: [],
  confirmed: true,
});
const bRes = await json(
  'POST',
  '/api/career/runs',
  { task: 'profile_summary' },
  { 'Idempotency-Key': key() }
);
await context.clearCookies();

// ===== User A: covered occupation.
await register('ux-journey-a');
await homeStep('0 empty home', '00-empty');

// Profile via the real setup form.
await page.goto(`${BASE}/app/career/setup`);
await page.fill('#profile-currentTitle', 'Software Developer');
await page.fill('#profile-industry', 'Technology');
await page.fill('#profile-yearsExperience', '6');
await page.fill('#profile-location', 'Denver, CO');
const skillInput = page.locator('input[id*=skill]').first();
if (await skillInput.count()) {
  await skillInput.fill('Python');
  await page.getByRole('button', { name: 'Add skill' }).click();
}
const hlInput = page.locator('input[id*=highlight]').first();
if (await hlInput.count()) {
  for (const h of DUTIES) {
    await hlInput.fill(h);
    await page.getByRole('button', { name: 'Add highlight' }).click();
  }
}
await page.check('#profile-confirmed');
await shoot('01-setup-profile', false);
await page.getByRole('button', { name: 'Save facts and continue' }).click();
await page.waitForSelector('#goal-targetRole', { timeout: 10000 });
await homeStep('1 profile saved (before goal)', '01-profile');
await page.goto(`${BASE}/app/career/setup`);
if (!(await page.locator('#goal-targetRole').count())) {
  // setup may restart at step 1; use the profile page, which shows the goal form
  await page.goto(`${BASE}/app/career/profile`);
}
await page.fill('#goal-targetRole', 'Software developer');
await page.fill('#goal-targetLocation', 'Denver, CO');
await page.selectOption('#goal-workArrangement', 'hybrid');
await page.fill('#goal-desiredPayMin', '150000');
await page.fill('#goal-weeklyEffortHours', '6');
await page.check('#goal-confirmed');
await page.getByRole('button', { name: 'Save goal' }).click();
await page.waitForTimeout(1500);
await homeStep('2 goal set', '02-goal');

// Occupation.
const { run: mRun, match } = await matchOccupation('15-1252.00');
results.push({
  label: 'occupation match run',
  status: mRun?.status,
  candidates: match?.candidates?.map(c => `${c.code} ${c.strength}`),
});
await homeStep('3 occupation proposed', '03-occupation');
await page.goto(`${BASE}/app/career/occupation?run=${mRun.id}`);
await page.waitForTimeout(1500);
await shoot('03b-occupation-ui', false);
const code =
  match?.candidates?.find(c => c.code === '15-1252.00')?.code ?? match?.candidates?.[0]?.code;
results.push({ label: 'confirm occupation', code, status: await confirmMatch(match, code) });

// Market brief.
await homeStep('4 occupation confirmed -> brief', '04-brief');
await uiRun('/app/career/market', 'Build my market brief', '#brief-heading', '04b-market-result');
const brief = (await json('GET', '/api/career/market-briefs'))?.briefs?.[0];
results.push({ label: 'market brief', status: brief?.status, area: brief?.areaTitle });

// Pay: covered.
await homeStep('5 brief done -> pay', '05-pay');
await uiRun('/app/career/pay', 'Build my pay analysis', '#pay-benchmark', '05b-pay-covered');
const pay = (await json('GET', '/api/career/pay-analyses'))?.analyses?.[0];
const payFull = pay ? await json('GET', `/api/career/pay-analyses/${pay.id}`) : null;
results.push({
  label: 'pay covered',
  status: pay?.status,
  area: pay?.areaTitle,
  personalizedAvailable: pay?.personalizedAvailable,
  personalized: payFull?.sections?.find(s => s.key === 'personalized')?.status,
  scenario: payFull?.sections?.find(s => s.key === 'scenario')?.status,
  pageText: (await page.locator('main').innerText())
    .match(/personalized|prediction|not advertised pay|insufficient/gi)
    ?.slice(0, 5),
});

// Roadmap.
await homeStep('6 pay done -> roadmap', '06-roadmap');
await uiRun('/app/career/roadmap', 'Build my roadmap', '[data-option]', '06b-roadmap-proposed');
await homeStep('7 roadmap proposed -> accept', '07-accept');
const rmId0 = (await json('GET', '/api/career/roadmaps'))?.roadmaps?.[0]?.id;
await page.goto(`${BASE}/app/career/roadmap?roadmap=${rmId0}`);
await page.waitForSelector('[data-option]');
await page.locator('input[name=path]').first().check();
await page
  .getByRole('button', { name: /Accept|accept this path/i })
  .first()
  .click();
await page.locator('[data-confirm]').click();
await page.waitForTimeout(2000);
const rms = (await json('GET', '/api/career/roadmaps'))?.roadmaps ?? [];
results.push({ label: 'roadmap accepted', status: rms[0]?.status });
await shoot('07b-roadmap-accepted', false);

// Resume.
await homeStep('8 roadmap accepted -> resume', '08-resume');
await uiRun('/app/career/resume', 'Draft my resume', '#contact-heading', '08b-resume-ready');
const mats = (await json('GET', '/api/career/materials?kind=resume'))?.materials ?? [];
const resumeId = mats[0]?.id;
const mat = resumeId ? await json('GET', `/api/career/materials/${resumeId}`) : null;
results.push({
  label: 'resume material',
  id: resumeId,
  sections: mat?.sections?.map(s => `${s.key}:${s.lines?.length}`),
  origins: [...new Set(mat?.sections?.flatMap(s => s.lines.map(l => l.origin)))],
});

// Exports through the real UI download.
await homeStep('9 resume drafted -> export', '09-export');
await page.goto(`${BASE}/app/career/resume?material=${resumeId}`);
await page.locator('[data-export-panel]').waitFor({ timeout: 15000 });
for (const [fmt, radioLabel, magic] of [
  ['pdf', 'PDF', '%PDF'],
  ['docx', 'Word', 'PK'],
]) {
  await page.getByLabel(radioLabel, { exact: true }).check();
  const [dl] = await Promise.all([
    page.waitForEvent('download', { timeout: 20000 }),
    page.getByRole('button', { name: 'Download', exact: true }).click(),
  ]);
  const p = await dl.path();
  const head = fs.readFileSync(p).subarray(0, 4).toString('latin1');
  results.push({
    label: `export ${fmt}`,
    fileName: dl.suggestedFilename(),
    magic: head,
    ok: head.startsWith(magic),
    bytes: fs.statSync(p).size,
  });
}
await shoot('09b-export-done', false);

// Summary.
await page.goto(`${BASE}/app/career/summary-draft`);
await page.getByRole('button', { name: 'Draft my summary' }).click();
await page.locator('textarea').first().waitFor({ timeout: 45000 });
await page.waitForTimeout(600);
await shoot('10-summary');
const sums = (await json('GET', '/api/career/materials?kind=summary'))?.materials ?? [];
results.push({ label: 'summary material', count: sums.length });
await homeStep('10 summary drafted', '10-after-summary');

// Privacy page.
await page.goto(`${BASE}/app/career/privacy`);
await page.getByRole('heading', { name: 'Your career data' }).waitFor();
await shoot('11-privacy');
const [pdl] = await Promise.all([
  page.waitForEvent('download', { timeout: 15000 }),
  page
    .getByRole('button', { name: /Download my career data|Download/ })
    .first()
    .click(),
]);
results.push({
  label: 'privacy export',
  fileName: pdl.suggestedFilename(),
  startsWithBrace:
    fs
      .readFileSync(await pdl.path())
      .subarray(0, 1)
      .toString() === '{',
});

// Optional photo continuation (no purchase).
await page.goto(`${BASE}/app/career/materials`);
await page.getByRole('heading', { name: 'Profile photo (optional)' }).waitFor();
await page.waitForTimeout(800);
await shoot('12-materials-photo');
results.push({
  label: 'photo optional',
  text: (await page.locator('#photo-heading').locator('..').innerText()).slice(0, 400),
  continueLink: await page.getByRole('link', { name: 'Continue without a photo' }).count(),
  createLink: await page.getByRole('link', { name: 'Create a new photo' }).count(),
});

// Reload mid-run recovery: start a run, immediately view home, then reload.
const slow = await json(
  'POST',
  '/api/career/runs',
  { task: 'roadmap' },
  { 'Idempotency-Key': key() }
);
await page.goto(`${BASE}/app/career`);
await page.waitForTimeout(300);
await page.reload();
await page.waitForTimeout(1500);
const homeText = await page.locator('main').innerText();
const sRun = await waitRun(slow.id);
results.push({
  label: 'reload mid-run',
  runStatus: sRun?.status,
  stillWorkingOrResult: /Still working|Latest result|Try again/.test(homeText),
  home: homeText.slice(0, 300),
});
await shoot('13-reload-midrun', false);

// Cross-user checks (A's ids as B).
const aRoadmapId = rms[0]?.id;
const aSumId = sums[0]?.id;
await context.clearCookies();
// B's sign-in via cookie lost: register a fresh user C (second user)
await register('ux-journey-c');
const cross = {};
for (const [n, u] of [
  ['roadmap', `/api/career/roadmaps/${aRoadmapId}`],
  ['resume', `/api/career/materials/${resumeId}`],
  ['summary', `/api/career/materials/${aSumId}`],
  ['resumeVersions', `/api/career/materials/${resumeId}/versions`],
  ['brief', `/api/career/market-briefs/${brief?.id}`],
  ['pay', `/api/career/pay-analyses/${pay?.id}`],
  ['runB-as-C', `/api/career/runs/${bRes?.id}`],
])
  cross[n] = (await api('GET', u)).status();
const xexp = await api('POST', `/api/career/materials/${resumeId}/exports`, { format: 'pdf' });
cross.exportCreate = xexp.status();
results.push({ label: 'cross-user reads', ...cross });

// Malicious context: C gets an XSS title.
await api('PUT', '/api/career/profile', {
  currentTitle: '<img src=x onerror=alert(1)>',
  skills: ['<script>alert(2)</script>'],
  highlights: [],
  confirmed: true,
});
await page.goto(`${BASE}/app/career/profile`);
await page.waitForTimeout(1500);
const titleVal = await page.inputValue('#profile-currentTitle').catch(() => '');
await page.goto(`${BASE}/app/career/materials`);
await page.waitForTimeout(800);
await page.goto(`${BASE}/app/career?next=javascript:alert(1)`);
await page.waitForTimeout(1500);
const injected = await page.evaluate(
  () => document.querySelectorAll('main img[src="x"], main script').length
);
results.push({
  label: 'malicious context',
  titleRenderedAsText: titleVal === '<img src=x onerror=alert(1)>',
  injectedElements: injected,
  dialogs: [...dialogs],
  urlAfterNext: page.url().replace(BASE, ''),
  homeShowsJavascriptLink: await page.locator('a[href^="javascript:"]').count(),
});
await shoot('14-malicious-home', false);

// ===== User E: unusual occupation (honest "no match" outcome).
await context.clearCookies();
await setupUser(
  'ux-journey-e',
  {
    currentTitle: 'Beekeeper',
    industry: 'Agriculture',
    yearsExperience: 3,
    location: 'Hyder, AK',
    skills: ['Apiary management'],
    highlights: ['Managed honey bee colonies and harvested honey'],
  },
  { targetRole: 'Apiarist', targetLocation: 'Hyder, AK', weeklyEffortHours: 3 }
);
const odd = await matchOccupation();
results.push({
  label: 'unusual occupation match',
  status: odd.match?.status,
  candidates: odd.match?.candidates?.map(c => `${c.code} ${c.strength}`),
  guidance: odd.match?.guidance,
});
await homeStep('U1 unusual: occupation unsupported', 'U1-unusual-occupation');
await page.goto(`${BASE}/app/career/occupation?run=${odd.run?.id}`);
await page.waitForTimeout(1500);
await shoot('U1b-unusual-occupation-ui', false);

// ===== User D: sparse case = covered occupation in a tiny, unresolved area.
await context.clearCookies();
await setupUser(
  'ux-journey-d',
  {
    currentTitle: 'Software Developer',
    industry: 'Technology',
    yearsExperience: 3,
    location: 'Hyder, AK',
    skills: ['Python'],
    highlights: DUTIES,
  },
  { targetRole: 'Software developer', targetLocation: 'Hyder, AK', weeklyEffortHours: 3 }
);
const sparse = await matchOccupation('15-1252.00');
results.push({
  label: 'sparse match',
  status: sparse.match?.status,
  candidates: sparse.match?.candidates?.map(c => `${c.code} ${c.strength}`),
});
let sparseConfirmed = false;
const cand = sparse.match?.candidates?.find(c => c.code === '15-1252.00');
if (cand && sparse.match.status === 'proposed')
  sparseConfirmed = (await confirmMatch(sparse.match, cand.code)) === 200;
results.push({ label: 'sparse confirm', sparseConfirmed, code: cand?.code });
if (!sparseConfirmed)
  results.push({ label: 'SPARSE SETUP FAILED', failed: true, status: sparse.match?.status });
if (sparseConfirmed) {
  await homeStep('S1 sparse: brief', 'S1-sparse-brief');
  await uiRun('/app/career/market', 'Build my market brief', '#brief-heading', 'S1b-sparse-market');
  const sb = (await json('GET', '/api/career/market-briefs'))?.briefs?.[0];
  const sbf = sb ? await json('GET', `/api/career/market-briefs/${sb.id}`) : null;
  results.push({
    label: 'brief sparse',
    status: sbf?.status,
    resolution: sbf?.location?.resolution,
    sections: sbf?.sections?.map(s => `${s.key}:${s.status}${s.reason ? '/' + s.reason : ''}`),
  });
  await homeStep('S2 sparse: pay', 'S2-sparse-pay');
  await uiRun('/app/career/pay', 'Build my pay analysis', '#pay-benchmark', 'S2b-sparse-pay');
  const sp = (await json('GET', '/api/career/pay-analyses'))?.analyses?.[0];
  const spf = sp ? await json('GET', `/api/career/pay-analyses/${sp.id}`) : null;
  results.push({
    label: 'pay sparse',
    status: sp?.status,
    area: sp?.areaTitle,
    resolution: spf?.location?.resolution,
    sections: spf?.sections?.map(s => `${s.key}:${s.status}${s.reason ? '/' + s.reason : ''}`),
    unavailableShown: /not available|unavailable|not published|national/i.test(
      await page.locator('main').innerText()
    ),
  });
}

fs.writeFileSync(
  path.join(OUT, 'checks.json'),
  JSON.stringify({ steps, results, errors, dialogs }, null, 2)
);
await browser.close();
const failures = [
  ...steps.filter(s => !s.ok).map(s => `step failed: ${s.step}`),
  ...results.filter(r => r.failed).map(r => `failed: ${r.label}`),
  ...results
    .filter(r => r.overflow > 0 || r.h1 > 1 || r.axe?.length)
    .map(r => `check failed: ${r.label}`),
  ...errors.map(e => `page error: ${e}`),
  ...dialogs.map(d => `dialog: ${d}`),
];
console.log(
  `Wrote ${results.length} results to ${OUT}; steps ok ${steps.filter(s => s.ok).length}/${steps.length}; page errors: ${errors.length}; dialogs: ${dialogs.length}`
);
if (failures.length) {
  console.error(`FAILED (${failures.length}):\n${failures.join('\n')}`);
  process.exit(1);
}
