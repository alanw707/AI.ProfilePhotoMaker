export const environment = {
  production: false,
  apiUrl: '/api', // Use proxy for simplicity in development
  baseUrl: '',
  name: 'development',
  turnstileSiteKey: '',
  analytics: {
    ga4MeasurementId: '',
  },
  features: {
    debugMode: true,
    useProxy: true,
    cors: true,
    enableImageValidation: true, // Enable validation to test auto-repair
    enableReplicateCredits: false, // Disable Replicate API when TestController is disabled

    // Granular Logging Controls (Development - Reduced Noise)
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
  ngrok: {
    enabled: false,
    frontendUrl: 'http://localhost:4200',
    backendUrl: 'http://localhost:5032', // Updated to match actual API port
  },
};
