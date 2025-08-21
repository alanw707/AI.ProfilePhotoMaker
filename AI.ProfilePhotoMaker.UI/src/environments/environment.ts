export const environment = {
  production: false,
  apiUrl: '/api', // Use proxy for simplicity in development
  baseUrl: '',
  name: 'development',
  features: {
    debugMode: true,
    useProxy: true,
    cors: true,
    enableImageValidation: true, // Enable validation to test auto-repair
    enableReplicateCredits: false, // Disable Replicate API when TestController is disabled

    // NEW: Auto-Repair Feature Flags (Development Configuration)
    enableAutoRepair: true, // Enable for testing in development
    autoRepairDryRunOnly: false, // Allow actual repairs for dev testing
    autoRepairThreshold: 1, // Lower threshold for easier testing
    autoRepairCooldown: 5 * 60 * 1000, // 5-minute cooldown for rapid testing
    autoRepairMaxAttempts: 3, // Standard attempt limit
    autoRepairTimeoutMs: 30000, // 30-second timeout
    autoRepairNotifications: true, // Show notifications for debugging
    autoRepairTelemetry: true, // Enable telemetry for analysis
    autoRepairValidationLevel: 'lenient', // Less strict for dev data

    // Granular Logging Controls (Development - Reduced Noise)
    logging: {
      enableApiDebug: false, // Disable API call noise
      enableStateDebug: false, // Disable state change noise
      enableWorkflowDebug: false, // Disable workflow debugging
      enableAuthDebug: false, // Disable auth noise
      enableFileDebug: false, // Disable file operation noise
      enableGalleryDebug: false, // Disable gallery operation noise
      enableDashboardDebug: false, // Disable dashboard state noise
      enableAutoRepairDebug: false, // Disable auto-repair debug (can enable when needed)
    },
  },
  ngrok: {
    enabled: false,
    frontendUrl: 'http://localhost:4200',
    backendUrl: 'http://localhost:5032', // Updated to match actual API port
  },
};
