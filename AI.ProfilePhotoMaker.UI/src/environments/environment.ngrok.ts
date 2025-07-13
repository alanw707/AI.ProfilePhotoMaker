export const environment = {
  production: false,
  apiUrl: '/api', // Use proxy for all API calls
  baseUrl: '', // Let the proxy handle routing
  name: 'ngrok',
  features: {
    debugMode: true,
    useProxy: true,
    cors: true,
  },
  ngrok: {
    enabled: true,
    frontendUrl: 'https://awlocaldev.ngrok.app',
    backendUrl: 'https://awlocaldev-api.ngrok.app',
  },
};
