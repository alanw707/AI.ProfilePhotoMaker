/**
 * E2E Test: Image Upload Validation
 * 
 * Tests the complete image upload flow to validate:
 * 1. Images can be uploaded successfully
 * 2. Uploaded images are accessible via generated URLs
 * 3. Storage service is properly configured (Azure vs Local)
 * 
 * This test helps detect the production issue where LocalStorageService
 * generates inaccessible URLs in containerized environments.
 */

const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

// Test configuration
const config = {
  // UI base URL (can be overridden via TEST_BASE_URL)
  baseUrl: process.env.TEST_BASE_URL || 'https://app.aiprofilephotomaker.com',
  // API base URL (can be overridden via TEST_API_BASE_URL / API_BASE_URL)
  apiBaseUrl: (process.env.TEST_API_BASE_URL || process.env.API_BASE_URL || 'https://api.aiprofilephotomaker.com')
    .replace(/\/api\/?$/i, '')
    .replace(/\/+$/, ''),
  
  // Test credentials (create a dedicated test user for this)
  testUser: {
    email: process.env.UPLOAD_TEST_EMAIL || 'test.upload@example.com',
    password: process.env.UPLOAD_TEST_PASSWORD || 'TestUpload123!'
  },
  
  // Test image path (create a small test image)
  testImagePath: path.join(__dirname, 'test-images', 'sample-selfie.jpg')
};

