export const environment = {
  production: true,
  apiUrl: '/api',
  baseUrl: '',
  name: 'docker-local',
  features: {
    debugMode: false,
    useProxy: true,
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
