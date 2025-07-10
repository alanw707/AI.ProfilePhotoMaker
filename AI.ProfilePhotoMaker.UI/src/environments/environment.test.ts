export const environment = {
  production: false,
  development: false,
  test: true,
  
  // Test environment URLs - will be auto-detected from backend
  apiBaseUrl: 'https://test-api.profilephotomaker.com/api',
  appBaseUrl: 'https://test-api.profilephotomaker.com',
  frontendBaseUrl: 'https://test-app.profilephotomaker.com',
  
  // External URLs (same as main URLs in test)
  externalApiUrl: 'https://test-api.profilephotomaker.com/api',
  externalAppUrl: 'https://test-api.profilephotomaker.com',
  externalFrontendUrl: 'https://test-app.profilephotomaker.com',
  
  // Feature flags for test environment
  enableAutoUrlDetection: true,
  enableExternalAccess: true,
  enableConfigurationDebug: true,
  
  // OAuth configuration
  oauth: {
    useExternalUrls: true
  },
  
  // Test-specific settings
  testSettings: {
    mockPayments: true,
    enableTestData: true,
    skipEmailVerification: true,
    debugMode: true
  }
};