test.describe('Image Upload Flow Validation', () => {
  
  test.beforeAll(async () => {
    // Verify test image exists
    if (!fs.existsSync(config.testImagePath)) {
      throw new Error(`Test image not found: ${config.testImagePath}`);
    }
  });

  test('should upload image and validate accessibility', async ({ page }) => {
    // Pre-flight storage health check
    console.log('🔍 Pre-flight: Checking storage service health...');
    const preflightHealth = await validateStorageHealth(page);
    console.log(`📊 Storage Service: ${preflightHealth.provider} (${preflightHealth.status})`);
    
    // Step 1: Navigate to application
    console.log('🌐 Navigating to application...');
    await page.goto(config.baseUrl);
    
    // Step 2: Login with test user (with retry logic)
    console.log('🔐 Attempting login...');
    await performLoginWithRetry(page);
    
    // Step 3: Navigate to upload section
    console.log('📤 Navigating to upload section...');
    await page.click('[data-testid="upload-section"]');
    
    // Step 4: Upload test image (with enhanced validation)
    console.log('📁 Uploading test image...');
    const uploadResult = await performImageUploadWithValidation(page);
    
    // Step 5: Validate end-to-end image accessibility
    console.log('🔍 Validating image accessibility and storage configuration...');
    await validateImageUrl(page, uploadResult.imageUrl);
    
    // Step 6: Post-upload storage validation
    console.log('🏥 Post-upload: Validating storage operations...');
    const postUploadHealth = await validateStorageHealth(page);
    expect(postUploadHealth.status).toBe('Healthy');
    
    console.log('✅ Complete image upload and validation test passed!');
  });
  
  /**
   * Performs login with retry logic for better reliability
   */
  async function performLoginWithRetry(page, maxRetries = 3) {
    for (let attempt = 1; attempt <= maxRetries; attempt++) {
      try {
        console.log(`🔐 Login attempt ${attempt}/${maxRetries}...`);
        
        await page.click('[data-testid="login-button"]', { timeout: 10000 });
        
        // Fill login form
        await page.fill('input[type="email"]', config.testUser.email);
        await page.fill('input[type="password"]', config.testUser.password);
        await page.click('button[type="submit"]');
        
        // Wait for successful login
        await page.waitForSelector('[data-testid="dashboard"]', { timeout: 15000 });
        console.log('✅ Login successful');
        return;
        
      } catch (error) {
        console.log(`⚠️ Login attempt ${attempt} failed:`, error.message);
        
        if (attempt === maxRetries) {
          throw new Error(`Login failed after ${maxRetries} attempts: ${error.message}`);
        }
        
        // Wait before retry
        await page.waitForTimeout(2000);
        
        // Try to return to base page for retry
        try {
          await page.goto(config.baseUrl);
        } catch (navError) {
          console.log('⚠️ Navigation reset failed, continuing retry...');
        }
      }
    }
  }
  
  /**
   * Performs image upload with comprehensive validation
   */
  async function performImageUploadWithValidation(page) {
    const uploadStartTime = Date.now();
    
    try {
      // Set up file chooser listener
      const fileChooserPromise = page.waitForEvent('filechooser');
      
      // Trigger file selection
      await page.click('input[type="file"]');
      const fileChooser = await fileChooserPromise;
      await fileChooser.setFiles(config.testImagePath);
      
      console.log('📁 File selected, waiting for upload to complete...');
      
      // Wait for upload progress to disappear (indicating completion)
      try {
        await page.waitForSelector('[data-testid="upload-progress"]', { state: 'hidden', timeout: 45000 });
      } catch (progressError) {
        // Fallback: check for success indicators
        console.log('⚠️ Upload progress element not found, checking for success indicators...');
        await page.waitForSelector('[data-testid="upload-success"], [data-testid="uploaded-image"]', { timeout: 30000 });
      }
      
      const uploadDuration = Date.now() - uploadStartTime;
      console.log(`⏱️ Upload completed in ${uploadDuration}ms`);
      
      // Verify images appear in the uploaded images list
      const uploadedImages = page.locator('[data-testid="uploaded-image"]');
      await expect(uploadedImages).toHaveCountGreaterThan(0);
      
      // Get the uploaded image URL
      const firstImage = uploadedImages.first();
      const imageUrl = await firstImage.getAttribute('src');
      
      if (!imageUrl) {
        throw new Error('No image URL found after upload');
      }
      
      console.log('🔍 Upload successful, image URL detected:', imageUrl);
      
      return {
        imageUrl,
        uploadDuration,
        timestamp: new Date().toISOString()
      };
      
    } catch (error) {
      console.error('❌ Image upload failed:', error.message);
      
      // Capture page state for debugging
      console.log('📸 Capturing page state for debugging...');
      try {
        const pageContent = await page.content();
        console.log('📄 Page title:', await page.title());
        console.log('🔍 Current URL:', page.url());
        
        // Look for error messages
        const errorElements = await page.locator('[data-testid*="error"], .error, .alert-error').all();
        if (errorElements.length > 0) {
          console.log('🚨 Error messages found on page:');
          for (const errorElement of errorElements) {
            const errorText = await errorElement.textContent();
            console.log(`  ❌ ${errorText}`);
          }
        }
      } catch (debugError) {
        console.log('⚠️ Could not capture page state for debugging:', debugError.message);
      }
      
      throw error;
    }
  }
  
  /**
   * Validates storage service health
   */
  async function validateStorageHealth(page) {
    try {
      const healthUrl = `${config.apiBaseUrl}/api/health/storage`;
      const response = await page.request.get(healthUrl);
      const healthData = await response.json();
      
      return {
        status: healthData.status || 'Unknown',
        provider: healthData.provider || 'Unknown',
        canConnect: healthData.canConnect || false,
        response: healthData
      };
    } catch (error) {
      console.log('⚠️ Could not retrieve storage health:', error.message);
      return {
        status: 'Error',
        provider: 'Unknown',
        canConnect: false,
        error: error.message
      };
    }
  }

  /**
   * Validates image URL and determines storage configuration
   */
  async function validateImageUrl(page, imageUrl) {
    const resolvedUrl = /^https?:\/\//i.test(imageUrl)
      ? imageUrl
      : new URL(imageUrl, config.apiBaseUrl).toString();

    // Analyze URL pattern to determine storage service
    const urlAnalysis = analyzeImageUrl(resolvedUrl);
    console.log('📊 URL Analysis:', urlAnalysis);
    
    // Test image accessibility
    const response = await page.request.get(resolvedUrl);
    const isAccessible = response.ok();
    
    console.log(`🌐 Image accessibility: ${isAccessible ? '✅ Accessible' : '❌ Not accessible'} (Status: ${response.status()})`);
    
    if (!isAccessible) {
      console.error('🚨 Image upload issue detected:');
      console.error(`   URL: ${resolvedUrl}`);
      console.error(`   Status: ${response.status()}`);
      console.error(`   Storage Type: ${urlAnalysis.storageType}`);
      
      if (urlAnalysis.storageType === 'local') {
        console.error('🔧 SOLUTION: Configure Azure Storage environment variables:');
        console.error('   - AZURE_STORAGE_CONNECTION_STRING');
        console.error('   - AZURE_STORAGE_CONTAINER_NAME');
      }
    }
    
    // Assert image is accessible
    expect(isAccessible).toBeTruthy();
    
    // Validate correct storage type for environment
    if (config.baseUrl.includes('app.aiprofilephotomaker.com')) {
      // Production should use Azure Blob Storage
      expect(urlAnalysis.storageType).toBe('azure');
      console.log('✅ Production correctly using Azure Blob Storage');
    }
  }

  /**
   * Analyzes image URL to determine storage service type
   */
  function analyzeImageUrl(url) {
    const analysis = {
      url: url,
      storageType: 'unknown',
      containerized: false,
      recommendations: []
    };
    
    if (url.includes('blob.core.windows.net')) {
      analysis.storageType = 'azure';
      analysis.containerized = true;
      analysis.recommendations.push('✅ Using Azure Blob Storage - optimal for production');
    } else if (url.includes('/profile-images/') || url.includes('/uploads/')) {
      analysis.storageType = 'local';
      analysis.containerized = false;
      analysis.recommendations.push('⚠️ Using Local Storage - may fail in containerized environments');
      analysis.recommendations.push('🔧 Configure Azure Storage for production reliability');
    } else if (url.includes('/devstoreaccount1/')) {
      analysis.storageType = 'azurite';
      analysis.containerized = true;
      analysis.recommendations.push('✅ Using Azurite (Azure Storage Emulator) - good for development');
    }
    
    return analysis;
  }
});

