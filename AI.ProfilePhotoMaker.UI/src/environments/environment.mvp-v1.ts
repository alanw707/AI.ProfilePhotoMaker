export const environment = {
  production: true,
  apiUrl: 'https://api.aiprofilephotomaker.com/api',
  baseUrl: 'https://api.aiprofilephotomaker.com',
  name: 'mvp-v1',
  features: {
    debugMode: false,
    useProxy: false,
    cors: true,
    enableImageValidation: true,
    enableReplicateCredits: true,
  },
  azure: {
    enabled: true,
    frontendUrl: 'https://app.aiprofilephotomaker.com',
    backendUrl: 'https://api.aiprofilephotomaker.com',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
