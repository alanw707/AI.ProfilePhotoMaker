import { test, expect, Page } from '@playwright/test';
import { testImageAccessibility, makeRequestWithMetrics, captureTimestampedScreenshot } from './test-helpers';

/**
 * Enhanced Image Deletion Diagnostic Test Suite
 * 
 * PURPOSE: Systematic diagnosis of enhanced image deletion issues
 * CONTEXT: Test the full workflow from upload → enhancement → deletion
 * 
 * TEST SCENARIOS:
 * 1. Check existing enhanced images in blob storage
 * 2. Upload image and generate enhanced version
 * 3. Test DELETE /api/image/enhanced/{fileName} endpoint directly 
 * 4. Test different scenarios (valid/invalid filenames, authentication)
 * 5. Test actual user workflow via browser automation
 * 6. Trace the entire deletion flow with detailed logging
 * 7. Check backend logs and storage state
 */

const NGROK_BASE_URL = 'https://clear-anteater-usually.ngrok-free.app';
const API_BASE_URL = `${NGROK_BASE_URL}/api`;

// Test data for enhanced image operations
const TEST_IMAGE_DATA = {
  enhancedFileName: 'test_enhanced.png',
  invalidFileName: '../../../malicious.png',
  nonExistentFileName: 'does_not_exist_enhanced.png',
  emptyFileName: '',
  pathTraversalFileName: '..\\..\\system32\\test_enhanced.png'
};

interface DiagnosticResult {
  step: string;
  success: boolean;
  details: string;
  responseTime?: number;
  statusCode?: number;
  errorDetails?: string;
  timestamp: string;
}

interface EnhancedImageInfo {
  fileName: string;
  url: string;
  exists: boolean;
  accessible: boolean;
  storageCheck?: boolean;
}

