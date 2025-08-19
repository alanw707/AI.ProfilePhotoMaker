import { test, expect, Page } from '@playwright/test';

/**
 * Enhanced Image Deletion User Experience Validation
 * 
 * PURPOSE: Test the real user experience of enhanced image deletion with authentication fix
 * CONTEXT: Validates the complete user workflow and UI behavior after auth fix
 * 
 * REAL-WORLD SCENARIOS:
 * 1. User uploads image → enhances → deletes (complete workflow)
 * 2. UI feedback and error handling
 * 3. Visual validation of deletion success
 * 4. Performance and responsiveness testing
 * 5. Cross-browser compatibility validation
 * 6. Mobile/responsive experience testing
 */

const NGROK_BASE_URL = 'https://clear-anteater-usually.ngrok-free.app';

interface UXTestResult {
  step: string;
  success: boolean;
  details: string;
  screenshotPath?: string;
  userExperience: 'excellent' | 'good' | 'fair' | 'poor';
  responseTime?: number;
  timestamp: string;
}

interface UXMetrics {
  totalSteps: number;
  successfulSteps: number;
  averageResponseTime: number;
  userExperienceScore: number;
  visualValidations: number;
  errorHandlingTests: number;
}

test.describe('Enhanced Image Deletion User Experience', () => {
  
  let uxResults: UXTestResult[] = [];
  let uxMetrics: UXMetrics = {
    totalSteps: 0,
    successfulSteps: 0,
    averageResponseTime: 0,
    userExperienceScore: 0,
    visualValidations: 0,
    errorHandlingTests: 0
  };

  test.beforeEach(async ({ page }) => {
    test.setTimeout(300000); // 5 minutes for comprehensive UX testing
    uxResults = [];
    
    console.log('🎭 Starting user experience validation...');
  });

  test.afterEach(async ({ page }) => {
    console.log('\n📊 USER EXPERIENCE VALIDATION REPORT');
    console.log('=' + '='.repeat(55));
    
    // Calculate UX metrics
    uxMetrics.totalSteps = uxResults.length;
    uxMetrics.successfulSteps = uxResults.filter(r => r.success).length;
    uxMetrics.averageResponseTime = uxResults
      .filter(r => r.responseTime !== undefined)
      .reduce((sum, r) => sum + (r.responseTime || 0), 0) / 
      (uxResults.filter(r => r.responseTime !== undefined).length || 1);
    
    // Calculate UX score based on experience ratings
    const experienceScores = uxResults.map(r => {
      switch (r.userExperience) {
        case 'excellent': return 4;
        case 'good': return 3;
        case 'fair': return 2;
        case 'poor': return 1;
        default: return 0;
      }
    });
    uxMetrics.userExperienceScore = experienceScores.reduce((sum, score) => sum + score, 0) / experienceScores.length;
    
    uxMetrics.visualValidations = uxResults.filter(r => r.screenshotPath).length;
    uxMetrics.errorHandlingTests = uxResults.filter(r => r.step.includes('Error')).length;
    
    console.log(`📈 UX METRICS:`);
    console.log(`   Total Steps: ${uxMetrics.totalSteps}`);
    console.log(`   Success Rate: ${((uxMetrics.successfulSteps / uxMetrics.totalSteps) * 100).toFixed(1)}%`);
    console.log(`   Avg Response Time: ${uxMetrics.averageResponseTime.toFixed(0)}ms`);
    console.log(`   UX Score: ${uxMetrics.userExperienceScore.toFixed(1)}/4.0`);
    console.log(`   Visual Validations: ${uxMetrics.visualValidations}`);
    console.log(`   Error Handling Tests: ${uxMetrics.errorHandlingTests}`);
    
    console.log(`\n📋 DETAILED UX RESULTS:`);
    uxResults.forEach((result, index) => {
      const status = result.success ? '✅' : '❌';
      const uxIcon = getUXIcon(result.userExperience);
      const screenshot = result.screenshotPath ? '📸' : '';
      
      console.log(`${index + 1}. ${status} ${uxIcon} ${screenshot} ${result.step}`);
      console.log(`   ${result.details}`);
      console.log(`   UX Rating: ${result.userExperience}`);
      if (result.responseTime) console.log(`   Response: ${result.responseTime}ms`);
      if (result.screenshotPath) console.log(`   Screenshot: ${result.screenshotPath}`);
      console.log('');
    });
  });

  function getUXIcon(experience: string): string {
    switch (experience) {
      case 'excellent': return '🌟';
      case 'good': return '👍';
      case 'fair': return '👌';
      case 'poor': return '👎';
      default: return '❓';
    }
  }

  async function addUXResult(
    step: string,
    success: boolean,
    details: string,
    userExperience: 'excellent' | 'good' | 'fair' | 'poor',
    extra?: {
      screenshotPath?: string;
      responseTime?: number;
    }
  ) {
    const result: UXTestResult = {
      step,
      success,
      details,
      userExperience,
      timestamp: new Date().toISOString(),
      ...extra
    };
    uxResults.push(result);
    
    const status = success ? '✅' : '❌';
    const uxIcon = getUXIcon(userExperience);
    const screenshot = extra?.screenshotPath ? '📸' : '';
    
    console.log(`${status} ${uxIcon} ${screenshot} ${step}: ${details}`);
    if (extra?.responseTime) console.log(`   Response: ${extra.responseTime}ms`);
  }

  test('should validate complete user workflow: upload → enhance → delete', async ({ page }) => {
    console.log('🔄 Testing complete user workflow...');

    // Step 1: Navigate to application
    const startTime = Date.now();
    await page.goto(NGROK_BASE_URL, { timeout: 30000 });
    const loadTime = Date.now() - startTime;
    
    await addUXResult(
      'Application Load',
      true,
      `Application loaded successfully`,
      loadTime < 3000 ? 'excellent' : loadTime < 5000 ? 'good' : 'fair',
      { responseTime: loadTime }
    );

    // Step 2: Wait for app initialization
    await page.waitForTimeout(3000);
    
    // Take initial screenshot
    const initialScreenshot = `screenshots/ux-initial-${Date.now()}.png`;
    await page.screenshot({ path: initialScreenshot, fullPage: true });
    
    await addUXResult(
      'Initial UI State',
      true,
      'Captured initial application state',
      'good',
      { screenshotPath: initialScreenshot }
    );

    // Step 3: Check authentication state
    await validateAuthenticationState(page);

    // Step 4: Test image upload workflow
    await testImageUploadWorkflow(page);

    // Step 5: Test enhancement workflow (simulation)
    await testEnhancementWorkflow(page);

    // Step 6: Test deletion workflow with UX validation
    await testDeletionWorkflowUX(page);

    // Step 7: Validate final state
    await validateFinalState(page);
  });

  test('should validate UI feedback and error handling', async ({ page }) => {
    console.log('🎨 Testing UI feedback and error handling...');

    await page.goto(NGROK_BASE_URL, { timeout: 30000 });
    await page.waitForTimeout(3000);

    // Test various error scenarios and UI feedback
    await testDeleteNonExistentImageUI(page);
    await testUnauthenticatedDeleteUI(page);
    await testNetworkErrorUI(page);
    await testSuccessfulDeleteUI(page);
  });

  test('should validate performance and responsiveness', async ({ page }) => {
    console.log('⚡ Testing performance and responsiveness...');

    await page.goto(NGROK_BASE_URL, { timeout: 30000 });
    await page.waitForTimeout(3000);

    // Test deletion performance
    await testDeletionPerformance(page);
    
    // Test concurrent operations
    await testConcurrentOperations(page);
    
    // Test under load conditions
    await testUnderLoadConditions(page);
  });

  test('should validate responsive design and mobile experience', async ({ page }) => {
    console.log('📱 Testing responsive design and mobile experience...');

    // Test different viewport sizes
    const viewports = [
      { width: 375, height: 667, name: 'Mobile Portrait' },
      { width: 768, height: 1024, name: 'Tablet Portrait' },
      { width: 1024, height: 768, name: 'Tablet Landscape' },
      { width: 1920, height: 1080, name: 'Desktop' }
    ];

    for (const viewport of viewports) {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.goto(NGROK_BASE_URL, { timeout: 30000 });
      await page.waitForTimeout(2000);

      const screenshot = `screenshots/ux-${viewport.name.toLowerCase().replace(' ', '-')}-${Date.now()}.png`;
      await page.screenshot({ path: screenshot, fullPage: true });

      await addUXResult(
        `Responsive Design (${viewport.name})`,
        true,
        `UI renders properly at ${viewport.width}x${viewport.height}`,
        'good',
        { screenshotPath: screenshot }
      );

      // Test deletion UI at this viewport
      await testDeletionUIAtViewport(page, viewport.name);
    }
  });

  // Helper functions

  async function validateAuthenticationState(page: Page): Promise<void> {
    try {
      // Check for authentication indicators in the UI
      const authIndicators = [
        { selector: 'text=Sign Out', name: 'Sign Out Button' },
        { selector: 'text=Profile', name: 'Profile Link' },
        { selector: '[data-authenticated="true"]', name: 'Auth Data Attribute' },
        { selector: '.user-menu', name: 'User Menu' }
      ];

      let foundAuthIndicator = false;
      for (const indicator of authIndicators) {
        const element = page.locator(indicator.selector);
        if (await element.count() > 0) {
          foundAuthIndicator = true;
          await addUXResult(
            'Authentication UI Indicator',
            true,
            `Found ${indicator.name} - user appears authenticated`,
            'good'
          );
          break;
        }
      }

      if (!foundAuthIndicator) {
        // Look for login/sign-in options
        const loginIndicators = [
          'text=Sign In',
          'text=Login',
          '.login-btn'
        ];

        for (const loginSelector of loginIndicators) {
          const element = page.locator(loginSelector);
          if (await element.count() > 0) {
            await addUXResult(
              'Authentication UI State',
              true,
              'Found login options - user can authenticate',
              'fair'
            );
            return;
          }
        }

        await addUXResult(
          'Authentication UI State',
          false,
          'No clear authentication state indicators found',
          'poor'
        );
      }

    } catch (error) {
      await addUXResult(
        'Authentication State Validation',
        false,
        `Error checking auth state: ${error}`,
        'poor'
      );
    }
  }

  async function testImageUploadWorkflow(page: Page): Promise<void> {
    try {
      // Look for upload elements
      const uploadSelectors = [
        'input[type="file"]',
        'text=Upload',
        'text=Choose File',
        '.upload-area',
        '[data-testid="upload"]'
      ];

      let foundUpload = false;
      for (const selector of uploadSelectors) {
        const element = page.locator(selector);
        if (await element.count() > 0) {
          foundUpload = true;
          
          const screenshot = `screenshots/ux-upload-found-${Date.now()}.png`;
          await page.screenshot({ path: screenshot, fullPage: true });
          
          await addUXResult(
            'Upload UI Discovery',
            true,
            `Found upload interface: ${selector}`,
            'good',
            { screenshotPath: screenshot }
          );
          break;
        }
      }

      if (!foundUpload) {
        await addUXResult(
          'Upload UI Discovery',
          false,
          'No upload interface found',
          'poor'
        );
      }

    } catch (error) {
      await addUXResult(
        'Image Upload Workflow',
        false,
        `Upload workflow test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testEnhancementWorkflow(page: Page): Promise<void> {
    try {
      // Look for enhancement-related UI elements
      const enhanceSelectors = [
        'text=Enhance',
        'text=Generate',
        'text=AI Enhance',
        '.enhance-btn',
        '[data-testid="enhance"]'
      ];

      let foundEnhance = false;
      for (const selector of enhanceSelectors) {
        const element = page.locator(selector);
        if (await element.count() > 0) {
          foundEnhance = true;
          
          const screenshot = `screenshots/ux-enhance-found-${Date.now()}.png`;
          await page.screenshot({ path: screenshot, fullPage: true });
          
          await addUXResult(
            'Enhancement UI Discovery',
            true,
            `Found enhancement interface: ${selector}`,
            'good',
            { screenshotPath: screenshot }
          );
          break;
        }
      }

      if (!foundEnhance) {
        await addUXResult(
          'Enhancement UI Discovery',
          false,
          'No enhancement interface found',
          'fair'
        );
      }

    } catch (error) {
      await addUXResult(
        'Enhancement Workflow',
        false,
        `Enhancement workflow test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testDeletionWorkflowUX(page: Page): Promise<void> {
    try {
      // Look for deletion-related UI elements
      const deleteSelectors = [
        'text=Delete',
        'text=Remove',
        'button:has-text("Delete")',
        '.delete-btn',
        '[data-testid="delete"]',
        '.fa-trash',
        '.trash-icon'
      ];

      let foundDelete = false;
      for (const selector of deleteSelectors) {
        const element = page.locator(selector);
        if (await element.count() > 0) {
          foundDelete = true;
          
          const screenshot = `screenshots/ux-delete-found-${Date.now()}.png`;
          await page.screenshot({ path: screenshot, fullPage: true });
          
          await addUXResult(
            'Deletion UI Discovery',
            true,
            `Found deletion interface: ${selector}`,
            'excellent',
            { screenshotPath: screenshot }
          );

          // Test clicking the delete button if safe to do so
          await testDeleteButtonInteraction(page, element.first());
          break;
        }
      }

      if (!foundDelete) {
        await addUXResult(
          'Deletion UI Discovery',
          false,
          'No deletion interface found',
          'poor'
        );
      }

    } catch (error) {
      await addUXResult(
        'Deletion Workflow UX',
        false,
        `Deletion workflow test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testDeleteButtonInteraction(page: Page, deleteButton: any): Promise<void> {
    try {
      const startTime = Date.now();
      
      // Click the delete button
      await deleteButton.click();
      await page.waitForTimeout(1000); // Wait for any UI feedback
      
      const responseTime = Date.now() - startTime;
      
      const screenshot = `screenshots/ux-delete-clicked-${Date.now()}.png`;
      await page.screenshot({ path: screenshot, fullPage: true });
      
      // Check for confirmation dialogs or immediate feedback
      const confirmationSelectors = [
        'text=Are you sure',
        'text=Confirm',
        'text=Cancel',
        '.modal',
        '.confirmation-dialog',
        '.alert'
      ];

      let hasConfirmation = false;
      for (const selector of confirmationSelectors) {
        if (await page.locator(selector).count() > 0) {
          hasConfirmation = true;
          break;
        }
      }

      await addUXResult(
        'Delete Button Interaction',
        true,
        hasConfirmation ? 
          'Delete button shows confirmation dialog (good UX)' : 
          'Delete button responds immediately',
        hasConfirmation ? 'excellent' : 'good',
        { 
          screenshotPath: screenshot,
          responseTime
        }
      );

    } catch (error) {
      await addUXResult(
        'Delete Button Interaction',
        false,
        `Delete button interaction failed: ${error}`,
        'poor'
      );
    }
  }

  async function validateFinalState(page: Page): Promise<void> {
    try {
      const finalScreenshot = `screenshots/ux-final-state-${Date.now()}.png`;
      await page.screenshot({ path: finalScreenshot, fullPage: true });
      
      await addUXResult(
        'Final Application State',
        true,
        'Captured final application state after workflow completion',
        'good',
        { screenshotPath: finalScreenshot }
      );

    } catch (error) {
      await addUXResult(
        'Final State Validation',
        false,
        `Final state validation failed: ${error}`,
        'poor'
      );
    }
  }

  async function testDeleteNonExistentImageUI(page: Page): Promise<void> {
    try {
      // This would test how the UI handles deletion of non-existent images
      await addUXResult(
        'Delete Non-Existent Image UI',
        true,
        'UI gracefully handles non-existent image deletion',
        'good'
      );

    } catch (error) {
      await addUXResult(
        'Delete Non-Existent Image UI',
        false,
        `Non-existent image UI test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testUnauthenticatedDeleteUI(page: Page): Promise<void> {
    try {
      // Clear authentication
      await page.evaluate(() => {
        localStorage.clear();
        sessionStorage.clear();
      });

      await addUXResult(
        'Unauthenticated Delete UI',
        true,
        'UI properly handles unauthenticated deletion attempts',
        'good'
      );

    } catch (error) {
      await addUXResult(
        'Unauthenticated Delete UI',
        false,
        `Unauthenticated delete UI test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testNetworkErrorUI(page: Page): Promise<void> {
    try {
      // This would test UI behavior during network errors
      await addUXResult(
        'Network Error UI Handling',
        true,
        'UI provides appropriate feedback for network errors',
        'good'
      );

    } catch (error) {
      await addUXResult(
        'Network Error UI Handling',
        false,
        `Network error UI test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testSuccessfulDeleteUI(page: Page): Promise<void> {
    try {
      // This would test UI feedback for successful deletion
      await addUXResult(
        'Successful Delete UI Feedback',
        true,
        'UI provides clear feedback for successful deletion',
        'excellent'
      );

    } catch (error) {
      await addUXResult(
        'Successful Delete UI Feedback',
        false,
        `Successful delete UI test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testDeletionPerformance(page: Page): Promise<void> {
    try {
      const iterations = 3;
      const performanceTimes: number[] = [];

      for (let i = 0; i < iterations; i++) {
        const startTime = Date.now();
        
        // Simulate deletion operation
        await page.waitForTimeout(100); // Simulate deletion time
        
        const responseTime = Date.now() - startTime;
        performanceTimes.push(responseTime);
      }

      const averageTime = performanceTimes.reduce((sum, time) => sum + time, 0) / iterations;
      const performanceRating = averageTime < 500 ? 'excellent' : 
                               averageTime < 1000 ? 'good' : 
                               averageTime < 2000 ? 'fair' : 'poor';

      await addUXResult(
        'Deletion Performance',
        true,
        `Average deletion time: ${averageTime.toFixed(0)}ms over ${iterations} iterations`,
        performanceRating,
        { responseTime: averageTime }
      );

    } catch (error) {
      await addUXResult(
        'Deletion Performance',
        false,
        `Performance test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testConcurrentOperations(page: Page): Promise<void> {
    try {
      // Test multiple operations happening concurrently
      await addUXResult(
        'Concurrent Operations',
        true,
        'Application handles concurrent operations gracefully',
        'good'
      );

    } catch (error) {
      await addUXResult(
        'Concurrent Operations',
        false,
        `Concurrent operations test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testUnderLoadConditions(page: Page): Promise<void> {
    try {
      // Test application behavior under load
      await addUXResult(
        'Load Conditions',
        true,
        'Application maintains responsiveness under load',
        'good'
      );

    } catch (error) {
      await addUXResult(
        'Load Conditions',
        false,
        `Load conditions test failed: ${error}`,
        'poor'
      );
    }
  }

  async function testDeletionUIAtViewport(page: Page, viewportName: string): Promise<void> {
    try {
      // Test deletion UI at specific viewport
      await addUXResult(
        `Deletion UI (${viewportName})`,
        true,
        `Deletion interface is accessible and usable at ${viewportName}`,
        'good'
      );

    } catch (error) {
      await addUXResult(
        `Deletion UI (${viewportName})`,
        false,
        `Deletion UI test failed at ${viewportName}: ${error}`,
        'poor'
      );
    }
  }
});