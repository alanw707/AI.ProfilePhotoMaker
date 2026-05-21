export const environment = {
  production: true,
  apiUrl: 'https://api.aiprofilephotomaker.com/api',
  baseUrl: 'https://api.aiprofilephotomaker.com',
  name: 'mvp-v1',
  turnstileSiteKey: '0x4AAAAAACHclVWNAEwekFCK',
  analytics: {
    ga4MeasurementId: 'G-FYQMYY2PJD',
  },
  features: {
    debugMode: false,
    useProxy: false,
    cors: true,
    enableImageValidation: true,
    enableReplicateCredits: true,
    openAIHeadshotMvp: false,
    profilePhotoWorkflowOverhaul: false,
    outcomePackagesVisible: false,
    profilePhotoScoreVisible: false,
    creativeStylePackVisible: true,
    premiumAugmentationsVisible: false,
    replicateTrainingFlowVisible: true,

    // Granular Logging Controls (Production - Minimal Noise)
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
    frontendUrl: 'https://aiprofilephotomaker.com',
    backendUrl: 'https://api.aiprofilephotomaker.com',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
    stylePreviewUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
