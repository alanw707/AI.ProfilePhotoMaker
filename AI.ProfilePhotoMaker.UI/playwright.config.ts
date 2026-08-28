import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: 'tests',
  testMatch: ['**/*.spec.ts'],
  fullyParallel: false,
  retries: 0,
  use: {
    headless: true,
    baseURL: 'http://localhost:4200',
    trace: 'off',
  },
  webServer: {
    command: 'npm run dev:local',
    url: 'http://localhost:4200',
    reuseExistingServer: true,
    timeout: 120_000,
  },
  // Keep default single project unless overridden
});

