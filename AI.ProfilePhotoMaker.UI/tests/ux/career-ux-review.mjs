// Career UX review harness (#378). Drives the REAL running UI + API (no mocks):
//   1. scripts/career-localdev-api.sh start   (LocalDev API, career flag on)
//   2. npm run dev:local                        (UI on :4200, proxies /api)
//   3. AXE_PATH=/path/to/axe.min.js node tests/ux/career-ux-review.mjs [outDir]
// Registers a synthetic user, walks setup -> home -> profile, and for each page
// and viewport saves a screenshot plus measurable checks (axe WCAG A/AA, horizontal
// overflow, touch targets, focus visibility, heading structure). The output feeds
// the Impeccable review in docs/testing/career/. A simulated review is NOT
// target-user evidence.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/378');
const AXE = process.env.AXE_PATH;
const VIEWPORTS = [
  { name: 'desktop', width: 1280, height: 900 },
  { name: 'mobile-390', width: 390, height: 844 },
  { name: 'mobile-320', width: 320, height: 720 },
];
fs.mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({
  executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined,
});
const context = await browser.newContext();
const page = await context.newPage();
const results = [];

async function register() {
  const email = `ux-${Date.now()}@example.com`;
  const password = 'Passw0rd!Career';
  const res = await page.request.post(`${BASE}/api/auth/register`, {
    data: {
      email,
      password,
      confirmPassword: password,
      firstName: 'Riley',
      lastName: 'Synthetic',
      gender: 'Prefer not to say',
      ethnicity: 'Prefer not to say',
      ageConfirmed: true,
      acceptTerms: true,
      turnstileToken: 'local',
    },
  });
  if (!res.ok()) throw new Error(`register failed ${res.status()} ${await res.text()}`);
  const confirm = await page.request.post(`${BASE}/api/auth/dev/confirm-email`);
  if (!confirm.ok())
    throw new Error(`dev confirm failed ${confirm.status()} (API must run as LocalDev)`);
  // The app guard also checks client-side auth state; the API session cookie is the authority.
  const body = await res.json();
  await page.goto(`${BASE}/`);
  // Dismiss the site-wide cookie banner the way a user would; it is fixed to the
  // viewport bottom and otherwise covers form actions (existing app behaviour).
  const reject = page.getByRole('button', { name: /reject non-essential/i });
  if (await reject.count()) await reject.first().click();
  await page.evaluate(token => {
    localStorage.setItem('authToken', token);
    localStorage.setItem('token', token);
  }, body.token);
}

async function measure(label) {
  const checks = await page.evaluate(() => {
    const doc = document.documentElement;
    const overflow = doc.scrollWidth - doc.clientWidth;
    const small = [
      ...document.querySelectorAll('main a, main button, main input, main select, main textarea'),
    ]
      .filter(
        el =>
          el.offsetParent !== null && !(el instanceof HTMLInputElement && el.type === 'checkbox')
      )
      .map(el => ({ el, r: el.getBoundingClientRect() }))
      .filter(({ r }) => r.height < 44 || r.width < 24)
      .map(
        ({ el, r }) =>
          `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''} "${(el.textContent || el.getAttribute('aria-label') || '').trim().slice(0, 40)}" ${Math.round(r.width)}x${Math.round(r.height)}`
      );
    const headings = [...document.querySelectorAll('main h1, main h2, main h3')].map(
      h => `${h.tagName}:${h.textContent.trim().slice(0, 50)}`
    );
    return {
      overflow,
      small,
      headings,
      h1Count: document.querySelectorAll('h1').length,
      title: document.title,
    };
  });
  let axe = null;
  if (AXE) {
    await page.addScriptTag({ path: AXE });
    axe = await page.evaluate(async () => {
      const r = await window.axe.run(document.querySelector('main') ?? document, {
        runOnly: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'],
      });
      return r.violations.map(v => ({
        id: v.id,
        impact: v.impact,
        help: v.help,
        nodes: v.nodes.slice(0, 5).map(n => n.target.join(' ')),
      }));
    });
  }
  results.push({ label, url: page.url(), ...checks, axe });
}

async function focusWalk(label, steps = 12) {
  const seen = [];
  await page.mouse.click(1, 1);
  for (let i = 0; i < steps; i++) {
    await page.keyboard.press('Tab');
    seen.push(
      await page.evaluate(() => {
        const el = document.activeElement;
        if (!el || el === document.body) return 'body';
        const cs = getComputedStyle(el);
        const visible =
          (cs.outlineStyle !== 'none' && parseFloat(cs.outlineWidth) >= 2) ||
          cs.boxShadow !== 'none';
        return `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''} focusVisible=${visible}`;
      })
    );
  }
  results.push({ label: `${label} keyboard`, focusOrder: seen });
}

