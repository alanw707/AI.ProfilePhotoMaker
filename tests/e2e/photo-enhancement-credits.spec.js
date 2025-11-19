const { test, expect } = require('@playwright/test');
const path = require('path');
const fs = require('fs');

const creditTestEmail = process.env.CREDIT_TEST_EMAIL || process.env.E2E_TEST_EMAIL;
const creditTestPassword =
  process.env.CREDIT_TEST_PASSWORD || process.env.E2E_TEST_PASSWORD || process.env.CREDIT_TEST_PASS;

const selectors = {
  loginEmail: '#email',
  loginPassword: '#password',
  loginSubmit: 'button[type="submit"]',
  creditsValue: '.credits-section .credits-card h3',
  uploadInput: 'input[type="file"]',
  enhanceButton: '.enhance-btn',
  processingSection: '.processing-section',
  resultsSection: '.results-section',
};

const testImagePath = path.join(__dirname, 'test-images', 'sample-selfie.jpg');

test.describe('Photo Enhancement Credits', () => {
  test.skip(
    !creditTestEmail || !creditTestPassword,
    'Set CREDIT_TEST_EMAIL and CREDIT_TEST_PASSWORD env vars to run this suite.'
  );

  test.beforeAll(() => {
    if (!fs.existsSync(testImagePath)) {
      throw new Error(`Test image missing at ${testImagePath}.`);
    }
  });

  test('deducts two credits after a successful OpenAI enhancement', async ({ page }) => {
    await page.context().clearCookies();

    await page.goto('/auth/login');
    await page.fill(selectors.loginEmail, creditTestEmail);
    await page.fill(selectors.loginPassword, creditTestPassword);
    await Promise.all([
      page.waitForURL('**/app/**', { timeout: 45000 }),
      page.click(selectors.loginSubmit),
    ]);

    await page.goto('/app/enhance');
    const startingCredits = await waitForCreditsToLoad(page);
    expect(startingCredits).toBeGreaterThanOrEqual(2);

    await uploadImageForEnhancement(page);
    await page.click(selectors.enhanceButton);

    await page.waitForSelector(selectors.processingSection, { timeout: 15000 });
    await page.waitForSelector(selectors.resultsSection, { timeout: 240000 });

    const refreshedCredits = await waitForCreditsDecrease(page, startingCredits);
    await expect(refreshedCredits).toBe(startingCredits - 2);

    const apiCredits = await fetchCreditsFromApi(page);
    expect(apiCredits.totalCredits).toBe(refreshedCredits);
  });
});

async function waitForCreditsToLoad(page) {
  const locator = page.locator(selectors.creditsValue).first();
  await locator.waitFor({ timeout: 20000 });
  return extractCredits(await locator.textContent());
}

async function waitForCreditsDecrease(page, previousCredits) {
  await page.waitForFunction(
    ({ selector, previous }) => {
      const el = document.querySelector(selector);
      if (!el) {
        return false;
      }
      const match = el.textContent?.match(/(\d+)/);
      if (!match) {
        return false;
      }
      const current = parseInt(match[1], 10);
      return current < previous;
    },
    { selector: selectors.creditsValue, previous: previousCredits },
    { timeout: 120000 }
  );

  return waitForCreditsToLoad(page);
}

async function uploadImageForEnhancement(page) {
  await page.waitForSelector(selectors.uploadInput, { timeout: 15000 });
  await page.setInputFiles(selectors.uploadInput, testImagePath);
  await expect(page.locator('.file-preview')).toBeVisible({ timeout: 15000 });
}

function extractCredits(text) {
  if (!text) {
    throw new Error('Credits element did not contain text');
  }
  const match = text.match(/(\d+)/);
  if (!match) {
    throw new Error(`Unable to parse credits from "${text}"`);
  }
  return parseInt(match[1], 10);
}

async function fetchCreditsFromApi(page) {
  const response = await page.request.get('/api/credit/status');
  expect(response.ok()).toBeTruthy();
  const body = await response.json();
  expect(body.success).toBeTruthy();
  return body.data;
}
