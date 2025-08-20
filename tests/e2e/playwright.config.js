// @ts-check
const { defineConfig, devices } = require('@playwright/test');

/**
 * Playwright Configuration for AI Profile Photo Maker E2E Tests
 * 
 * Configured for comprehensive testing across multiple environments
 * with specific focus on storage service validation.
 */
module.exports = defineConfig({
  // Test directory
  testDir: './tests/e2e',
  
  // Global test timeout
  timeout: 60000,
  
  // Test execution configuration
  fullyParallel: false, // Run tests sequentially for better debugging
  forbidOnly: !!process.env.CI, // Fail if test.only() is left in CI
  retries: process.env.CI ? 2 : 1, // Retry failed tests in CI
  workers: process.env.CI ? 1 : 2, // Limit parallelism for stability
  
  // Reporter configuration
  reporter: [
    ['html', { outputFolder: 'test-results/html-report' }],
    ['json', { outputFile: 'test-results/test-results.json' }],
    ['list'],
    ...(process.env.CI ? [['github']] : [])
  ],
  
  // Global test configuration
  use: {
    // Base URL for tests
    baseURL: process.env.TEST_BASE_URL || 'https://app.aiprofilephotomaker.com',
    
    // Browser configuration
    headless: process.env.CI ? true : false,
    viewport: { width: 1280, height: 720 },
    ignoreHTTPSErrors: false,
    
    // Action timeouts
    actionTimeout: 10000,
    navigationTimeout: 30000,
    
    // Test artifacts
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    trace: 'retain-on-failure',
    
    // Network configuration
    bypassCSP: false,
    
    // User agent
    userAgent: 'Playwright-E2E-Tests/1.0 (+https://github.com/microsoft/playwright)',
  },

  // Test projects for different environments and scenarios
  projects: [
    {
      name: 'production-validation',
      use: { 
        ...devices['Desktop Chrome'],
        baseURL: 'https://app.aiprofilephotomaker.com'
      },
      testMatch: '**/image-upload-validation.spec.js',
      timeout: 90000, // Longer timeout for production tests
    },
    
    {
      name: 'staging-validation',
      use: { 
        ...devices['Desktop Chrome'],
        baseURL: process.env.STAGING_URL || 'https://staging-app.aiprofilephotomaker.com'
      },
      testMatch: '**/image-upload-validation.spec.js',
      timeout: 60000,
    },
    
    {
      name: 'cross-browser-validation',
      use: { 
        ...devices['Desktop Firefox'],
        baseURL: process.env.TEST_BASE_URL || 'https://app.aiprofilephotomaker.com'
      },
      testMatch: '**/image-upload-validation.spec.js',
      dependencies: ['production-validation'], // Run after main validation
    },
    
    {
      name: 'mobile-validation',
      use: { 
        ...devices['iPhone 13'],
        baseURL: process.env.TEST_BASE_URL || 'https://app.aiprofilephotomaker.com'
      },
      testMatch: '**/image-upload-validation.spec.js',
      dependencies: ['production-validation'], // Run after main validation
    }
  ],

  // Global setup and teardown
  globalSetup: require.resolve('./setup/global-setup.js'),
  globalTeardown: require.resolve('./setup/global-teardown.js'),

  // Output directories
  outputDir: 'test-results/artifacts',
  
  // Web server configuration (for local development)
  webServer: process.env.START_LOCAL_SERVER ? {
    command: 'npm run dev',
    port: 3000,
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
  } : undefined,

  // Test expectations
  expect: {
    // Global assertion timeout
    timeout: 10000,
    
    // Screenshot comparison threshold
    threshold: 0.3,
    
    // Screenshot comparison mode
    mode: 'strict'
  }
});

// Environment-specific configuration overrides
if (process.env.NODE_ENV === 'development') {
  module.exports.use.headless = false;
  module.exports.use.video = 'on';
  module.exports.workers = 1;
}

if (process.env.DEBUG === 'true') {
  module.exports.use.headless = false;
  module.exports.use.slowMo = 1000;
  module.exports.timeout = 0; // No timeout for debugging
  module.exports.retries = 0;
}