test.describe('Storage Service Health Check', () => {
  
  test('should validate storage service configuration via health endpoint', async ({ page }) => {
    // Test the comprehensive storage health endpoint
    const healthUrl = `${config.apiBaseUrl}/api/health/storage`;
    
    console.log('🔍 Testing storage health endpoint:', healthUrl);
    
    try {
      const response = await page.request.get(healthUrl);
      const healthData = await response.json();
      
      console.log('🏥 Storage Health Response:', JSON.stringify(healthData, null, 2));
      
      // Validate storage service is healthy
      expect(response.ok()).toBeTruthy();
      expect(healthData.status).toBe('Healthy');
      
      // Validate storage provider configuration
      if (healthData.provider) {
        console.log(`📋 Storage Provider: ${healthData.provider}`);
        
        // For production, validate Azure Blob Storage is used
        if (config.baseUrl.includes('app.aiprofilephotomaker.com')) {
          expect(healthData.provider).toBe('AzureBlobStorage');
          console.log('✅ Production correctly configured with Azure Blob Storage');
          
          // Validate connection capabilities
          expect(healthData.canConnect).toBe(true);
          console.log('✅ Azure Storage connectivity confirmed');
          
          // Check for emulator usage (should not be in production)
          if (healthData.isEmulator === true) {
            console.warn('⚠️ WARNING: Production appears to be using Azure Storage emulator');
            expect(healthData.isEmulator).toBe(false);
          }
          
        } else {
          console.log(`ℹ️ Non-production environment using: ${healthData.provider}`);
        }
        
      // Validate operation test results
      if (healthData.testResults) {
        console.log('🧪 Storage Operations Test Results:');
        Object.entries(healthData.testResults).forEach(([operation, success]) => {
          console.log(`  ${operation}: ${success ? '✅ PASS' : '❌ FAIL'}`);
        });
        
        // Critical operations must pass
        expect(healthData.testResults.upload).toBe(true);
        expect(healthData.testResults.exists).toBe(true);
        expect(healthData.testResults.delete).toBe(true);
      }
        
      } else {
        console.error('❌ Storage provider information missing from health response');
        throw new Error('Storage provider not reported in health check');
      }
      
    } catch (error) {
      console.error('❌ Storage health endpoint failed:', error.message);
      throw error; // Re-throw to fail the test
    }
  });
  
  test('should validate end-to-end storage integration', async ({ page }) => {
    console.log('🧪 Testing end-to-end storage integration...');
    
    // Get baseline storage configuration
    const storageHealthUrl = `${config.apiBaseUrl}/api/health/storage`;
    const storageHealth = await page.request.get(storageHealthUrl);
    const storageData = await storageHealth.json();
    
    console.log(`📊 Baseline Storage Config: ${storageData.provider} (Can Connect: ${storageData.canConnect})`);
    
    // Test overall API health
    const healthUrl = `${config.apiBaseUrl}/api/health`;
    
    try {
      const healthResponse = await page.request.get(healthUrl);
      const healthData = await healthResponse.json();
      
      console.log('🏥 API Health Status:', healthData.status);
      
      // Validate overall system health
      expect(healthResponse.ok()).toBeTruthy();
      expect(healthData.status).toBe('Healthy');

      // Basic expected checks should be present
      if (Array.isArray(healthData.checks)) {
        const names = healthData.checks.map(c => c.name);
        expect(names).toContain('database');
        expect(names).toContain('migrations');
      }
      
    } catch (error) {
      console.error('❌ API health check failed:', error.message);
      throw error;
    }
  });
  
  test('should detect and report storage configuration issues', async ({ page }) => {
    console.log('🔍 Testing storage configuration issue detection...');
    
    const storageHealthUrl = `${config.apiBaseUrl}/api/health/storage`;
    const response = await page.request.get(storageHealthUrl);
    const healthData = await response.json();
    
    // Create detailed configuration report
    const configReport = {
      provider: healthData.provider,
      canConnect: healthData.canConnect,
      hasConnectionString: healthData.hasConnectionString,
      isEmulator: healthData.isEmulator,
      environment: config.baseUrl.includes('app.aiprofilephotomaker.com') ? 'Production' : 'Development',
      expectedProvider: config.baseUrl.includes('app.aiprofilephotomaker.com') ? 'AzureBlobStorage' : 'Any',
      configurationValid: true,
      issues: [],
      recommendations: []
    };
    
    // Detect configuration issues
    if (configReport.environment === 'Production') {
      if (configReport.provider !== 'AzureBlobStorage') {
        configReport.configurationValid = false;
        configReport.issues.push(`Production using ${configReport.provider} instead of AzureBlobStorage`);
        configReport.recommendations.push('Configure Azure Storage environment variables');
        configReport.recommendations.push('Check ConnectionStrings__AzureStorage and AzureStorage__ContainerName');
      }
      
      if (configReport.isEmulator === true) {
        configReport.configurationValid = false;
        configReport.issues.push('Production using Azure Storage emulator instead of real Azure Storage');
        configReport.recommendations.push('Update connection string to use real Azure Storage account');
      }
      
      if (!configReport.canConnect) {
        configReport.configurationValid = false;
        configReport.issues.push('Cannot connect to configured storage service');
        configReport.recommendations.push('Verify storage account exists and credentials are correct');
      }
    }
    
    // Report findings
    console.log('📋 Storage Configuration Report:');
    console.log(JSON.stringify(configReport, null, 2));
    
    if (configReport.issues.length > 0) {
      console.error('🚨 Storage Configuration Issues Detected:');
      configReport.issues.forEach(issue => console.error(`  ❌ ${issue}`));
      
      console.log('🔧 Recommended Actions:');
      configReport.recommendations.forEach(rec => console.log(`  💡 ${rec}`));
    } else {
      console.log('✅ Storage configuration appears correct for this environment');
    }
    
    // For production, configuration must be valid
    if (configReport.environment === 'Production') {
      expect(configReport.configurationValid).toBe(true);
    }
  });
});

// Helper test for creating test user (run separately)
test.describe.skip('Test Setup', () => {
  
  test('create test user', async ({ page }) => {
    await page.goto(`${config.baseUrl}/register`);
    
    // Fill registration form
    await page.fill('input[name="email"]', config.testUser.email);
    await page.fill('input[name="password"]', config.testUser.password);
    await page.fill('input[name="confirmPassword"]', config.testUser.password);
    
    await page.click('button[type="submit"]');
    
    // Verify registration success
    await page.waitForSelector('[data-testid="registration-success"]');
    
    console.log('✅ Test user created successfully');
  });
});
