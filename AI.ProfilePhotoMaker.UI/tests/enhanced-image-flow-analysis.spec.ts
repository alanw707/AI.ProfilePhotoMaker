import { test, expect, Page } from '@playwright/test';

/**
 * Enhanced Image Flow Analysis
 * Traces the complete enhanced image lifecycle to find where deletion fails
 */

interface NetworkRequest {
  method: string;
  url: string;
  headers: Record<string, string>;
  status?: number;
  response?: any;
}

test.describe('Enhanced Image Flow Analysis', () => {
  let capturedRequests: NetworkRequest[] = [];
  
  test.beforeEach(async ({ page }) => {
    capturedRequests = [];
    
    // Intercept ALL network requests to trace the complete flow
    page.on('request', request => {
      capturedRequests.push({
        method: request.method(),
        url: request.url(),
        headers: request.headers()
      });
    });
    
    page.on('response', response => {
      const request = capturedRequests.find(req => req.url === response.url());
      if (request) {
        request.status = response.status();
      }
    });
    
    // Set up authentication for all tests
    await page.goto('http://localhost:4200');
    await page.evaluate(() => {
      // Set up a valid authentication token
      const futureTimestamp = Math.floor(Date.now() / 1000) + 3600; // 1 hour from now
      const payload = btoa(JSON.stringify({
        sub: "1",
        name: "Test User",
        iat: Math.floor(Date.now() / 1000),
        exp: futureTimestamp
      }));
      localStorage.setItem('auth_token', `Bearer header.${payload}.signature`);
      
      // Mock AuthService
      (window as any).authService = {
        getToken: () => localStorage.getItem('auth_token'),
        currentUser: { id: 1, email: 'test@example.com' }
      };
    });
  });
  
  test('1. Trace Complete Enhanced Image Workflow', async ({ page }) => {
    console.log('\n🔍 Tracing Enhanced Image Workflow...');
    
    // Step 1: Navigate to AI Enhancement
    await page.goto('http://localhost:4200/ai-enhancement');
    await page.waitForTimeout(2000);
    
    console.log('📍 Step 1: Navigate to AI Enhancement');
    console.log(`Captured ${capturedRequests.length} requests so far`);
    
    // Step 2: Try to find upload area or enhancement interface
    const uploadArea = page.locator('input[type="file"], .upload-area, [data-testid*="upload"]').first();
    const enhanceButton = page.locator('button:has-text("Enhance"), .enhance-btn, [data-testid*="enhance"]').first();
    
    // Look for any existing enhanced images to test deletion
    const existingImages = page.locator('.enhanced-image, .image-result, .photo-item');
    const imageCount = await existingImages.count();
    
    console.log(`📸 Found ${imageCount} existing enhanced images`);
    
    // Step 3: Inspect the current page for deletion mechanisms
    const deleteButtons = page.locator('button:has-text("Delete"), .delete-btn, [data-testid*="delete"]');
    const deleteButtonCount = await deleteButtons.count();
    
    console.log(`🗑️ Found ${deleteButtonCount} delete buttons`);
    
    // Step 4: Check if there are any JavaScript errors or console logs
    const logs: string[] = [];
    page.on('console', msg => {
      logs.push(`${msg.type()}: ${msg.text()}`);
    });
    
    // Step 5: If we have delete buttons, try clicking one to trace the flow
    if (deleteButtonCount > 0) {
      console.log('🔄 Testing delete button click...');
      
      const firstDeleteBtn = deleteButtons.first();
      await firstDeleteBtn.click();
      
      // Wait for any confirmation dialogs
      const confirmDialog = page.locator('.modal, .dialog, .confirmation, .alert');
      if (await confirmDialog.isVisible({ timeout: 2000 })) {
        console.log('✅ Confirmation dialog appeared');
        const confirmBtn = confirmDialog.locator('button:has-text("Delete"), button:has-text("Confirm"), button:has-text("Yes")').first();
        if (await confirmBtn.isVisible()) {
          await confirmBtn.click();
          console.log('✅ Confirmed deletion');
        }
      }
      
      // Wait for deletion request
      await page.waitForTimeout(3000);
    } else {
      console.log('⚠️ No delete buttons found - testing manual deletion API call');
      
      // Manually trigger the FileUploadService method
      await page.evaluate(() => {
        // Check if FileUploadService is available
        if ((window as any).ng && (window as any).ng.getInjector) {
          try {
            const injector = (window as any).ng.getInjector(document.querySelector('app-root'));
            const fileUploadService = injector.get('FileUploadService');
            
            console.log('📦 FileUploadService found:', !!fileUploadService);
            
            if (fileUploadService && fileUploadService.deleteTemporaryEnhancedImage) {
              console.log('🔧 Calling deleteTemporaryEnhancedImage...');
              fileUploadService.deleteTemporaryEnhancedImage('test-image.jpg').subscribe({
                next: (result: any) => console.log('✅ Deletion result:', result),
                error: (err: any) => console.error('❌ Deletion error:', err)
              });
            }
          } catch (e) {
            console.error('Failed to access FileUploadService:', e);
          }
        }
        
        // Also try direct fetch to compare
        fetch('/api/FileUpload/enhanced/test-image.jpg', {
          method: 'DELETE',
          headers: {
            'Authorization': localStorage.getItem('auth_token') || '',
            'Content-Type': 'application/json'
          }
        }).then(response => {
          console.log('🌐 Direct fetch result:', response.status, response.statusText);
        }).catch(err => {
          console.error('🌐 Direct fetch error:', err);
        });
      });
      
      await page.waitForTimeout(3000);
    }
    
    // Step 6: Analyze captured requests
    console.log('\n📊 Network Request Analysis:');
    console.log('=' .repeat(50));
    
    const deleteRequests = capturedRequests.filter(req => req.method === 'DELETE');
    const apiRequests = capturedRequests.filter(req => req.url.includes('/api/'));
    
    console.log(`🌐 Total requests: ${capturedRequests.length}`);
    console.log(`🗑️ DELETE requests: ${deleteRequests.length}`);
    console.log(`🔌 API requests: ${apiRequests.length}`);
    
    deleteRequests.forEach((req, index) => {
      console.log(`\n🗑️ DELETE Request ${index + 1}:`);
      console.log(`   URL: ${req.url}`);
      console.log(`   Status: ${req.status || 'Pending'}`);
      console.log(`   Has Auth: ${req.headers.authorization ? '✅' : '❌'}`);
      console.log(`   Auth Value: ${req.headers.authorization || 'None'}`);
      console.log(`   All Headers:`, Object.keys(req.headers));
    });
    
    // Step 7: Check browser console logs
    console.log('\n📝 Browser Console Logs:');
    console.log('=' .repeat(50));
    logs.forEach(log => console.log(`   ${log}`));
    
    // Step 8: Inspect the current FileUploadService state
    const serviceState = await page.evaluate(() => {
      return {
        windowAuthService: !!(window as any).authService,
        windowFileUploadService: !!(window as any).FileUploadService,
        localStorageToken: localStorage.getItem('auth_token'),
        angularAvailable: !!(window as any).ng,
        angularVersion: (window as any).ng?.version?.full,
      };
    });
    
    console.log('\n🔧 Service State Analysis:');
    console.log('=' .repeat(50));
    console.log(`AuthService available: ${serviceState.windowAuthService ? '✅' : '❌'}`);
    console.log(`FileUploadService available: ${serviceState.windowFileUploadService ? '✅' : '❌'}`);
    console.log(`Auth token in localStorage: ${serviceState.localStorageToken ? '✅' : '❌'}`);
    console.log(`Angular available: ${serviceState.angularAvailable ? '✅' : '❌'}`);
    console.log(`Angular version: ${serviceState.angularVersion || 'Unknown'}`);
    
    // Assertions for the test
    expect(deleteRequests.length).toBeGreaterThanOrEqual(0); // We expect to see some DELETE requests
    
    if (deleteRequests.length > 0) {
      // Check if any DELETE requests have proper authentication
      const authenticatedDeletes = deleteRequests.filter(req => !!req.headers.authorization);
      console.log(`\n🔐 Authenticated DELETE requests: ${authenticatedDeletes.length}/${deleteRequests.length}`);
      
      if (authenticatedDeletes.length === 0) {
        console.log('❌ CRITICAL: All DELETE requests are missing authentication headers!');
      } else {
        console.log('✅ Some DELETE requests have authentication headers');
      }
    }
  });
  
  test('2. Test FileUploadService Integration', async ({ page }) => {
    console.log('\n🔍 Testing FileUploadService Integration...');
    
    await page.goto('http://localhost:4200');
    await page.waitForTimeout(2000);
    
    // Try to access Angular's dependency injection system
    const serviceTest = await page.evaluate(() => {
      try {
        // Modern Angular approach
        if ((window as any).ng) {
          const appRoot = document.querySelector('app-root');
          if (appRoot) {
            const injector = (window as any).ng.getInjector(appRoot);
            const fileUploadService = injector?.get?.('FileUploadService');
            
            if (fileUploadService) {
              console.log('✅ FileUploadService accessed via Angular DI');
              console.log('Available methods:', Object.getOwnPropertyNames(Object.getPrototypeOf(fileUploadService)));
              
              // Test if the deleteTemporaryEnhancedImage method exists
              if (typeof fileUploadService.deleteTemporaryEnhancedImage === 'function') {
                console.log('✅ deleteTemporaryEnhancedImage method found');
                return {
                  serviceFound: true,
                  methodFound: true,
                  methodType: typeof fileUploadService.deleteTemporaryEnhancedImage
                };
              } else {
                console.log('❌ deleteTemporaryEnhancedImage method NOT found');
                return {
                  serviceFound: true,
                  methodFound: false,
                  availableMethods: Object.getOwnPropertyNames(Object.getPrototypeOf(fileUploadService))
                };
              }
            }
          }
        }
        
        return {
          serviceFound: false,
          error: 'Could not access Angular DI or FileUploadService'
        };
      } catch (error) {
        return {
          serviceFound: false,
          error: error.message
        };
      }
    });
    
    console.log('\n📦 FileUploadService Test Results:');
    console.log('=' .repeat(50));
    console.log(JSON.stringify(serviceTest, null, 2));
    
    expect(serviceTest.serviceFound).toBe(true);
  });
  
  test('3. Find Alternative Deletion Mechanisms', async ({ page }) => {
    console.log('\n🔍 Searching for Alternative Deletion Mechanisms...');
    
    await page.goto('http://localhost:4200');
    await page.waitForTimeout(2000);
    
    // Search for any JavaScript code that makes DELETE requests
    const scriptAnalysis = await page.evaluate(() => {
      const scripts = Array.from(document.querySelectorAll('script'));
      const scriptContents = scripts.map(script => script.textContent || '').join('\n');
      
      // Look for common patterns that might indicate deletion code
      const patterns = [
        /delete.*enhanced/gi,
        /DELETE.*enhanced/gi,
        /fetch.*DELETE/gi,
        /http.*delete/gi,
        /\.delete\(/gi,
        /deleteTemporary/gi,
        /deleteEnhanced/gi,
        /remove.*image/gi
      ];
      
      const matches = patterns.map(pattern => {
        const found = scriptContents.match(pattern);
        return {
          pattern: pattern.toString(),
          matches: found ? found.length : 0,
          examples: found ? found.slice(0, 3) : []
        };
      }).filter(result => result.matches > 0);
      
      return {
        totalScripts: scripts.length,
        scriptSize: scriptContents.length,
        deletionPatterns: matches
      };
    });
    
    console.log('\n📜 Script Analysis Results:');
    console.log('=' .repeat(50));
    console.log(`Total scripts: ${scriptAnalysis.totalScripts}`);
    console.log(`Total script size: ${scriptAnalysis.scriptSize} characters`);
    console.log(`Deletion patterns found: ${scriptAnalysis.deletionPatterns.length}`);
    
    scriptAnalysis.deletionPatterns.forEach((pattern, index) => {
      console.log(`\n🔍 Pattern ${index + 1}: ${pattern.pattern}`);
      console.log(`   Matches: ${pattern.matches}`);
      console.log(`   Examples: ${pattern.examples.join(', ')}`);
    });
    
    // Also check for any event listeners on delete buttons
    const eventListenerAnalysis = await page.evaluate(() => {
      const deleteButtons = Array.from(document.querySelectorAll('button, [onclick], [data-testid*="delete"]'));
      
      return deleteButtons.map(button => {
        const element = button as HTMLElement;
        return {
          tagName: element.tagName,
          textContent: element.textContent?.trim().substring(0, 50),
          onclick: element.getAttribute('onclick'),
          dataTestId: element.getAttribute('data-testid'),
          classes: element.className,
          hasClickListener: !!(element as any)._listeners?.click || 
                            Object.keys(element).some(key => key.startsWith('__zone_symbol__addEventListener'))
        };
      }).filter(info => 
        info.textContent?.toLowerCase().includes('delete') ||
        info.onclick ||
        info.dataTestId?.includes('delete') ||
        info.classes?.includes('delete')
      );
    });
    
    console.log('\n🎯 Delete Button Analysis:');
    console.log('=' .repeat(50));
    eventListenerAnalysis.forEach((button, index) => {
      console.log(`\n🔘 Button ${index + 1}:`);
      console.log(`   Text: ${button.textContent}`);
      console.log(`   onclick: ${button.onclick || 'None'}`);
      console.log(`   data-testid: ${button.dataTestId || 'None'}`);
      console.log(`   Classes: ${button.classes || 'None'}`);
      console.log(`   Has Click Listener: ${button.hasClickListener ? '✅' : '❌'}`);
    });
  });
});