export const environment = {
  production: true,
  apiUrl: '/api',
  baseUrl: '',
  name: 'production',
  features: {
    debugMode: false,
    useProxy: false,
    cors: false,
    enableImageValidation: false,
    enableReplicateCredits: true,
  },
  ngrok: {
    enabled: false,
    frontendUrl: 'https://app.yourcompany.com',
    backendUrl: 'https://api.yourcompany.com',
  },
};
