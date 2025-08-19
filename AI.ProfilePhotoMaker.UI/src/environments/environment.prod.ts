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
  },
  azure: {
    enabled: true,
    frontendUrl: 'https://app.aiprofilephotomaker.com',
    backendUrl: 'https://api.aiprofilephotomaker.com',
    storageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net',
  },
};
