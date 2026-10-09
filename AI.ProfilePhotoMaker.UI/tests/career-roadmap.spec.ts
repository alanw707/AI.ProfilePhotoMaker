import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import type { RoadmapDto, RoadmapOption } from '../src/app/services/career-profile.service';
import {
  AXE_PATH,
  createdAt,
  labels,
  option,
  three,
  one,
  mock,
  open,
  overflow,
  type Opts,
} from './fixtures/career-roadmap';

test('run with real steps leads to a reload-safe roadmap with three options', async ({ page }) => {
  const calls = await mock(page);
  await open(page);
  await expect(page).toHaveTitle('Career Roadmap - AI Profile Photo Maker');
  await page.getByRole('button', { name: 'Build my roadmap' }).click();
  expect(calls.start).toEqual({ task: 'roadmap' });
  await expect(page).toHaveURL(/run=run-1/);
  await expect(page.getByText(labels[0])).toBeVisible({ timeout: 10000 });
  await expect(page).toHaveURL(/roadmap=rm-1/, { timeout: 15000 });
  await expect(page.locator('[data-option]')).toHaveCount(3);
  const first = page.locator('[data-option="closest_fit"]');
  await expect(first).toContainText('Median annual wage is $135,980.');
  await expect(first).toContainText(
    'Source: BLS Occupational Employment and Wage Statistics, May 2025'
  );
  await expect(first.locator('[data-assumptions]')).toContainText(
    'You can spend your weekly hours'
  );
  await expect(first.locator('[data-missing]')).toContainText('No advertised pay');
  await expect(first.locator('[data-scenario]')).toHaveText('A scenario, not a promise.');
  await expect(first.getByRole('heading', { name: 'This week' })).toBeVisible();
  await expect(first.getByRole('heading', { name: 'By day 30' })).toBeVisible();
  await expect(first.getByRole('heading', { name: 'By day 90' })).toBeVisible();
  await expect(first.locator('[data-task="t2"]')).toContainText('3 hours');
  await expect(first.locator('[data-task="t2"]')).toContainText(
    'Do this after: List three projects you could show.'
  );
  await expect(page.getByRole('group', { name: 'Choose a path' })).toBeVisible();
  await expect(page.getByText('Roadmaps are kept with your career data')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Recent roadmaps' })).toBeVisible();
  await page.reload();
  await expect(page.locator('[data-option]')).toHaveCount(3);
});

test('one option explains what was left out; low-time note shows', async ({ page }) => {
  await mock(page, { roadmap: one, lowTime: true });
  await open(page, '&roadmap=rm-1');
  await expect(page.locator('[data-option]')).toHaveCount(1);
  await expect(page.locator('[data-omitted]')).toContainText(
    'No other path is supported by the evidence yet.'
  );
  await expect(page.locator('[data-omitted]')).toContainText('Higher ambition');
  await expect(page.locator('[data-low-time]')).toContainText('under 2 hours a week');
});

test('accept needs a choice and a confirmation, then says the goal is unchanged', async ({
  page,
}) => {
  const calls = await mock(page);
  await open(page, '&roadmap=rm-1');
  await page.getByRole('button', { name: 'Accept this path' }).click();
  await expect(page.locator('[data-error]')).toContainText('Choose a path first.');
  expect(calls.accept).toHaveLength(0);
  await page.getByRole('radio', { name: /Data Scientists/ }).check();
  await page.getByRole('button', { name: 'Accept this path' }).click();
  await expect(page.getByRole('dialog')).toContainText('does not change your goal');
  expect(calls.accept).toHaveLength(0);
  await page.getByRole('button', { name: 'Cancel' }).click();
  expect(calls.accept).toHaveLength(0);
  await page.getByRole('button', { name: 'Accept this path' }).click();
  await page.getByRole('button', { name: 'Yes, accept this path' }).click();
  await expect(page.locator('[data-accepted]')).toContainText('Your goal itself was not changed.');
  await expect(page.getByRole('link', { name: 'Go to the occupation page' })).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
  expect(calls.accept).toHaveLength(1);
  expect(calls.accept[0].body).toEqual({ optionKey: 'higher_ambition' });
  expect(calls.accept[0].ifMatch).toMatch(/^"goal-v\d+"$/);
});

test('a changed goal (412) refetches it and asks to retry', async ({ page }) => {
  const calls = await mock(page, { acceptStatus: 412 });
  await open(page, '&roadmap=rm-1');
  await page.getByRole('radio', { name: /Software Developers/ }).check();
  await page.getByRole('button', { name: 'Accept this path' }).click();
  await page.getByRole('button', { name: 'Yes, accept this path' }).click();
  await expect(page.locator('[data-error]')).toContainText('Choose Accept this path to try again.');
  expect(calls.goals).toBeGreaterThanOrEqual(3);
  await expect(page.getByRole('button', { name: 'Accept this path' })).toBeVisible();
});

test('effort edits save through the endpoint and cycles are explained', async ({ page }) => {
  const calls = await mock(page);
  await open(page, '&roadmap=rm-1');
  const input = page
    .locator('[data-option="closest_fit"]')
    .getByLabel('Hours for List three projects you could show');
  await input.fill('4');
  await input.press('Enter');
  await expect(page.locator('[data-effort-saved]')).toContainText('Saved 4 hours');
  expect(calls.put).toEqual([{ effortHours: 4 }]);
  await input.fill('50');
  await input.press('Enter');
  await expect(page.getByText('Enter hours between 0.5 and 40.').first()).toBeVisible();
  expect(calls.put).toHaveLength(1);

  const cyc = await page.context().newPage();
  await mock(cyc, { cycle: true });
  await open(cyc, '&roadmap=rm-1');
  const field = cyc
    .locator('[data-option="closest_fit"]')
    .getByLabel('Hours for List three projects you could show');
  await field.fill('3');
  await field.press('Enter');
  await expect(cyc.getByText('depend on each other in a loop')).toBeVisible();
  await expect(cyc.locator('body')).not.toContainText('CareerRoadmapCycle');
});

test('stale banner, keyboard selection and no machine codes', async ({ page }) => {
  await mock(page, { stale: true });
  await open(page, '&roadmap=rm-1');
  await expect(page.locator('[data-stale]')).toContainText(
    'Your profile, goal or market evidence changed after this roadmap.'
  );
  await page.getByRole('radio', { name: /Software Developers/ }).focus();
  await page.keyboard.press('Space');
  await expect(page.getByRole('radio', { name: /Software Developers/ })).toBeChecked();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByRole('radio', { name: /Data Scientists/ })).toBeChecked();
  const text = (await page.locator('[data-roadmap]').innerText()).replace(/\s+/g, ' ');
  for (const code of [
    'closest_fit',
    'higher_ambition',
    'steadier_transition',
    'no_supported_alternative',
    'oews',
    '15-1252.00',
    '2026-10-05T',
    'proposed',
  ])
    expect(text).not.toContain(code);
  await expect(
    page.getByRole('button', { name: /Oct 5, 2026|October 5, 2026|2026-10-05/ })
  ).toHaveCount(0);
});

