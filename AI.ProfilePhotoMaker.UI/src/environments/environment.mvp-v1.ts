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

    // NEW: Auto-Repair Feature Flags (MVP-v1 Configuration - Safety First)
    enableAutoRepair: false, // DISABLED initially for safety
    autoRepairDryRunOnly: true, // DRY-RUN ONLY when enabled
    autoRepairThreshold: 5, // Higher threshold for safety
    autoRepairCooldown: 24 * 60 * 60 * 1000, // 24-hour cooldown
    autoRepairMaxAttempts: 1, // Conservative attempt limit
    autoRepairTimeoutMs: 30000, // 30-second timeout
    autoRepairNotifications: true, // Show notifications for transparency
    autoRepairTelemetry: true, // Full metrics in production
    autoRepairValidationLevel: 'strict', // Strictest validation

    // Granular Logging Controls (Production - Minimal Noise)
    logging: {
      enableApiDebug: false, // Disabled for production
      enableStateDebug: false, // Disabled for production
      enableWorkflowDebug: false, // Disabled for production
      enableAuthDebug: false, // Disabled for production
      enableFileDebug: false, // Disabled for production
      enableGalleryDebug: false, // Disabled for production
      enableDashboardDebug: false, // Disabled for production
      enableAutoRepairDebug: false, // Disabled for production
    },
  },
  azure: {
    enabled: true,
    frontendUrl: 'https://app.aiprofilephotomaker.com',
    backendUrl: 'https://api.aiprofilephotomaker.com',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
