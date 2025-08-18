// Enhanced Environment Configuration Template for Auto-Repair
// Copy the relevant sections to your environment files

export const environmentTemplate = {
  production: false, // or true for production
  apiUrl: '/api',
  baseUrl: '',
  name: 'development', // or 'staging', 'production'
  
  features: {
    // Existing flags
    debugMode: true,
    useProxy: true,
    cors: true,
    enableImageValidation: true, // Enable image validation
    enableReplicateCredits: false,
    
    // NEW: Auto-Repair Feature Flags
    enableAutoRepair: false,           // Master switch for auto-repair functionality
    autoRepairDryRunOnly: true,        // Safety mode - log repairs but don't execute
    autoRepairThreshold: 3,            // Minimum 404s before triggering auto-repair
    autoRepairCooldown: 24 * 60 * 60 * 1000,  // 24-hour cooldown between repairs
    autoRepairMaxAttempts: 3,          // Maximum repair attempts per session
    autoRepairTimeoutMs: 30000,        // 30-second timeout for repair operations
    
    // NEW: Advanced Auto-Repair Settings
    autoRepairNotifications: true,     // Show user notifications for repairs
    autoRepairTelemetry: true,         // Send repair telemetry/analytics
    autoRepairBackgroundMode: true,    // Run repairs in background
    autoRepairValidationLevel: 'strict', // 'strict', 'moderate', 'lenient'
  },
  
  // NEW: Auto-Repair Monitoring Configuration
  monitoring: {
    enableRepairMetrics: true,
    repairLogLevel: 'info', // 'debug', 'info', 'warn', 'error'
    repairHistoryRetentionDays: 30,
    enablePerformanceTracking: true,
  },
  
  // Environment-specific configurations
  ngrok: {
    enabled: false,
    frontendUrl: 'http://localhost:4200',
    backendUrl: 'http://localhost:5032',
  },
};

// Environment-specific recommendations:

// DEVELOPMENT ENVIRONMENT
export const developmentConfig = {
  features: {
    enableAutoRepair: true,           // Enable for testing
    autoRepairDryRunOnly: false,      // Allow actual repairs in dev
    autoRepairThreshold: 1,           // Lower threshold for testing
    autoRepairCooldown: 5 * 60 * 1000, // 5-minute cooldown for dev
    autoRepairValidationLevel: 'lenient', // Less strict for dev data
  },
  monitoring: {
    repairLogLevel: 'debug',          // Verbose logging in dev
  },
};

// STAGING ENVIRONMENT
export const stagingConfig = {
  features: {
    enableAutoRepair: true,           // Enable for validation
    autoRepairDryRunOnly: true,       // DRY-RUN ONLY in staging initially
    autoRepairThreshold: 3,           // Conservative threshold
    autoRepairCooldown: 12 * 60 * 60 * 1000, // 12-hour cooldown
    autoRepairValidationLevel: 'strict', // Strict validation
  },
  monitoring: {
    repairLogLevel: 'info',           // Standard logging
    enablePerformanceTracking: true,  // Monitor performance impact
  },
};

// PRODUCTION ENVIRONMENT
export const productionConfig = {
  features: {
    enableAutoRepair: false,          // DISABLED initially in production
    autoRepairDryRunOnly: true,       // DRY-RUN ONLY when enabled
    autoRepairThreshold: 5,           // Higher threshold for safety
    autoRepairCooldown: 24 * 60 * 60 * 1000, // 24-hour cooldown
    autoRepairMaxAttempts: 1,         // Conservative attempt limit
    autoRepairValidationLevel: 'strict', // Strictest validation
  },
  monitoring: {
    repairLogLevel: 'warn',           // Only warnings and errors
    enableRepairMetrics: true,        // Full metrics in production
    enablePerformanceTracking: true,  // Critical for production
  },
};

// Implementation notes:
/*
1. Copy the relevant feature flags to your environment.ts files
2. Start with conservative settings (dry-run mode, high thresholds)
3. Gradually relax settings based on validation results
4. Monitor performance and error rates after each change
5. Use feature flags to quickly disable if issues arise

Example environment.ts update:
```typescript
export const environment = {
  // ... existing config ...
  features: {
    // ... existing features ...
    enableAutoRepair: false,           // Start disabled
    autoRepairDryRunOnly: true,        // Safety first
    autoRepairThreshold: 5,            // Conservative
    autoRepairCooldown: 24 * 60 * 60 * 1000,
  },
};
```
*/