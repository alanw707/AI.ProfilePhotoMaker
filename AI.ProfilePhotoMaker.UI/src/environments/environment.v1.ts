export const environment = {
  production: true,
  apiUrl: 'https://aiprofilemaker-api-v1.eastus.azurecontainerapps.io/api',
  baseUrl: 'https://aiprofilemaker-api-v1.eastus.azurecontainerapps.io',
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
    frontendUrl: 'https://aiprofilemaker-web-v1.eastus.azurecontainerapps.io',
    backendUrl: 'https://aiprofilemaker-api-v1.eastus.azurecontainerapps.io',
    storageUrl: 'https://[storage-account-name].blob.core.windows.net',
  },
};
