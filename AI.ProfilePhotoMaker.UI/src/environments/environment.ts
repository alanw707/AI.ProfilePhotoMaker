export const environment = {
  production: false,
  development: true,
  test: false,
  
  // Dynamic URL configuration
  apiBaseUrl: 'http://localhost:5035/api',
  appBaseUrl: 'http://localhost:5035',
  frontendBaseUrl: 'http://localhost:4200',
  
  // External URLs for tunneling (localtunnel, ngrok, etc.)
  // These can be overridden at runtime
  externalApiUrl: '',
  externalAppUrl: '',
  externalFrontendUrl: '',
  
  // Feature flags
  enableAutoUrlDetection: true,
  enableExternalAccess: true,
  
  // OAuth configuration
  oauth: {
    // Will be dynamically determined based on current domain
    useExternalUrls: false
  }
};