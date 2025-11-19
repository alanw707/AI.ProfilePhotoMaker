const { defineConfig, devices } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './',
  timeout: 60000,
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: '../test-results/html-report-local' }]],
  use: {
    baseURL: process.env.TEST_BASE_URL || 'http://localhost:4200',
    headless: true,
    viewport: { width: 1280, height: 720 },
    actionTimeout: 10000,
    navigationTimeout: 30000,
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'local-chrome',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: process.env.TEST_BASE_URL || 'http://localhost:4200',
      },
      testMatch: ['**/*.spec.js', '**/*.spec.ts'],
    },
  ],
  globalSetup: require.resolve('./setup/global-setup.js'),
  globalTeardown: require.resolve('./setup/global-teardown.js'),
  outputDir: '../test-results/artifacts-local',
});
