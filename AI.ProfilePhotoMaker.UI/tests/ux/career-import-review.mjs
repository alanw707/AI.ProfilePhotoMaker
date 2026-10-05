// Live-stack UX review for #379 resume import (no API mocks).
//   scripts/career-localdev-api.sh start; npm run dev:local
//   AXE_PATH=<axe.min.js> node tests/ux/career-import-review.mjs [outDir]
// Builds a synthetic, fictional PDF in memory. Simulated review, not user evidence.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const BASE = process.env.BASE_URL ?? 'http://localhost:4200';
const OUT = path.resolve(process.argv[2] ?? '../docs/testing/career/379');
const AXE = process.env.AXE_PATH;
fs.mkdirSync(OUT, { recursive: true });

function pdf(pages) {
  const esc = s => s.replace(/[\\()]/g, m => '\\' + m);
  const objs = ['<< /Type /Catalog /Pages 2 0 R >>',
    `<< /Type /Pages /Kids [${pages.map((_, i) => `${4 + 2 * i} 0 R`).join(' ')}] /Count ${pages.length} >>`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>'];
  pages.forEach((lines, i) => {
    objs.push(`<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R >> >> /Contents ${5 + 2 * i} 0 R >>`);
    const body = 'BT /F1 11 Tf 50 750 Td 14 TL ' + lines.map(l => `(${esc(l)}) Tj T*`).join(' ') + ' ET';
    objs.push(`<< /Length ${body.length} >>\nstream\n${body}\nendstream`);
  });
  let out = '%PDF-1.4\n'; const offs = [];
  objs.forEach((o, i) => { offs.push(out.length); out += `${i + 1} 0 obj\n${o}\nendobj\n`; });
  const x = out.length;
  out += `xref\n0 ${objs.length + 1}\n0000000000 65535 f \n` + offs.map(o => `${String(o).padStart(10, '0')} 00000 n \n`).join('');
  out += `trailer\n<< /Size ${objs.length + 1} /Root 1 0 R >>\nstartxref\n${x}\n%%EOF\n`;
  return Buffer.from(out, 'latin1');
}
const resume = pdf([[
  'Morgan Ellis', 'Senior Operations Lead', 'Denver, CO', 'Summary',
  'Operations leader focused on clinic scheduling and process improvement.', 'Experience',
  'Senior Operations Lead, Regional Health Services, 2021 - 2023',
  '- Reduced scheduling backlog by 30 percent across 12 clinics',
  '- Helped with vendor onboarding for new sites',
  'Operations Analyst, Northwind Clinics, 2016 - 2020',
  '- Built a weekly KPI report used by regional directors'], ['Skills', 'SQL, Tableau, Process Improvement']]);
const scan = pdf([[]]);

const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined });
const page = await (await browser.newContext()).newPage();
const errors = []; page.on('pageerror', e => errors.push(e.message));
const results = [];

async function measure(label) {
  const checks = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    small: [...document.querySelectorAll('main a, main button, main input:not([type=checkbox]):not([type=radio]), main select, main textarea')]
      .filter(e => e.offsetParent).map(e => [e, e.getBoundingClientRect()]).filter(([, r]) => r.height < 44)
      .map(([e, r]) => `${e.tagName} "${(e.textContent || e.getAttribute('aria-label') || '').trim().slice(0, 30)}" ${Math.round(r.height)}`),
    h1: document.querySelectorAll('h1').length, title: document.title,
  }));
  let axe = [];
  if (AXE) {
    await page.addScriptTag({ path: AXE });
    axe = await page.evaluate(async () => (await window.axe.run(document.querySelector('main') ?? document,
      { runOnly: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'] })).violations.map(v => `${v.id}: ${v.nodes.slice(0, 3).map(n => n.target.join(' ')).join(' | ')}`));
  }
  results.push({ label, ...checks, axe });
}
async function shoot(name) {
  for (const [vp, w, h] of [['desktop', 1280, 900], ['mobile-390', 390, 844], ['mobile-320', 320, 720]]) {
    await page.setViewportSize({ width: w, height: h }); await page.waitForTimeout(250);
    await page.screenshot({ path: path.join(OUT, `${name}-${vp}.png`), fullPage: true });
    await measure(`${name} @${vp}`);
  }
  await page.setViewportSize({ width: 1280, height: 900 });
}

const email = `ux-import-${Date.now()}@example.com`, pw = 'Passw0rd!Career';
const reg = await page.request.post(`${BASE}/api/auth/register`, { data: { email, password: pw, confirmPassword: pw,
  firstName: 'Riley', lastName: 'Synthetic', gender: 'Prefer not to say', ethnicity: 'Prefer not to say',
  ageConfirmed: true, acceptTerms: true, turnstileToken: 'local' } });
if (!reg.ok()) throw new Error('register ' + reg.status());
await page.request.post(`${BASE}/api/auth/dev/confirm-email`);
await page.request.put(`${BASE}/api/career/profile`, { data: { currentTitle: 'Operations lead', skills: ['SQL'], highlights: [], confirmed: true } });
await page.goto(`${BASE}/`);
const reject = page.getByRole('button', { name: /reject non-essential/i }); if (await reject.count()) await reject.first().click();

await page.goto(`${BASE}/app/career/import`); await page.waitForSelector('h1'); await page.waitForTimeout(600);
await shoot('01-import-empty');
await page.getByRole('button', { name: 'Upload and read resume' }).click(); await page.waitForTimeout(300);
await shoot('02-consent-error');
await page.getByLabel('I understand and agree').check();
await page.getByLabel(/Resume file/).setInputFiles({ name: 'morgan-ellis.pdf', mimeType: 'application/pdf', buffer: resume });
await page.getByRole('button', { name: 'Upload and read resume' }).click();
await page.getByRole('heading', { name: 'Review suggestions' }).waitFor({ timeout: 20000 });
results.push({ label: 'focus after processing', focused: await page.evaluate(() => document.activeElement?.textContent?.trim().slice(0, 40)) });
await shoot('03-review');
const boxes = page.locator('main input[type=checkbox]:not(:checked)');
results.push({ label: 'suggestions', count: await boxes.count(), checkedByDefault: await page.locator('main input[type=checkbox]:checked').count() - 1 });
await page.getByLabel('Senior Operations Lead').first().check();
const sel = page.getByRole('button', { name: /Accept selected/ });
await sel.click();
await page.waitForURL(/career\/profile/, { timeout: 15000 }); await page.waitForTimeout(800);
results.push({ label: 'after accept', text: (await page.locator('main').innerText()).slice(0, 300) });
await shoot('04-profile-after-accept');

await page.goto(`${BASE}/app/career/import`); await page.waitForSelector('h1');
await page.getByLabel('I understand and agree').check();
await page.getByLabel(/Resume file/).setInputFiles({ name: 'scan.pdf', mimeType: 'application/pdf', buffer: scan });
await page.getByRole('button', { name: 'Upload and read resume' }).click();
await page.getByRole('heading', { name: 'We could not read this file' }).waitFor({ timeout: 20000 });
await shoot('05-unreadable-paste-fallback');

fs.writeFileSync(path.join(OUT, 'checks.json'), JSON.stringify({ results, errors }, null, 2));
await browser.close();
console.log(`Wrote ${results.length} results to ${OUT}; page errors: ${errors.length}`);
