export const environment = {
  production: false,
  development: true,
  test: false,
  
  // Local development URLs
  apiBaseUrl: 'http://localhost:5035/api',
  appBaseUrl: 'http://localhost:5035',
  frontendBaseUrl: 'http://localhost:4200',
  
  // External URLs for localtunnel access
  // These will be dynamically detected or can be set manually
  externalApiUrl: '', // Will be auto-detected or set via environment variable
  externalAppUrl: '', // Will be auto-detected or set via environment variable
  externalFrontendUrl: '', // Will be auto-detected or set via environment variable
  
  // Feature flags
  enableAutoUrlDetection: true,
  enableExternalAccess: true,
  
  // OAuth configuration
  oauth: {
    // Use external URLs for OAuth redirects when accessing via localtunnel
    useExternalUrls: true
  }
};