test.describe('Enhanced Image Deletion Diagnostic Suite', () => {
  
  let diagnosticResults: DiagnosticResult[] = [];
  let enhancedImages: EnhancedImageInfo[] = [];
  let authToken: string | null = null;
  let currentUserId: string | null = null;

  test.beforeEach(async ({ page }) => {
    test.setTimeout(180000); // 3 minutes for comprehensive tests
    diagnosticResults = [];
    enhancedImages = [];
    
    console.log('🔍 Starting Enhanced Image Deletion Diagnostic...');
    await addDiagnosticResult('Test Suite Initialization', true, 'Diagnostic suite started');
  });

  test.afterEach(async ({ page }) => {
    // Generate diagnostic report
    console.log('\n📊 DIAGNOSTIC REPORT SUMMARY');
    console.log('=' + '='.repeat(50));
    
    const totalSteps = diagnosticResults.length;
    const successfulSteps = diagnosticResults.filter(r => r.success).length;
    const failedSteps = totalSteps - successfulSteps;
    
    console.log(`Total diagnostic steps: ${totalSteps}`);
    console.log(`Successful: ${successfulSteps}`);
    console.log(`Failed: ${failedSteps}`);
    console.log(`Success rate: ${((successfulSteps / totalSteps) * 100).toFixed(1)}%`);
    
    console.log('\n📋 STEP-BY-STEP RESULTS:');
    diagnosticResults.forEach((result, index) => {
      const status = result.success ? '✅' : '❌';
      console.log(`${index + 1}. ${status} ${result.step}`);
      console.log(`   Details: ${result.details}`);
      if (result.statusCode) console.log(`   Status: ${result.statusCode}`);
      if (result.responseTime) console.log(`   Time: ${result.responseTime}ms`);
      if (result.errorDetails) console.log(`   Error: ${result.errorDetails}`);
      console.log(`   Timestamp: ${result.timestamp}`);
      console.log('');
    });

    if (enhancedImages.length > 0) {
      console.log('\n🖼️  ENHANCED IMAGES DISCOVERED:');
      enhancedImages.forEach((img, index) => {
        console.log(`${index + 1}. File: ${img.fileName}`);
        console.log(`   URL: ${img.url}`);
        console.log(`   Exists: ${img.exists ? 'Yes' : 'No'}`);
        console.log(`   Accessible: ${img.accessible ? 'Yes' : 'No'}`);
        if (img.storageCheck !== undefined) {
          console.log(`   Storage Check: ${img.storageCheck ? 'Pass' : 'Fail'}`);
        }
        console.log('');
      });
    }
  });

  // Helper function to add diagnostic results
  async function addDiagnosticResult(step: string, success: boolean, details: string, extra?: {
    responseTime?: number;
    statusCode?: number;
    errorDetails?: string;
  }) {
    const result: DiagnosticResult = {
      step,
      success,
      details,
      timestamp: new Date().toISOString(),
      ...extra
    };
    diagnosticResults.push(result);
    
    const status = success ? '✅' : '❌';
    console.log(`${status} ${step}: ${details}`);
    if (extra?.statusCode) console.log(`   HTTP ${extra.statusCode}`);
    if (extra?.responseTime) console.log(`   ${extra.responseTime}ms`);
    if (extra?.errorDetails) console.log(`   Error: ${extra.errorDetails}`);
  }

  test('should perform comprehensive enhanced image deletion diagnostics', async ({ page }) => {
    console.log('🚀 Starting comprehensive enhanced image deletion diagnostics...');

    // Step 1: Test Application Accessibility
    console.log('\n1️⃣ Testing application accessibility...');
    try {
      const response = await page.goto(NGROK_BASE_URL, { timeout: 30000 });
      const accessible = response?.status() === 200;
      await addDiagnosticResult(
        'Application Accessibility', 
        accessible, 
        `Application response: HTTP ${response?.status()}`,
        { statusCode: response?.status() }
      );
      
      if (!accessible) {
        throw new Error(`Application not accessible: HTTP ${response?.status()}`);
      }
    } catch (error) {
      await addDiagnosticResult(
        'Application Accessibility', 
        false, 
        'Failed to access application',
        { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
      );
      return; // Stop if app is not accessible
    }

    // Step 2: Check Authentication Status
    console.log('\n2️⃣ Checking authentication status...');
    try {
      // Try to access the API to check if we need authentication
      const healthResponse = await page.request.get(`${API_BASE_URL}/health`);
      await addDiagnosticResult(
        'Health Check',
        healthResponse.ok(),
        `Health endpoint response: HTTP ${healthResponse.status()}`,
        { statusCode: healthResponse.status() }
      );

      // Try to access a protected endpoint to check auth status
      const profileResponse = await page.request.get(`${API_BASE_URL}/profile`);
      const isAuthenticated = profileResponse.status() !== 401;
      
      await addDiagnosticResult(
        'Authentication Status',
        true,
        `Authentication required: ${!isAuthenticated}`,
        { statusCode: profileResponse.status() }
      );

      if (isAuthenticated) {
        try {
          const profileData = await profileResponse.json();
          currentUserId = profileData.id || profileData.userId;
          console.log(`   Authenticated user ID: ${currentUserId}`);
        } catch {
          console.log('   Authenticated but could not parse user data');
        }
      }
    } catch (error) {
      await addDiagnosticResult(
        'Authentication Check',
        false,
        'Failed to check authentication status',
        { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
      );
    }

    // Step 3: Discover Existing Enhanced Images
    console.log('\n3️⃣ Discovering existing enhanced images...');
    await discoverEnhancedImages(page);

    // Step 4: Test Enhanced Image Deletion Endpoint - Valid Cases
    console.log('\n4️⃣ Testing enhanced image deletion endpoint - Valid cases...');
    await testValidDeletionScenarios(page);

    // Step 5: Test Enhanced Image Deletion Endpoint - Invalid Cases
    console.log('\n5️⃣ Testing enhanced image deletion endpoint - Invalid cases...');
    await testInvalidDeletionScenarios(page);

    // Step 6: Test Deletion with Storage Verification
    console.log('\n6️⃣ Testing deletion with storage verification...');
    await testDeletionWithStorageVerification(page);

    // Step 7: Test Browser-Based User Workflow
    console.log('\n7️⃣ Testing browser-based user workflow...');
    await testBrowserBasedDeletionWorkflow(page);

    // Step 8: Check Backend Logs and Health
    console.log('\n8️⃣ Checking backend health and logging...');
    await checkBackendHealthAndLogs(page);

    // Step 9: Generate Final Assessment
    console.log('\n9️⃣ Generating final assessment...');
    await generateFinalAssessment();
  });

  async function discoverEnhancedImages(page: Page) {
    try {
      // Try to list enhanced images through various methods
      
      // Method 1: Check common enhanced image URLs
      const commonEnhancedPaths = [
        '/devstoreaccount1/profile-images/generated/test-user/image_enhanced.png',
        '/devstoreaccount1/profile-images/generated/sample-user/photo_enhanced.jpg',
        '/devstoreaccount1/profile-images/generated/user-123/profile_enhanced.png'
      ];

      for (const path of commonEnhancedPaths) {
        const fullUrl = `${NGROK_BASE_URL}${path}`;
        try {
          const response = await page.request.get(fullUrl);
          const exists = response.status() === 200;
          
          const imageInfo: EnhancedImageInfo = {
            fileName: path.split('/').pop() || '',
            url: fullUrl,
            exists,
            accessible: exists
          };
          
          if (exists) {
            enhancedImages.push(imageInfo);
            console.log(`   Found enhanced image: ${imageInfo.fileName}`);
          }
        } catch (error) {
          console.log(`   Error checking ${path}: ${error}`);
        }
      }

      // Method 2: Try to access user-specific endpoints to find enhanced images
      if (currentUserId) {
        try {
          const imagesResponse = await page.request.get(`${API_BASE_URL}/images`);
          if (imagesResponse.ok()) {
            const imagesData = await imagesResponse.json();
            console.log(`   Retrieved images data for user ${currentUserId}`);
            
            // Look for enhanced images in the response
            if (Array.isArray(imagesData)) {
              const enhancedInData = imagesData.filter((img: any) => 
                img.url && img.url.includes('_enhanced')
              );
              
              enhancedInData.forEach((img: any) => {
                enhancedImages.push({
                  fileName: img.fileName || img.url.split('/').pop() || '',
                  url: img.url,
                  exists: true,
                  accessible: true
                });
              });
            }
          }
        } catch (error) {
          console.log(`   Could not retrieve user images: ${error}`);
        }
      }

      await addDiagnosticResult(
        'Enhanced Image Discovery',
        true,
        `Found ${enhancedImages.length} enhanced images`
      );

    } catch (error) {
      await addDiagnosticResult(
        'Enhanced Image Discovery',
        false,
        'Failed to discover enhanced images',
        { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
      );
    }
  }

  async function testValidDeletionScenarios(page: Page) {
    const validTestCases = [
      {
        name: 'Existing Enhanced Image',
        fileName: enhancedImages.length > 0 ? enhancedImages[0].fileName : 'test_enhanced.png',
        expectedStatus: enhancedImages.length > 0 ? 200 : 404
      },
      {
        name: 'Well-formed Filename',
        fileName: 'user_photo_enhanced.png',
        expectedStatus: 404 // Likely doesn't exist, but should handle gracefully
      }
    ];

    for (const testCase of validTestCases) {
      try {
        const startTime = Date.now();
        const response = await page.request.delete(`${API_BASE_URL}/image/enhanced/${testCase.fileName}`);
        const responseTime = Date.now() - startTime;
        
        let responseBody = '';
        try {
          responseBody = await response.text();
        } catch {
          responseBody = 'Could not read response body';
        }

        const success = response.status() === testCase.expectedStatus;
        
        await addDiagnosticResult(
          `Delete ${testCase.name}`,
          success,
          `Expected ${testCase.expectedStatus}, got ${response.status()}. Response: ${responseBody.substring(0, 100)}`,
          { 
            statusCode: response.status(), 
            responseTime,
            errorDetails: !success ? `Expected ${testCase.expectedStatus}, got ${response.status()}` : undefined
          }
        );

        // If deletion was successful, verify the image is gone
        if (response.status() === 200) {
          await verifyImageDeleted(page, testCase.fileName);
        }

      } catch (error) {
        await addDiagnosticResult(
          `Delete ${testCase.name}`,
          false,
          'Request failed',
          { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
        );
      }
    }
  }

  async function testInvalidDeletionScenarios(page: Page) {
    const invalidTestCases = [
      {
        name: 'Empty Filename',
        fileName: TEST_IMAGE_DATA.emptyFileName,
        expectedStatus: 404 // Route likely won't match
      },
      {
        name: 'Path Traversal Attack',
        fileName: TEST_IMAGE_DATA.invalidFileName,
        expectedStatus: 400 // Should be rejected by validation
      },
      {
        name: 'Windows Path Traversal',
        fileName: TEST_IMAGE_DATA.pathTraversalFileName,
        expectedStatus: 400 // Should be rejected by validation
      },
      {
        name: 'Non-existent File',
        fileName: TEST_IMAGE_DATA.nonExistentFileName,
        expectedStatus: 404 // File doesn't exist
      }
    ];

    for (const testCase of invalidTestCases) {
      try {
        const startTime = Date.now();
        let response;
        
        if (testCase.fileName === '') {
          // Special case for empty filename - test the route
          response = await page.request.delete(`${API_BASE_URL}/image/enhanced/`);
        } else {
          response = await page.request.delete(`${API_BASE_URL}/image/enhanced/${encodeURIComponent(testCase.fileName)}`);
        }
        
        const responseTime = Date.now() - startTime;
        
        let responseBody = '';
        try {
          responseBody = await response.text();
        } catch {
          responseBody = 'Could not read response body';
        }

        const success = response.status() === testCase.expectedStatus || 
                        response.status() === 400 || 
                        response.status() === 404; // Any of these are acceptable for invalid requests
        
        await addDiagnosticResult(
          `Invalid Delete ${testCase.name}`,
          success,
          `Status ${response.status()}, Response: ${responseBody.substring(0, 100)}`,
          { 
            statusCode: response.status(), 
            responseTime,
            errorDetails: !success ? `Unexpected status ${response.status()}` : undefined
          }
        );

      } catch (error) {
        await addDiagnosticResult(
          `Invalid Delete ${testCase.name}`,
          false,
          'Request failed',
          { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
        );
      }
    }
  }

  async function testDeletionWithStorageVerification(page: Page) {
    // This test verifies that deletion actually removes files from storage
    if (enhancedImages.length === 0) {
      await addDiagnosticResult(
        'Storage Verification Test',
        false,
        'No enhanced images available for storage verification test'
      );
      return;
    }

    const testImage = enhancedImages[0];
    
    try {
      // Step 1: Verify image exists before deletion
      const beforeResponse = await page.request.get(testImage.url);
      const existsBeforeDeletion = beforeResponse.status() === 200;
      
      await addDiagnosticResult(
        'Pre-deletion Storage Check',
        existsBeforeDeletion,
        `Image ${testImage.fileName} exists before deletion: ${existsBeforeDeletion}`,
        { statusCode: beforeResponse.status() }
      );

      if (!existsBeforeDeletion) {
        console.log('   Image does not exist before deletion - skipping storage verification');
        return;
      }

      // Step 2: Perform deletion
      const deleteResponse = await page.request.delete(`${API_BASE_URL}/image/enhanced/${testImage.fileName}`);
      const deletionSuccessful = deleteResponse.status() === 200;
      
      await addDiagnosticResult(
        'Deletion Request',
        deletionSuccessful,
        `Deletion request for ${testImage.fileName}`,
        { statusCode: deleteResponse.status() }
      );

      // Step 3: Verify image no longer exists after deletion
      if (deletionSuccessful) {
        // Wait a moment for the deletion to propagate
        await page.waitForTimeout(2000);
        
        const afterResponse = await page.request.get(testImage.url);
        const existsAfterDeletion = afterResponse.status() === 200;
        
        await addDiagnosticResult(
          'Post-deletion Storage Check',
          !existsAfterDeletion,
          `Image ${testImage.fileName} exists after deletion: ${existsAfterDeletion}`,
          { 
            statusCode: afterResponse.status(),
            errorDetails: existsAfterDeletion ? 'Image still exists after deletion' : undefined
          }
        );

        // Update the image info
        testImage.exists = existsAfterDeletion;
        testImage.accessible = existsAfterDeletion;
        testImage.storageCheck = !existsAfterDeletion;
      }

    } catch (error) {
      await addDiagnosticResult(
        'Storage Verification Test',
        false,
        'Storage verification failed',
        { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
      );
    }
  }

  async function testBrowserBasedDeletionWorkflow(page: Page) {
    try {
      // Navigate to the application
      await page.goto(NGROK_BASE_URL, { timeout: 30000 });
      
      // Look for delete buttons or enhanced image management UI
      await page.waitForTimeout(3000); // Allow app to load
      
      // Take a screenshot to see the current state
      const screenshotPath = await captureTimestampedScreenshot(page, 'enhanced-image-ui-state');
      console.log(`   Screenshot saved: ${screenshotPath}`);
      
      // Look for enhanced images in the UI
      const enhancedImageElements = await page.locator('img[src*="_enhanced"], [data-enhanced], .enhanced-image').count();
      
      await addDiagnosticResult(
        'UI Enhanced Images Found',
        true,
        `Found ${enhancedImageElements} enhanced image elements in UI`
      );

      // Look for delete buttons
      const deleteButtons = await page.locator('button:has-text("Delete"), button:has-text("Remove"), .delete-btn, [data-action="delete"]').count();
      
      await addDiagnosticResult(
        'UI Delete Controls Found',
        true,
        `Found ${deleteButtons} delete control elements in UI`
      );

      // Try to interact with delete functionality if available
      if (deleteButtons > 0) {
        try {
          const firstDeleteButton = page.locator('button:has-text("Delete"), button:has-text("Remove"), .delete-btn, [data-action="delete"]').first();
          
          // Check if button is visible and enabled
          const isVisible = await firstDeleteButton.isVisible();
          const isEnabled = await firstDeleteButton.isEnabled();
          
          await addDiagnosticResult(
            'UI Delete Button State',
            isVisible && isEnabled,
            `Delete button visible: ${isVisible}, enabled: ${isEnabled}`
          );

          if (isVisible && isEnabled) {
            // Monitor network requests during click
            const responsePromise = page.waitForResponse(response => 
              response.url().includes('/api/image/enhanced/') && 
              response.request().method() === 'DELETE'
            );

            await firstDeleteButton.click();
            
            try {
              const networkResponse = await responsePromise;
              await addDiagnosticResult(
                'UI Deletion Network Request',
                true,
                `Delete request triggered: HTTP ${networkResponse.status()}`,
                { statusCode: networkResponse.status() }
              );
            } catch {
              await addDiagnosticResult(
                'UI Deletion Network Request',
                false,
                'No delete network request detected after button click'
              );
            }
          }
        } catch (error) {
          await addDiagnosticResult(
            'UI Delete Interaction',
            false,
            'Failed to interact with delete controls',
            { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
          );
        }
      }

    } catch (error) {
      await addDiagnosticResult(
        'Browser-based Workflow Test',
        false,
        'Failed to test browser-based deletion workflow',
        { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
      );
    }
  }

  async function checkBackendHealthAndLogs(page: Page) {
    try {
      // Check API health
      const healthResponse = await page.request.get(`${API_BASE_URL}/health`);
      const healthData = healthResponse.ok() ? await healthResponse.json() : null;
      
      await addDiagnosticResult(
        'Backend Health Check',
        healthResponse.ok(),
        `Health status: ${healthResponse.status()}`,
        { statusCode: healthResponse.status() }
      );

      // Check storage health if available
      if (healthData && healthData.storage) {
        await addDiagnosticResult(
          'Storage Health Check',
          healthData.storage.status === 'Healthy',
          `Storage status: ${healthData.storage.status}`
        );
      }

      // Try to get some basic metrics or info
      const infoEndpoints = [
        '/api/info',
        '/api/status',
        '/api/metrics'
      ];

      for (const endpoint of infoEndpoints) {
        try {
          const response = await page.request.get(`${NGROK_BASE_URL}${endpoint}`);
          if (response.ok()) {
            await addDiagnosticResult(
              `Backend Info (${endpoint})`,
              true,
              `Endpoint accessible: HTTP ${response.status()}`,
              { statusCode: response.status() }
            );
          }
        } catch {
          // Endpoint might not exist, that's fine
        }
      }

    } catch (error) {
      await addDiagnosticResult(
        'Backend Health Check',
        false,
        'Failed to check backend health',
        { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
      );
    }
  }

  async function verifyImageDeleted(page: Page, fileName: string) {
    // Try to access the image directly to verify it's deleted
    const commonPaths = [
      `/devstoreaccount1/profile-images/generated/${currentUserId}/${fileName}`,
      `/devstoreaccount1/profile-images/generated/test-user/${fileName}`,
      `/devstoreaccount1/profile-images/generated/sample-user/${fileName}`
    ];

    for (const path of commonPaths) {
      try {
        const fullUrl = `${NGROK_BASE_URL}${path}`;
        const response = await page.request.get(fullUrl);
        const stillExists = response.status() === 200;
        
        if (stillExists) {
          await addDiagnosticResult(
            'Post-deletion Verification',
            false,
            `Image ${fileName} still accessible at ${path}`,
            { statusCode: response.status() }
          );
          return;
        }
      } catch {
        // Error accessing means likely deleted, which is good
      }
    }

    await addDiagnosticResult(
      'Post-deletion Verification',
      true,
      `Image ${fileName} no longer accessible`
    );
  }

  async function generateFinalAssessment() {
    const totalSteps = diagnosticResults.length;
    const successfulSteps = diagnosticResults.filter(r => r.success).length;
    const criticalFailures = diagnosticResults.filter(r => 
      !r.success && (
        r.step.includes('Application Accessibility') ||
        r.step.includes('Delete') && r.step.includes('Enhanced') ||
        r.step.includes('Storage')
      )
    );

    const assessment = {
      overallHealth: (successfulSteps / totalSteps) >= 0.7,
      deletionFunctionality: !criticalFailures.some(f => f.step.includes('Delete')),
      storageIntegration: !criticalFailures.some(f => f.step.includes('Storage')),
      criticalIssues: criticalFailures.length,
      recommendations: generateRecommendations(criticalFailures)
    };

    await addDiagnosticResult(
      'Final Assessment',
      assessment.overallHealth,
      `Overall health: ${assessment.overallHealth ? 'Good' : 'Issues detected'}. ` +
      `Deletion functionality: ${assessment.deletionFunctionality ? 'Working' : 'Broken'}. ` +
      `Critical issues: ${assessment.criticalIssues}`
    );

    console.log('\n🎯 FINAL ASSESSMENT:');
    console.log(`Overall Health: ${assessment.overallHealth ? 'GOOD ✅' : 'ISSUES DETECTED ❌'}`);
    console.log(`Deletion Functionality: ${assessment.deletionFunctionality ? 'WORKING ✅' : 'BROKEN ❌'}`);
    console.log(`Storage Integration: ${assessment.storageIntegration ? 'WORKING ✅' : 'ISSUES ❌'}`);
    console.log(`Critical Issues: ${assessment.criticalIssues}`);
    
    if (assessment.recommendations.length > 0) {
      console.log('\n💡 RECOMMENDATIONS:');
      assessment.recommendations.forEach((rec, index) => {
        console.log(`${index + 1}. ${rec}`);
      });
    }
  }

  function generateRecommendations(criticalFailures: DiagnosticResult[]): string[] {
    const recommendations: string[] = [];

    if (criticalFailures.some(f => f.step.includes('Application Accessibility'))) {
      recommendations.push('Check if the application is running and ngrok tunnel is active');
    }

    if (criticalFailures.some(f => f.step.includes('Authentication'))) {
      recommendations.push('Verify authentication configuration and user session management');
    }

    if (criticalFailures.some(f => f.step.includes('Delete') && f.statusCode === 404)) {
      recommendations.push('Check if the deletion endpoint route is correctly configured');
    }

    if (criticalFailures.some(f => f.step.includes('Delete') && f.statusCode === 500)) {
      recommendations.push('Check backend logs for internal server errors during deletion');
    }

    if (criticalFailures.some(f => f.step.includes('Storage'))) {
      recommendations.push('Verify Azure Blob Storage configuration and connectivity');
      recommendations.push('Check if the storage service is properly injected and configured');
    }

    if (criticalFailures.some(f => f.step.includes('Post-deletion') && f.step.includes('still'))) {
      recommendations.push('Investigate storage service DeleteImageAsync implementation');
      recommendations.push('Check blob path construction and container configuration');
    }

    if (recommendations.length === 0) {
      recommendations.push('Review detailed diagnostic logs for specific failure patterns');
    }

    return recommendations;
  }

  test('should test specific enhanced image deletion scenarios with detailed tracing', async ({ page }) => {
    console.log('🔬 Testing specific enhanced image deletion scenarios with detailed tracing...');

    // Test with a known filename pattern that might exist
    const testScenarios = [
      {
        name: 'Standard Enhanced Image Pattern',
        fileName: 'profile_enhanced.png',
        description: 'Test deletion of standard enhanced image filename pattern'
      },
      {
        name: 'User-specific Enhanced Image',
        fileName: `user123_photo_enhanced.jpg`,
        description: 'Test deletion with user-specific filename'
      },
      {
        name: 'Generated Enhanced Image Pattern',
        fileName: 'generated_1234567890_enhanced.png',
        description: 'Test deletion of generated enhanced image pattern'
      }
    ];

    for (const scenario of testScenarios) {
      console.log(`\n🧪 Testing scenario: ${scenario.name}`);
      
      try {
        // Trace the deletion request with detailed logging
        const deleteUrl = `${API_BASE_URL}/image/enhanced/${scenario.fileName}`;
        console.log(`   DELETE URL: ${deleteUrl}`);
        
        // Monitor console logs during the request
        const consoleLogs: string[] = [];
        page.on('console', msg => {
          consoleLogs.push(`${msg.type()}: ${msg.text()}`);
        });

        // Monitor network activity
        const networkRequests: any[] = [];
        page.on('request', request => {
          if (request.url().includes('/api/image/enhanced/')) {
            networkRequests.push({
              method: request.method(),
              url: request.url(),
              headers: request.headers()
            });
          }
        });

        const startTime = Date.now();
        const response = await page.request.delete(deleteUrl);
        const endTime = Date.now();
        const responseTime = endTime - startTime;

        // Get response details
        let responseBody = '';
        let responseJson: any = null;
        try {
          responseBody = await response.text();
          responseJson = JSON.parse(responseBody);
        } catch {
          // Response might not be JSON
        }

        // Log detailed trace information
        console.log(`   Response Status: ${response.status()}`);
        console.log(`   Response Time: ${responseTime}ms`);
        console.log(`   Response Headers: ${JSON.stringify(response.headers(), null, 2)}`);
        console.log(`   Response Body: ${responseBody.substring(0, 500)}${responseBody.length > 500 ? '...' : ''}`);
        
        if (networkRequests.length > 0) {
          console.log(`   Network Requests Captured: ${networkRequests.length}`);
          networkRequests.forEach((req, index) => {
            console.log(`     ${index + 1}. ${req.method} ${req.url}`);
          });
        }

        if (consoleLogs.length > 0) {
          console.log(`   Console Logs: ${consoleLogs.length}`);
          consoleLogs.forEach((log, index) => {
            console.log(`     ${index + 1}. ${log}`);
          });
        }

        // Analyze the response
        let analysis = '';
        if (response.status() === 200) {
          analysis = 'Deletion successful';
        } else if (response.status() === 404) {
          analysis = 'File not found (expected for non-existent files)';
        } else if (response.status() === 400) {
          analysis = 'Bad request (possibly invalid filename)';
        } else if (response.status() === 401 || response.status() === 403) {
          analysis = 'Authentication/authorization issue';
        } else if (response.status() === 500) {
          analysis = 'Server error - check backend logs';
        } else {
          analysis = `Unexpected status code: ${response.status()}`;
        }

        await addDiagnosticResult(
          `Detailed Trace - ${scenario.name}`,
          response.status() === 200 || response.status() === 404, // Both are acceptable
          `${analysis}. ${scenario.description}`,
          { 
            statusCode: response.status(), 
            responseTime,
            errorDetails: response.status() >= 400 ? responseBody : undefined
          }
        );

        // Clean up event listeners
        page.removeAllListeners('console');
        page.removeAllListeners('request');

      } catch (error) {
        await addDiagnosticResult(
          `Detailed Trace - ${scenario.name}`,
          false,
          'Failed to execute detailed trace',
          { errorDetails: error instanceof Error ? error.message : 'Unknown error' }
        );
      }
    }
  });
});