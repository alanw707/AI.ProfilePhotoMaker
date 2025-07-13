export const environment = {
  production: false,
  apiUrl: '/api',
  baseUrl: '',
  name: 'development',
  features: {
    debugMode: true,
    useProxy: true,
    cors: true,
  },
  ngrok: {
    enabled: false,
    frontendUrl: 'http://localhost:4200',
    backendUrl: 'http://localhost:5035',
  },
};
