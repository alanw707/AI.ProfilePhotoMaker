/**
 * Azure Storage configuration and credential management for tests
 */

import { readFileSync, existsSync } from 'fs';
import { join } from 'path';

export interface AzureConfig {
  connectionString?: string;
  sasUrl?: string;
  baseUrl: string;
  hasCredentials: boolean;
  testMode: 'pre-upload' | 'post-upload' | 'mixed';
  uploadTests: boolean;
  performanceTests: boolean;
  visualTests: boolean;
  timeout: number;
}

/**
 * Load Azure configuration from environment variables and .env file
 */
export function loadAzureConfig(): AzureConfig {
  // Load .env file if it exists
  const envPath = join(__dirname, '..', '.env');
  const envVars = new Map<string, string>();
  
  // First load from .env file
  if (existsSync(envPath)) {
    try {
      const envContent = readFileSync(envPath, 'utf-8');
      const lines = envContent.split('\n');
      
      for (const line of lines) {
        const trimmedLine = line.trim();
        if (trimmedLine && !trimmedLine.startsWith('#')) {
          const [key, ...valueParts] = trimmedLine.split('=');
          if (key && valueParts.length > 0) {
            const value = valueParts.join('=').replace(/^"(.*)"$/, '$1');
            envVars.set(key, value);
          }
        }
      }
    } catch (error) {
      console.warn('Failed to load .env file:', error);
    }
  }
  
  // Override with actual environment variables
  for (const [key, value] of Object.entries(process.env)) {
    if (value !== undefined) {
      envVars.set(key, value);
    }
  }
  
  const connectionString = envVars.get('AZURE_STORAGE_CONNECTION_STRING');
  const sasUrl = envVars.get('AZURE_STORAGE_SAS_URL');
  const baseUrl = envVars.get('AZURE_BLOB_BASE_URL') || 'https://aipmstv16j74jubocuukg.blob.core.windows.net/style-previews';
  
  const hasCredentials = !!(connectionString || sasUrl);
  
  // Determine test mode based on credential availability
  let testMode: 'pre-upload' | 'post-upload' | 'mixed' = 'pre-upload';
  if (hasCredentials) {
    // If we have credentials, we can potentially test both states
    testMode = 'mixed';
  }
  
  return {
    connectionString,
    sasUrl,
    baseUrl,
    hasCredentials,
    testMode,
    uploadTests: envVars.get('RUN_UPLOAD_TESTS') === 'true',
    performanceTests: envVars.get('RUN_PERFORMANCE_TESTS') !== 'false', // Default to true
    visualTests: envVars.get('ENABLE_VISUAL_TESTING') !== 'false', // Default to true
    timeout: parseInt(envVars.get('TEST_TIMEOUT_MS') || '30000', 10)
  };
}

/**
 * Validate Azure configuration and log status
 */
export function validateAzureConfig(config: AzureConfig): {
  valid: boolean;
  warnings: string[];
  recommendations: string[];
} {
  const warnings: string[] = [];
  const recommendations: string[] = [];
  
  if (!config.hasCredentials) {
    warnings.push('No Azure Storage credentials configured');
    recommendations.push('Run scripts/setup-credentials.sh to configure credentials');
    recommendations.push('Or manually create .env file with AZURE_STORAGE_CONNECTION_STRING');
  }
  
  if (config.connectionString && config.connectionString.includes('REPLACE_WITH_')) {
    warnings.push('Azure connection string contains placeholder values');
    recommendations.push('Update connection string with actual credentials');
  }
  
  if (!config.uploadTests && config.hasCredentials) {
    warnings.push('Upload tests disabled despite having credentials');
    recommendations.push('Set RUN_UPLOAD_TESTS=true to enable upload verification');
  }
  
  if (config.timeout < 10000) {
    warnings.push('Test timeout is very low, may cause flaky tests');
    recommendations.push('Consider increasing TEST_TIMEOUT_MS to at least 10000');
  }
  
  return {
    valid: config.hasCredentials || config.testMode === 'pre-upload',
    warnings,
    recommendations
  };
}

/**
 * Generate test configuration report
 */
export function generateConfigReport(config: AzureConfig): string {
  const validation = validateAzureConfig(config);
  
  let report = `
# Azure Storage Test Configuration Report

## Configuration Status
- **Has Credentials**: ${config.hasCredentials ? '✅ Yes' : '❌ No'}
- **Test Mode**: ${config.testMode}
- **Base URL**: ${config.baseUrl}
- **Upload Tests**: ${config.uploadTests ? 'Enabled' : 'Disabled'}
- **Performance Tests**: ${config.performanceTests ? 'Enabled' : 'Disabled'}
- **Visual Tests**: ${config.visualTests ? 'Enabled' : 'Disabled'}
- **Timeout**: ${config.timeout}ms

## Credential Details
`;

  if (config.connectionString) {
    const masked = config.connectionString.replace(/AccountKey=([^;]+)/, 'AccountKey=***MASKED***');
    report += `- **Connection String**: ${masked}\n`;
  }
  
  if (config.sasUrl) {
    const masked = config.sasUrl.replace(/(\?.*sig=)([^&]+)/, '$1***MASKED***');
    report += `- **SAS URL**: ${masked}\n`;
  }
  
  if (validation.warnings.length > 0) {
    report += `\n## ⚠️ Warnings\n`;
    validation.warnings.forEach(warning => {
      report += `- ${warning}\n`;
    });
  }
  
  if (validation.recommendations.length > 0) {
    report += `\n## 💡 Recommendations\n`;
    validation.recommendations.forEach(rec => {
      report += `- ${rec}\n`;
    });
  }
  
  return report;
}

/**
 * Get the appropriate Azure Storage client based on available credentials
 */
export function getStorageAuthOptions(config: AzureConfig): {
  hasAuth: boolean;
  authType: 'connection-string' | 'sas' | 'none';
  credentials?: any;
} {
  if (config.connectionString && !config.connectionString.includes('REPLACE_WITH_')) {
    return {
      hasAuth: true,
      authType: 'connection-string',
      credentials: {
        connectionString: config.connectionString
      }
    };
  }
  
  if (config.sasUrl) {
    return {
      hasAuth: true,
      authType: 'sas',
      credentials: {
        sasUrl: config.sasUrl
      }
    };
  }
  
  return {
    hasAuth: false,
    authType: 'none'
  };
}

// Global configuration instance
let globalConfig: AzureConfig | null = null;

/**
 * Get the global Azure configuration (lazy-loaded)
 */
export function getAzureConfig(): AzureConfig {
  if (!globalConfig) {
    globalConfig = loadAzureConfig();
  }
  return globalConfig;
}

/**
 * Reset the global configuration (useful for testing)
 */
export function resetAzureConfig(): void {
  globalConfig = null;
}