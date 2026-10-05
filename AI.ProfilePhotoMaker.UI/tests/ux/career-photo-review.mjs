// Live-stack UX review for #391 career photo handoff.
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-photo-review.mjs [outDir]
// LocalDev cannot generate photos (dummy provider keys), so the populated photo list and
// the selection PUT are the only mocked calls; everything else hits the real API.
// Simulated review, not user evidence.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/391');
const AXE = process.env.AXE_PATH;
fs.mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined });
const context = await browser.newContext();
const page = await context.newPage();
const errors = [];
page.on('pageerror', e => errors.push(e.message));
const offOrigin = [];
page.on('framenavigated', f => {
  if (f === page.mainFrame() && !f.url().startsWith(BASE)) offOrigin.push(f.url());
});
const results = [];

async function measure(label) {
  const checks = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    small: [...document.querySelectorAll('main a, main button, main input[type=radio]')]
      .filter(e => e.offsetParent)
      .map(e => [e, (e.type === 'radio' ? e.closest('label') ?? e : e).getBoundingClientRect()])
      .filter(([, r]) => r.height < 24)
      .map(([e, r]) => `${e.tagName} "${(e.textContent || e.getAttribute('aria-label') || '').trim().slice(0, 30)}" ${Math.round(r.height)}`),
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

const email = `ux-photo-${Date.now()}@example.com`, pw = 'Passw0rd!Career';
const reg = await page.request.post(`${BASE}/api/auth/register`, {
  data: { email, password: pw, confirmPassword: pw, firstName: 'Riley', lastName: 'Synthetic', gender: 'Prefer not to say',
    ethnicity: 'Prefer not to say', ageConfirmed: true, acceptTerms: true, turnstileToken: 'local' },
});
if (!reg.ok()) throw new Error('register ' + reg.status());
await page.request.post(`${BASE}/api/auth/dev/confirm-email`);
await page.request.put(`${BASE}/api/career/profile`, { data: { currentTitle: 'Operations lead', skills: ['SQL'], highlights: [], confirmed: true } });
const goalRes = await page.request.post(`${BASE}/api/career/goals`, {
  data: { targetRole: 'Operations manager', targetLocation: 'Denver, CO', workArrangement: 'hybrid', weeklyEffortHours: 6, confirmed: true },
});
const goalId = (await goalRes.json()).data?.id;
results.push({ label: 'goal created', status: goalRes.status(), goalId });

// Real API checks: empty list, cross-user/missing asset, credits untouched.
const before = await (await page.request.get(`${BASE}/api/credits/status`)).text();
const list = await page.request.get(`${BASE}/api/career/photos`);
const listText = await list.text();
const missing = await page.request.put(`${BASE}/api/career/photos/selection`, { data: { processedImageId: 999999 } });
const bad = await page.request.put(`${BASE}/api/career/photos/selection`, { data: { processedImageId: 0 } });
const after = await (await page.request.get(`${BASE}/api/credits/status`)).text();
results.push({
  label: 'real api',
  list: list.status(), listBody: listText.slice(0, 300), privatePathLeak: /generated-private|RawImage/i.test(listText),
  selectMissing: [missing.status(), (await missing.json()).error?.code ?? (await missing.text()).slice(0, 120)],
  selectZero: bad.status(), creditsUnchanged: before === after,
});

await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i });
if (await reject.count()) await reject.first().click();

await page.goto(`${BASE}/app/career/materials`);
await page.getByRole('heading', { name: 'Career materials' }).waitFor();
await page.waitForTimeout(600);
await shoot('01-materials-empty-real');

// Populated state (mocked list only; LocalDev cannot produce finished photos).
const img = 'data:image/svg+xml;utf8,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200"><rect width="200" height="200" fill="#8aa"/><circle cx="100" cy="80" r="40" fill="#dcc"/><rect x="45" y="130" width="110" height="70" rx="30" fill="#556"/></svg>');
let selected = null;
await page.route('**/api/career/photos', r => r.fulfill({ json: { success: true, data: {
  photos: [
    { id: 42, imageUrl: img, createdAt: '2026-09-28T10:00:00Z', style: 'linkedin', isWatermarkedPreview: false },
    { id: 41, imageUrl: img, createdAt: '2026-09-20T10:00:00Z', style: 'corporate', isWatermarkedPreview: true },
  ],
  selectedPhotoId: selected, selectedPhotoAvailable: true,
  entitlements: [{ packageCode: 'starter_package', packageName: 'Starter Package', remainingCandidates: 2, remainingRefinements: 3,
    remainingPremiumAugmentations: 0, platformExportKitAvailable: true, expiresAt: null }],
} } }));
await page.route('**/api/career/photos/selection', r => {
  selected = 42;
  return r.fulfill({ json: { success: true, data: { selectedPhotoId: 42, careerGoalId: goalId, selectedAt: new Date().toISOString() } } });
});
await page.reload();
await page.getByRole('heading', { name: 'Career materials' }).waitFor();
await page.waitForTimeout(600);
results.push({ label: 'preview radio disabled', disabled: await page.locator('#photo-41').isDisabled() });
await shoot('02-materials-populated-mocked');
await page.locator('label[for=photo-42]').click();
await page.getByRole('button', { name: 'Use this photo' }).click();
await page.getByText('Photo saved to your career profile.').waitFor();
await shoot('03-photo-chosen-mocked');

// Handoff to the real photo workspace and back.
await page.getByRole('link', { name: 'Improve in photo workspace' }).first().click();
await page.waitForURL(/\/app\/enhance/);
results.push({ label: 'handoff url', url: page.url().replace(BASE, '') });
const banner = page.locator('.career-return-banner');
await banner.waitFor({ timeout: 15000 });
results.push({ label: 'workspace banner', text: (await banner.innerText()).trim() });
await page.waitForTimeout(800);
await shoot('04-workspace-back-banner');
await banner.getByRole('link').click();
await page.waitForURL(/\/app\/career\/materials/);
await page.waitForTimeout(500);
results.push({ label: 'returned', url: page.url().replace(BASE, ''), goalChangedNote: await page.getByText('Your career goal changed').count() });

// Open-redirect attempts: no banner, never off-origin.
for (const key of ['https://evil.com', '//evil.com', 'javascript:alert(1)', '/app/career/materials']) {
  await page.goto(`${BASE}/app/enhance?careerReturn=${encodeURIComponent(key)}&careerGoal=${goalId}`);
  await page.waitForTimeout(1500);
  results.push({ label: `open-redirect ${key}`, banner: await page.locator('.career-return-banner').count(), url: page.url().replace(BASE, '') });
}

// Goal changed while away.
await page.goto(`${BASE}/app/career/materials?careerGoal=9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d`);
await page.getByText('Your career goal changed while you were in the photo workspace.').waitFor();
await shoot('05-goal-changed');

// Expired session: drop auth, reload materials.
await page.unroute('**/api/career/photos');
await context.clearCookies();
await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(`${BASE}/app/career/materials`);
await page.waitForTimeout(2500);
results.push({ label: 'expired session', url: decodeURIComponent(page.url().replace(BASE, '')) });
await shoot('06-expired-session');

results.push({ label: 'off-origin navigations', urls: offOrigin });
fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify({ results, errors }, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results to ${OUT}; page errors: ${errors.length}`);
