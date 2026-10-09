import { existsSync } from 'node:fs';
import { test, expect, Page } from '@playwright/test';
import type { JobObservation, JobObservations } from '../src/app/services/career-profile.service';
import {
  AXE_PATH,
  MALICIOUS,
  observation,
  multi,
  unknownRemote,
  all,
  payload,
  mock,
  open,
  cards,
  type Options,
} from './fixtures/career-job-observations';

test.describe('desktop', () => {
  test.use({ viewport: { width: 1280, height: 900 } });

  test('intro names the source and says what the list is not', async ({ page }) => {
    await mock(page);
    await open(page);
    const intro = page.locator('[data-intro]');
    await expect(intro).toContainText('observed from one source, USAJOBS');
    await expect(intro).toContainText('not employment totals, not an outlook');
    await expect(intro).toContainText('not a count of all vacancies');
    await expect(intro).toContainText(
      'Job postings from USAJOBS (U.S. Office of Personnel Management).'
    );
    await expect(intro.getByRole('link', { name: /USAJOBS/ })).toHaveAttribute(
      'href',
      'https://www.usajobs.gov/'
    );
  });

  test('lists observations with pay basis, locations, remote status and a safe link', async ({
    page,
  }) => {
    await mock(page);
    await open(page);
    await expect(cards(page)).toHaveCount(3);
    await expect(cards(page).nth(0)).toContainText('$98,500 to $128,000 per year');
    await expect(cards(page).nth(0)).toContainText('28 September 2026');
    await expect(cards(page).nth(0)).toContainText('GS-12');
    await expect(cards(page).nth(1)).toContainText('$40.00 to $55.50 per hour');
    await expect(cards(page).nth(1).locator('[data-locations]')).toContainText(
      'Denver, CO (matches your area)'
    );
    await expect(cards(page).nth(1).locator('[data-locations]')).toContainText(
      'Colorado Springs, CO'
    );
    await expect(cards(page).nth(2)).toContainText('Pay not stated');
    await expect(cards(page).nth(2).locator('[data-remote]')).toHaveText('Remote not stated');
    await expect(cards(page).nth(0).locator('[data-remote]')).toHaveText(
      'Remote stated by the posting'
    );
    for (let i = 0; i < 3; i++) {
      await expect(cards(page).nth(i).locator('[data-remote]')).not.toContainText(/eligib/i);
    }
    const link = cards(page)
      .nth(0)
      .getByRole('link', { name: /View original posting/ });
    await expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    await expect(link).toHaveAttribute('target', '_blank');
  });

  test('the eligible-only filter drops the unknown-remote posting and counts it', async ({
    page,
  }) => {
    await mock(page);
    await open(page);
    await page.getByLabel('Only postings that state remote work').check();
    await expect(page).toHaveURL(/eligibleOnly=true/);
    await expect(cards(page)).toHaveCount(2);
    await expect(page.getByText('Analyst with no remote statement')).toHaveCount(0);
    const counts = page.locator('[data-counts]');
    await expect(counts).toContainText('1 hidden: remote not stated');
    await expect(counts).toContainText('4 hidden: not open where you are');
    await expect(counts).not.toContainText(/vacancy rate/i);
  });

  test('available-but-empty names the filters and offers a way forward', async ({ page }) => {
    await mock(page);
    await open(page, '&q=cobol');
    const empty = page.locator('[data-empty]');
    await expect(empty).toContainText('No open postings matched');
    await expect(empty).toContainText('search: "cobol"');
    await expect(empty).toContainText('widening the area');
  });

  test('not configured explains itself and links to the benchmark pages', async ({ page }) => {
    await mock(page, { reason: 'source_not_configured' });
    await open(page);
    const box = page.locator('[data-unavailable]');
    await expect(box).toContainText('The posting source is not configured yet.');
    await expect(box).toContainText('USAJOBS');
    await expect(box).toContainText('U.S. federal agencies only');
    await expect(box).toContainText('Job postings from USAJOBS');
    await expect(box.getByRole('link', { name: 'wage benchmark' })).toBeVisible();
    await expect(box.getByRole('link', { name: 'pay analysis' })).toBeVisible();
    await expect(box.getByRole('link', { name: 'market comparison' })).toBeVisible();
    await expect(cards(page)).toHaveCount(0);
  });

  test('source unreachable uses its own words', async ({ page }) => {
    await mock(page, { reason: 'source_unavailable' });
    await open(page);
    await expect(page.locator('[data-reason]')).toHaveText(
      'The posting source could not be reached. The benchmark pages are unaffected.'
    );
  });

  test('a stale preference names the saved location and the current filter', async ({ page }) => {
    await mock(page, { stale: true });
    await open(page, '&area=Denver');
    await expect(page.locator('[data-stale]')).toHaveText(
      'Your saved location is Austin, TX; this filter is Denver.'
    );
  });

  test('filters persist across reload and clear', async ({ page }) => {
    const requests = await mock(page);
    await open(page);
    await page.getByLabel('Area').fill('Boulder');
    await page.getByLabel('Search title or organization').fill('developer');
    await page.getByLabel('Remote work', { exact: true }).selectOption('eligible');
    await page.getByLabel('Only postings that state remote work').check();
    await expect(page).toHaveURL(/area=Boulder/);
    await expect(page).toHaveURL(/q=developer/);
    await expect(page).toHaveURL(/remote=eligible/);
    await expect(page).toHaveURL(/eligibleOnly=true/);
    await page.reload();
    await expect(page.getByLabel('Area')).toHaveValue('Boulder');
    await expect(page.getByLabel('Search title or organization')).toHaveValue('developer');
    await expect(page.getByLabel('Remote work', { exact: true })).toHaveValue('eligible');
    await expect(page.getByLabel('Only postings that state remote work')).toBeChecked();
    expect(requests.at(-1)).toContain('area=Boulder');
    await page.getByRole('button', { name: 'Clear' }).click();
    await expect(page).not.toHaveURL(/area=|q=|remote=|eligibleOnly=/);
    await expect(page.getByLabel('Area')).toHaveValue('');
  });

  test('untrusted listing text renders as literal text', async ({ page }) => {
    await mock(page, { titles: [MALICIOUS] });
    await open(page);
    await expect(page.getByText(MALICIOUS, { exact: true }).first()).toBeVisible();
    await expect(page.locator('main img')).toHaveCount(0);
  });
});

