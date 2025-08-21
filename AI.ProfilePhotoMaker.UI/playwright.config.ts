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
  // Keep default single project unless overridden
});