test('home links to the roadmap', async ({ page }) => {
  await mock(page);
  await page.goto('/app/career?e2eAuthBypass=1');
  await expect(
    page.getByRole('navigation', { name: 'Career steps' }).getByRole('link', { name: /Roadmap/ })
  ).toHaveAttribute('href', '/app/career/roadmap');
});

test.describe('accessibility', () => {
  test.skip(!existsSync(AXE_PATH), `axe-core not found at ${AXE_PATH}; set AXE_PATH`);
  for (const width of [1280, 390, 320]) {
    test(`0 axe WCAG 2.2 AA violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mock(page, { stale: true, lowTime: true });
      await open(page, '&roadmap=rm-1');
      await expect(page.locator('[data-option]')).toHaveCount(3);
      await page.addScriptTag({ path: AXE_PATH });
      const run = () =>
        page.evaluate(async () => {
          const result = await (window as any).axe.run(document, {
            runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
          });
          return result.violations.map(
            (v: any) => `${v.id}: ${v.nodes.map((n: any) => n.target.join(' ')).join(' | ')}`
          );
        });
      expect(await run()).toEqual([]);
      await page.getByRole('radio', { name: /Software Developers/ }).check();
      await page.getByRole('button', { name: 'Accept this path' }).click();
      await expect(page.getByRole('dialog')).toBeVisible();
      expect(await run()).toEqual([]);
      await page.getByRole('button', { name: 'Cancel' }).click();
      if (width === 320) expect(await overflow(page)).toBeLessThanOrEqual(0);
    });
  }
});