test.describe('mobile', () => {
  test.use({ viewport: { width: 320, height: 800 } });
  test('does not overflow horizontally at 320px', async ({ page }) => {
    await mock(page);
    await open(page);
    await expect(cards(page)).toHaveCount(3);
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth
      )
    ).toBe(false);
  });
});

test.describe('accessibility', () => {
  test.skip(!existsSync(AXE_PATH), `axe-core not found at ${AXE_PATH}; set AXE_PATH`);
  for (const width of [1280, 390, 320]) {
    test(`0 axe WCAG 2.2 AA violations at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      const run = async () => {
        if (!(await page.evaluate(() => 'axe' in window)))
          await page.addScriptTag({ path: AXE_PATH });
        return page.evaluate(async () => {
          const result = await (window as any).axe.run(document, {
            runOnly: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'],
          });
          return result.violations.map(
            (v: any) => `${v.id}: ${v.nodes.map((n: any) => n.target.join(' ')).join(' | ')}`
          );
        });
      };
      await mock(page);
      await open(page);
      await expect(cards(page)).toHaveCount(3);
      expect(await run()).toEqual([]);
      await page.getByLabel('Only postings that state remote work').check();
      await expect(cards(page)).toHaveCount(2);
      expect(await run()).toEqual([]);
      await page.unroute('**/api/**');
      await mock(page, { reason: 'source_not_configured' });
      await open(page);
      await expect(page.locator('[data-unavailable]')).toBeVisible();
      expect(await run()).toEqual([]);
    });
  }
});

test('occupation_required shows the confirm-occupation link, not a dead end', async ({ page }) => {
  await mock(page, { reason: 'occupation_required' });
  await open(page);
  const box = page.locator('[data-occupation-required]');
  await expect(box).toBeVisible();
  await expect(box.getByRole('link', { name: 'Confirm your occupation' })).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
});

test('without a confirmed occupation, postings are searched by the target role and say so', async ({
  page,
}) => {
  await mock(page, { byTargetRole: 'Senior data analyst' });
  await open(page);
  await expect(page.locator('[data-observation]').first()).toBeVisible();
  const note = page.locator('[data-search-basis="target_role"]');
  await expect(note).toContainText('Senior data analyst');
  await expect(note.getByRole('link', { name: 'Confirm your occupation' })).toHaveAttribute(
    'href',
    '/app/career/occupation'
  );
});

test('a widened search says what was widened', async ({ page }) => {
  await mock(page, { broadened: { keyword: 'data analyst', areaTitle: 'Colorado' } });
  await open(page);
  await expect(page.locator('[data-observation]').first()).toBeVisible();
  const note = page.locator('[data-search-broadened]');
  await expect(note).toContainText('“data analyst”');
  await expect(note).toContainText('Colorado');
});

test('an unwidened search shows no widening note', async ({ page }) => {
  await mock(page);
  await open(page);
  await expect(page.locator('[data-observation]').first()).toBeVisible();
  await expect(page.locator('[data-search-broadened]')).toHaveCount(0);
});
