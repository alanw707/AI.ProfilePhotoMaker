export const environment = {
  production: true,
  apiUrl: 'https://api.aiprofilephotomaker.com/api',
  baseUrl: 'https://api.aiprofilephotomaker.com',
  name: 'production',
  turnstileSiteKey: '0x4AAAAAAACHclVWNAEwekFCK',
  features: {
    debugMode: false,
    useProxy: false,
    cors: true, // Enable CORS for cross-origin requests to Azure API
    enableImageValidation: true, // Enable validation in production
    enableReplicateCredits: true, // Enable Replicate API in production

    // Granular Logging Controls (Production - Minimal Logging)
    logging: {
      enableApiDebug: false,
      enableStateDebug: false,
      enableWorkflowDebug: false,
      enableAuthDebug: false,
      enableFileDebug: false,
      enableGalleryDebug: false,
      enableDashboardDebug: false,
    },
  },
  azure: {
    enabled: true,
    frontendUrl: 'https://app.aiprofilephotomaker.com',
    backendUrl: 'https://api.aiprofilephotomaker.com',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
