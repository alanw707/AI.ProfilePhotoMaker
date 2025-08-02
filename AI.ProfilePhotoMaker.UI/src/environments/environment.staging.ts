export const environment = {
  production: false,
  apiUrl:
    'https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api',
  baseUrl:
    'https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io',
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
    frontendUrl:
      'https://aiprofilemaker-web-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io',
    backendUrl:
      'https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io',
    storageUrl: 'https://aiprofilemakerstrg3bawc74.blob.core.windows.net',
  },
};
