export const environment = {
  production: true,
  apiUrl: 'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api',
  baseUrl: 'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io',
  name: 'v1',
  features: {
    debugMode: false,
    useProxy: false,
    cors: true,
    enableImageValidation: true,
    enableReplicateCredits: true,
  },
  azure: {
    enabled: true,
    frontendUrl: 'https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io',
    backendUrl: 'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
