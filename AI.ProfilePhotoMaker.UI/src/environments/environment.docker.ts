export const environment = {
  production: true,
  apiUrl: 'http://localhost:5032/api',
  baseUrl: 'http://localhost:5032',
  name: 'docker-local',
  turnstileSiteKey: '',
  features: {
    debugMode: false,
    useProxy: false,
    cors: true,
    enableImageValidation: true,
    enableReplicateCredits: false,
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
    backendUrl: 'http://localhost:5032',
  },
};
