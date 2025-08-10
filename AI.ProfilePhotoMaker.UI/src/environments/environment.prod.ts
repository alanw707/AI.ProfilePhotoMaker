export const environment = {
  production: true,
  apiUrl: 'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api',
  baseUrl: 'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io',
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
    backendUrl: 'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
