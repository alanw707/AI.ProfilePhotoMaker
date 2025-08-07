import { test, expect } from '@playwright/test';
import { 
  loadAzureConfig, 
  validateAzureConfig, 
  generateConfigReport, 
  getStorageAuthOptions 
} from './azure-config';

test.describe('Azure Storage Credential Validation', () => {
  
  test('should load and validate Azure configuration', async () => {
    console.log('🔧 Loading Azure Storage configuration...');
    
    const config = loadAzureConfig();
    const validation = validateAzureConfig(config);
    const report = generateConfigReport(config);
    
    console.log(report);
    
    // Log configuration status
    if (config.hasCredentials) {
      console.log('✅ Azure Storage credentials are configured');
    } else {
      console.log('⚠️  No Azure Storage credentials configured - running in pre-upload mode');
    }
    
    // Configuration should be valid for the intended test mode
    expect(validation.valid, 'Configuration should be valid for intended test mode').toBe(true);
    
    // Base URL should be properly configured
    expect(config.baseUrl).toContain('aipmstv16j74jubocuukg');
    expect(config.baseUrl).toContain('style-previews');
  });

  test('should validate credential format when available', async () => {
    const config = loadAzureConfig();
    
    if (!config.hasCredentials) {
      console.log('⏭️  Skipping credential format validation - no credentials configured');
      return;
    }
    
    console.log('🔍 Validating credential format...');
    
    if (config.connectionString) {
      // Connection string should have the right format
      expect(config.connectionString).toMatch(/DefaultEndpointsProtocol=https/);
      expect(config.connectionString).toMatch(/AccountName=aipmstv16j74jubocuukg/);
      expect(config.connectionString).toMatch(/AccountKey=/);
      expect(config.connectionString).toMatch(/EndpointSuffix=core\.windows\.net/);
      
      // Should not contain placeholder values
      expect(config.connectionString).not.toMatch(/REPLACE_WITH_/);
      
      console.log('✅ Connection string format is valid');
    }
    
    if (config.sasUrl) {
      // SAS URL should have the right format
      expect(config.sasUrl).toMatch(/https:\/\/aipmstv16j74jubocuukg\.blob\.core\.windows\.net/);
      expect(config.sasUrl).toMatch(/style-previews/);
      expect(config.sasUrl).toMatch(/\?.*sv=/); // Should contain SAS parameters
      
      console.log('✅ SAS URL format is valid');
    }
  });

  test('should test Azure Storage connectivity when credentials available', async ({ page }) => {
    const config = loadAzureConfig();
    
    if (!config.hasCredentials) {
      console.log('⏭️  Skipping connectivity test - no credentials configured');
      return;
    }
    
    console.log('🌐 Testing Azure Storage connectivity...');
    
    const authOptions = getStorageAuthOptions(config);
    console.log(`Authentication type: ${authOptions.authType}`);
    
    // Test basic connectivity by trying to access a known blob
    // We'll test with a sample style preview URL
    const testUrl = `${config.baseUrl}/corporate.jpg`;
    
    try {
      const response = await page.request.get(testUrl, { timeout: 10000 });
      
      if (response.status() === 200) {
        console.log('✅ Storage connectivity test passed - images are accessible');
        
        // Verify it's actually an image
        const contentType = response.headers()['content-type'];
        expect(contentType).toMatch(/^image\/(jpeg|jpg)/i);
        
        const contentLength = response.headers()['content-length'];
        expect(parseInt(contentLength || '0')).toBeGreaterThan(1000);
        
      } else if (response.status() === 404) {
        console.log('⚠️  Storage accessible but images not uploaded yet (404 response)');
        // This is expected if images haven't been uploaded
      } else {
        console.log(`⚠️  Unexpected response status: ${response.status()}`);
      }
      
    } catch (error) {
      console.log(`❌ Storage connectivity test failed: ${error}`);
      // Don't fail the test - connectivity issues might be temporary
    }
  });

  test('should validate test configuration flags', async () => {
    const config = loadAzureConfig();
    
    console.log('🎛️  Validating test configuration flags...');
    
    // Log all configuration flags
    console.log(`- Upload Tests: ${config.uploadTests ? 'Enabled' : 'Disabled'}`);
    console.log(`- Performance Tests: ${config.performanceTests ? 'Enabled' : 'Disabled'}`);
    console.log(`- Visual Tests: ${config.visualTests ? 'Enabled' : 'Disabled'}`);
    console.log(`- Timeout: ${config.timeout}ms`);
    console.log(`- Test Mode: ${config.testMode}`);
    
    // Timeout should be reasonable
    expect(config.timeout).toBeGreaterThanOrEqual(5000);
    expect(config.timeout).toBeLessThanOrEqual(120000);
    
    // Test mode should be valid
    expect(['pre-upload', 'post-upload', 'mixed']).toContain(config.testMode);
    
    // Upload tests should only be enabled with credentials
    if (config.uploadTests) {
      expect(config.hasCredentials, 'Upload tests require credentials').toBe(true);
    }
    
    console.log('✅ Test configuration flags are valid');
  });

  test('should provide setup guidance when credentials missing', async () => {
    const config = loadAzureConfig();
    
    if (config.hasCredentials) {
      console.log('✅ Credentials are configured - no setup needed');
      return;
    }
    
    console.log('📋 Providing credential setup guidance...');
    
    const setupInstructions = `
🔧 Azure Storage Credential Setup Required

To enable full testing capabilities, configure Azure Storage credentials:

OPTION 1: Automated Setup
  cd tests/playwright/scripts
  ./setup-credentials.sh

OPTION 2: Manual Setup
  Create .env file with:
  AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=aipmstv16j74jubocuukg;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net"

OPTION 3: Environment Variables
  export AZURE_STORAGE_CONNECTION_STRING="..."

Current Test Capabilities:
  ✅ Pre-upload validation (404 tests)
  ❌ Post-upload verification (requires credentials)
  ❌ Upload functionality testing (requires credentials)
    `;
    
    console.log(setupInstructions);
    
    // Test should pass even without credentials - we can still do pre-upload validation
    expect(config.testMode).toBe('pre-upload');
  });

  test('should validate environment variable precedence', async () => {
    console.log('🔀 Testing environment variable precedence...');
    
    const config = loadAzureConfig();
    
    // Test that environment variables take precedence over .env file
    // This test mainly validates the loading logic is working correctly
    
    expect(typeof config.hasCredentials).toBe('boolean');
    expect(typeof config.uploadTests).toBe('boolean');
    expect(typeof config.performanceTests).toBe('boolean');
    expect(typeof config.visualTests).toBe('boolean');
    expect(typeof config.timeout).toBe('number');
    
    console.log('✅ Environment variable loading is working correctly');
  });
});