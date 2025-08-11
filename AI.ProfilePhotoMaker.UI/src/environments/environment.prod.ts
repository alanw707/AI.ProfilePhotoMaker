export const environment = {
  production: true,
  apiUrl: 'https://api.aiprofilephotomaker.com/api',
  baseUrl: 'https://api.aiprofilephotomaker.com',
  name: 'production',
  features: {
    debugMode: false,
    useProxy: false,
    cors: true, // Enable CORS for cross-origin requests to Azure API
    enableImageValidation: true, // Enable validation in production
    enableReplicateCredits: true, // Enable Replicate API in production
  },
  azure: {
    enabled: true,
    frontendUrl: 'https://app.aiprofilephotomaker.com',
    backendUrl: 'https://api.aiprofilephotomaker.com',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
