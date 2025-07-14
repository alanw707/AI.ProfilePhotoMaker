export const environment = {
  production: false,
  apiUrl: '/api',
  baseUrl: '',
  name: 'test',
  features: {
    debugMode: true,
    useProxy: true,
    cors: true,
    enableImageValidation: false,
  },
  ngrok: {
    enabled: false,
    frontendUrl: 'https://test.yourcompany.com',
    backendUrl: 'https://test-api.yourcompany.com',
  },
  // Test-specific settings
  testSettings: {
    mockPayments: true,
    enableTestData: true,
    skipEmailVerification: true,
    debugMode: true,
  },
};
