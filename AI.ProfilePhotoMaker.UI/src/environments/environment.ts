export const environment = {
  production: false,
  apiUrl: '/api', // Use proxy for simplicity in development
  baseUrl: '',
  name: 'development',
  features: {
    debugMode: true,
    useProxy: true,
    cors: true,
    enableImageValidation: false, // Disable excessive image validation in development
    enableReplicateCredits: false, // Disable Replicate API when TestController is disabled
  },
  ngrok: {
    enabled: false,
    frontendUrl: 'http://localhost:4200',
    backendUrl: 'http://localhost:5032', // Updated to match actual API port
  },
};
