export const environment = {
  production: true,
  apiUrl: 'https://api.aiprofilephotomaker.com/api',
  baseUrl: 'https://api.aiprofilephotomaker.com',
  name: 'production',
  features: {
    debugMode: false,
    useProxy: false,
    cors: true, // Enable CORS for cross-origin requests to Azure API
    enableImageValidation: true, // Enable validation in production
    enableReplicateCredits: true, // Enable Replicate API in production

    // NEW: Auto-Repair Feature Flags (Production Configuration - Safety First)
    enableAutoRepair: false, // DISABLED initially for safety
    autoRepairDryRunOnly: true, // DRY-RUN ONLY when enabled
    autoRepairThreshold: 5, // Higher threshold for safety
    autoRepairCooldown: 24 * 60 * 60 * 1000, // 24-hour cooldown
    autoRepairMaxAttempts: 1, // Conservative attempt limit
    autoRepairTimeoutMs: 30000, // 30-second timeout
    autoRepairNotifications: true, // Show notifications for transparency
    autoRepairTelemetry: true, // Full metrics in production
    autoRepairValidationLevel: 'strict', // Strictest validation

    // Granular Logging Controls (Production - Minimal Logging)
    logging: {
      enableApiDebug: false, // No API debug noise in production
      enableStateDebug: false, // No state debug noise in production
      enableWorkflowDebug: false, // No workflow debug in production
      enableAuthDebug: false, // No auth debug noise in production
      enableFileDebug: false, // No file debug noise in production
      enableGalleryDebug: false, // No gallery debug noise in production
      enableDashboardDebug: false, // No dashboard debug noise in production
      enableAutoRepairDebug: false, // No auto-repair debug in production
    },
  },
  azure: {
    enabled: true,
    frontendUrl: 'https://app.aiprofilephotomaker.com',
    backendUrl: 'https://api.aiprofilephotomaker.com',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
