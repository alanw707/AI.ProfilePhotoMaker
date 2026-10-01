import { expect, test, type Browser, type Page } from '@playwright/test';

// Real-stack browser check (no API mocks) for #378's two-user isolation. Needs
// scripts/career-localdev-api.sh start and npm run dev:local; skipped otherwise.
test.skip(!process.env['CAREER_E2E_LIVE'], 'set CAREER_E2E_LIVE=1 with the LocalDev API running');

async function signedInPage(browser: Browser, name: string): Promise<Page> {
  const context = await browser.newContext();
  const page = await context.newPage();
  const email = `${name}-${Date.now()}@example.com`;
  const password = 'Passw0rd!Career';
  const res = await page.request.post('/api/auth/register', {
    data: {
      email,
      password,
      confirmPassword: password,
      firstName: name,
      lastName: 'Synthetic',
      gender: 'Prefer not to say',
      ethnicity: 'Prefer not to say',
      ageConfirmed: true,
      acceptTerms: true,
      turnstileToken: 'local',
    },
  });
  expect(res.ok()).toBeTruthy();
  expect((await page.request.post('/api/auth/dev/confirm-email')).ok()).toBeTruthy();
  await page.goto('/');
  const reject = page.getByRole('button', { name: /reject non-essential/i });
  if (await reject.count()) await reject.first().click();
  return page;
}

test('one user never sees another user’s career facts, even after reload', async ({ browser }) => {
  const alice = await signedInPage(browser, 'alice');
  const bob = await signedInPage(browser, 'bob');

  await alice.goto('/app/career/setup');
  await alice.getByLabel('Current title').fill('Alice private title');
  await alice.getByLabel('I confirm these facts are accurate').first().check();
  await alice.getByRole('button', { name: /save facts and continue/i }).click();
  await alice.getByLabel('Target role').fill('Alice private goal');
  await alice.getByLabel('I confirm these facts are accurate').last().check();
  await alice.getByRole('button', { name: /save goal/i }).click();
  await expect(alice).toHaveURL(/\/app\/career$/);
  await alice.reload();
  await expect(alice.getByText('Alice private title')).toBeVisible();

  await bob.goto('/app/career');
  await expect(bob.getByRole('link', { name: /set up your profile and goal/i })).toBeVisible();
  await expect(bob.getByText('Alice private title')).toHaveCount(0);
  await expect(bob.getByText('Alice private goal')).toHaveCount(0);
  await bob.goto('/app/career/profile');
  await expect(bob.getByLabel('Current title')).toHaveValue('');
  await expect(bob.getByText('Alice private title')).toHaveCount(0);
});
