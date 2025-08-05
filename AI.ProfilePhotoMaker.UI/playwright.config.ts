import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright Configuration for AI Profile Photo Maker
 * Environment: Staging Validation Tests
 */
export default defineConfig({
  testDir: './e2e/staging',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ['html', { outputFolder: 'playwright-report' }],
    ['json', { outputFile: 'test-results.json' }],
    ['junit', { outputFile: 'test-results.xml' }],
    ['line']
  ],
  use: {
    baseURL: 'https://aiprofilemaker-web-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    headless: true
  },

  timeout: 30000,
  expect: {
    timeout: 10000
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
    {
      name: 'mobile-chrome',
      use: { ...devices['Pixel 5'] },
    },
    {
      name: 'mobile-safari',
      use: { ...devices['iPhone 12'] },
    },
  ],

  // Global setup and teardown
  globalSetup: require.resolve('./e2e/staging/global-setup.ts'),
  globalTeardown: require.resolve('./e2e/staging/global-teardown.ts'),
});