async function shoot(name) {
  for (const vp of VIEWPORTS) {
    await page.setViewportSize({ width: vp.width, height: vp.height });
    await page.waitForTimeout(250);
    await page.screenshot({ path: path.join(OUT, `${name}-${vp.name}.png`), fullPage: true });
    await measure(`${name} @${vp.name}`);
  }
  await page.setViewportSize({ width: 1280, height: 900 });
}

await register();

// Empty home
await page.goto(`${BASE}/app/career`);
await page.waitForSelector('h1');
await page.waitForTimeout(800);
await shoot('01-home-empty');

// Setup step 1: submit empty first to capture validation state
await page.goto(`${BASE}/app/career/setup`);
await page.waitForSelector('#profile-currentTitle');
await shoot('02-setup-step1');
await focusWalk('setup step 1');
await page.getByRole('button', { name: /save facts and continue/i }).click();
await page.waitForTimeout(400);
await shoot('03-setup-validation');
results.push({
  label: 'validation focus',
  focused: await page.evaluate(
    () => document.activeElement?.getAttribute('role') ?? document.activeElement?.tagName
  ),
});

await page.fill('#profile-currentTitle', 'Data analyst');
await page.fill('#profile-industry', 'Healthcare');
await page.fill('#profile-yearsExperience', '4');
await page.fill('#profile-location', 'Denver, CO');
await page.fill('#profile-summary', 'Builds reporting for clinical operations teams.');
for (const s of ['SQL', 'Tableau', 'Python']) {
  await page.fill('#profile-skills', s);
  await page.keyboard.press('Enter');
}
await page.fill('#profile-highlights', 'Built the weekly KPI report used by 12 clinics');
await page.keyboard.press('Enter');
await page.check('#profile-confirmed');
await page.getByRole('button', { name: /save facts and continue/i }).click();
await page.waitForSelector('#goal-targetRole');
await shoot('04-setup-step2');

await page.fill('#goal-targetRole', 'Senior data analyst');
await page.fill('#goal-targetLocation', 'Seattle, WA');
await page.fill('#goal-desiredPayMin', '95000');
await page.fill('#goal-desiredPayMax', '120000');
await page.fill('#goal-weeklyEffortHours', '5');
await page.check('#goal-confirmed');
await page.getByRole('button', { name: /save goal/i }).click();
await page.waitForURL(/\/app\/career$/);
await page.waitForTimeout(800);
await shoot('05-home-saved');

// Reload restores saved facts from the API
await page.reload();
await page.waitForTimeout(1200);
results.push({
  label: 'reload restores',
  text: (await page.locator('main').innerText()).slice(0, 400),
});

// Profile edit -> goal becomes stale
await page.goto(`${BASE}/app/career/profile`);
await page.waitForSelector('#profile-currentTitle');
await page.waitForTimeout(800);
await page.fill('#profile-currentTitle', 'Analyst II');
await page.check('#profile-confirmed');
await page.getByRole('button', { name: /save professional facts/i }).click();
await page.waitForTimeout(1000);
await page.reload();
await page.waitForSelector('#profile-currentTitle');
await page.waitForTimeout(1000);
await shoot('06-profile-edited-goal-stale');
await focusWalk('profile');

// Version history
const view = page.getByRole('button', { name: /view version 1/i });
if (await view.count()) {
  await view.click();
  await page.waitForTimeout(500);
}
await shoot('07-profile-version-history');

// Goal history: edit the goal, then view and restore version 1
await page.fill('#goal-targetRole', 'Analytics manager');
await page.check('#goal-confirmed');
await page.getByRole('button', { name: /save goal/i }).click();
await page.waitForTimeout(1000);
const viewGoal = page.getByRole('button', { name: /view goal version 1/i });
if (await viewGoal.count()) {
  await viewGoal.click();
  await page.waitForTimeout(500);
}
await shoot('08-goal-history');
const restoreGoal = page.getByRole('button', { name: /restore goal version 1/i });
if (await restoreGoal.count()) {
  await restoreGoal.click();
  await page.waitForTimeout(800);
  results.push({ label: 'goal restore status', text: await page.locator('[role=status]').innerText() });
}

fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify(results, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results and screenshots to ${OUT}`);
