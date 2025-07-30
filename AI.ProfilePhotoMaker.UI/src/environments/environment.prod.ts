export const environment = {
  production: true,
  apiUrl: 'https://aiprofilephotomakerapi.azurewebsites.net/api',
  baseUrl: 'https://aiprofilephotomakerapi.azurewebsites.net',
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
    frontendUrl: 'https://aiprofilephotomaker.azurestaticapps.net',
    backendUrl: 'https://aiprofilephotomakerapi.azurewebsites.net',
    storageUrl: 'https://aiprofilephotomaker.blob.core.windows.net',
  },
};
