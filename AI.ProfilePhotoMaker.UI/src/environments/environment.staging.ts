export const environment = {
  production: false,
  apiUrl: 'https://api-apm-simple.nicestone-1ec028d4.eastus.azurecontainerapps.io/api',
  baseUrl: 'https://api-apm-simple.nicestone-1ec028d4.eastus.azurecontainerapps.io',
  name: 'staging',
  features: {
    debugMode: true,
    useProxy: false,
    cors: true,
    enableImageValidation: true,
    enableReplicateCredits: true,
  },
  azure: {
    enabled: true,
    frontendUrl: 'https://ui-apm-simple.nicestone-1ec028d4.eastus.azurecontainerapps.io',
    backendUrl: 'https://api-apm-simple.nicestone-1ec028d4.eastus.azurecontainerapps.io',
    storageUrl: 'https://apmstorage81948.blob.core.windows.net',
  },
};
