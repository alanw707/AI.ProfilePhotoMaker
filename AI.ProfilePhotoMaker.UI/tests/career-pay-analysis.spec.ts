import { test, expect, Page } from '@playwright/test';
import { formatFigure } from '../src/app/pages/career/market-format';
import type {
  MarketFigure,
  PayAnalysisDto,
  PayBenchmarkSection,
} from '../src/app/services/career-profile.service';
import {
  figure,
  benchmark,
  gates,
  analysis,
  labels,
  insufficient,
  mock,
  open,
} from './fixtures/career-pay-analysis';

test('start, real steps, saved figures, gates and scenario', async ({ page }) => {
  const started = await mock(page);
  await open(page);
  await page.getByRole('button', { name: 'Build my pay analysis' }).click();
  expect(started()).toEqual({ task: 'pay_analysis' });
  await expect(page).toHaveURL(/run=run-1/);
  await expect(page.getByText(labels[0])).toBeVisible({ timeout: 10000 });
  await expect(page).toHaveURL(/analysis=pay-1/, { timeout: 15000 });
  for (const f of benchmark.figures)
    await expect(page.locator(`[data-figure="benchmark:${f.key}:${f.areaCode}"]`)).toHaveText(
      formatFigure(f)
    );
  const personalized = page.locator('[data-section="personalized"]');
  await expect(personalized).toContainText(
    'No qualified source: no provider has granted written rights for ongoing commercial use. Nothing here is advertised pay.'
  );
  await expect(personalized).not.toContainText('$');
  for (const gate of gates) await expect(personalized).toContainText(gate.gateId);
  await expect(page.locator('[data-section="scenario"]')).toContainText('$14,020 (10.3%)');
  await expect(page.locator('[data-section="scenario"]')).toContainText('preference, not evidence');
  await expect(page.getByText('Recent analyses')).toBeVisible();
});
test('no machine code leaks to the page', async ({ page }) => {
  await mock(page, { insufficient: true });
  await open(page, '&analysis=pay-1');
  const body = page.locator('[data-analysis]');
  await expect(body).toContainText('42.0% of observations');
  const text = (await body.innerText()).replace(/\s+/g, ' ');
  for (const code of [
    'provider_rights_unverified',
    'insufficient_observations',
    'insufficient_employers',
    'source_unavailable',
    'location_unresolved',
    'not_published',
    'work-location',
    'duplicate requisition',
    'different geography',
    'Unverified',
    'Passed',
  ])
    expect(text).not.toContain(code);
  expect(text).not.toContain('0.42');
  expect(text).not.toContain('USD /');
  const personalized = page.locator('[data-section="personalized"]');
  await expect(personalized).toContainText('2 excluded for a duplicate posting');
  await expect(personalized).toContainText('3 excluded for a different location');
  await expect(personalized).toContainText('1 excluded for not open where you are');
  await expect(personalized).toContainText('Not enough independent current observations yet.');
  await expect(personalized).toContainText(
    'One employer supplies more than 40% of these observations.'
  );
  await expect(personalized).toContainText(
    'The range moves by more than 10% when the largest employer is removed.'
  );
  await expect(page.locator('[data-gates] li')).toHaveCount(8);
  await expect(page.locator('[data-gates]')).toContainText('Not yet verified');
  await expect(page.locator('[data-gates]')).not.toContainText('Failed');
  const scenario = page.locator('[data-section="scenario"]');
  await expect(scenario).toContainText('Benchmark median (Denver-Aurora-Centennial, CO)');
  await expect(scenario).toContainText("your goal's minimum desired pay");
});
test('recompute reports agreement and divergence without altering stored figures', async ({
  page,
}) => {
  await mock(page);
  await open(page, '&analysis=pay-1');
  await page.getByRole('button', { name: 'Recompute' }).click();
  await expect(page.locator('[data-recompute]')).toContainText(
    'Reproduced exactly (input hash abcdef012345)'
  );
  await expect(page.locator('[data-figure="benchmark:medianAnnual:99"]')).toHaveText('$135,980');
});
test('divergence, stale, source drawer and 320px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 800 });
  await mock(page, { stale: true, divergent: true });
  await open(page, '&analysis=pay-1');
  await expect(page.locator('[data-stale]')).toContainText(
    'Your profile or goal changed after this analysis. It is kept as it was; build a new one for current inputs.'
  );
  await page.getByRole('button', { name: 'Recompute' }).click();
  await expect(page.locator('[data-recompute]')).toContainText('disagree: benchmark.medianAnnual');
  await expect(page.locator('[data-figure="benchmark:medianAnnual:99"]')).toHaveText('$135,980');
  await page.getByRole('button', { name: 'Sources', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('BLS OEWS 2025');
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toBeHidden();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth
    )
  ).toBeLessThanOrEqual(0);
  await expect(page.locator('[data-section="personalized"]')).not.toContainText('%');
});
test('direct reload, missing occupation and home link', async ({ page }) => {
  await mock(page);
  await open(page, '&analysis=pay-1');
  await page.reload();
  await expect(page.locator('[data-figure="benchmark:medianAnnual:99"]')).toHaveText('$135,980');
  await page.goto('/app/career?e2eAuthBypass=1');
  await expect(page.getByRole('link', { name: 'Pay analysis' })).toHaveAttribute(
    'href',
    '/app/career/pay'
  );
});
test('missing occupation links to confirmation', async ({ page }) => {
  await mock(page, { missing: true });
  await open(page);
  await expect(
    page.getByText('This page uses your confirmed occupation.', { exact: false })
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Confirm your occupation' })).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
});
