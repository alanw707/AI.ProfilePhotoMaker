export const environment = {
  production: true,
  development: false,
  test: false,
  
  // Production URLs - these should be replaced with actual production domains
  apiBaseUrl: 'https://your-production-api.com/api',
  appBaseUrl: 'https://your-production-api.com',
  frontendBaseUrl: 'https://your-production-frontend.com',
  
  // External URLs (same as production in this case)
  externalApiUrl: 'https://your-production-api.com/api',
  externalAppUrl: 'https://your-production-api.com',
  externalFrontendUrl: 'https://your-production-frontend.com',
  
  // Feature flags
  enableAutoUrlDetection: false,
  enableExternalAccess: false,
  
  // OAuth configuration
  oauth: {
    useExternalUrls: false
  }
};