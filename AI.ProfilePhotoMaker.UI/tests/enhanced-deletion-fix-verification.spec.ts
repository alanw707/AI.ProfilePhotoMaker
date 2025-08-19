import { test, expect, Page } from '@playwright/test';

/**
 * Enhanced Image Deletion Fix Verification
 * Verifies that FileUploadService is now properly available and
 * deleteTemporaryEnhancedImage method works with authentication
 */

test.describe('Enhanced Image Deletion Fix Verification', () => {
  let capturedRequests: any[] = [];
  
  test.beforeEach(async ({ page }) => {
    capturedRequests = [];
    
    // Intercept DELETE requests to verify authentication headers
    page.on('request', request => {
      if (request.method() === 'DELETE' && request.url().includes('/api/')) {
        capturedRequests.push({
          method: request.method(),
          url: request.url(),
          headers: request.headers()
        });
      }
    });
    
    page.on('response', response => {
      const request = capturedRequests.find(req => req.url === response.url());
      if (request) {
        request.status = response.status();
      }
    });
  });
  
  test('1. Verify FileUploadService is Now Available', async ({ page }) => {
    console.log('\n🔍 Verifying FileUploadService Availability...');
    
    await page.goto('http://localhost:4200');
    await page.waitForTimeout(3000); // Allow Angular to fully initialize
    
    // Set up authentication
    await page.evaluate(() => {
      const futureTimestamp = Math.floor(Date.now() / 1000) + 3600;
      const payload = btoa(JSON.stringify({
        sub: "1",
        name: "Test User",
        iat: Math.floor(Date.now() / 1000),
        exp: futureTimestamp
      }));
      localStorage.setItem('auth_token', `Bearer header.${payload}.signature`);
    });
    
    // Test FileUploadService accessibility
    const serviceTest = await page.evaluate(() => {
      try {
        // Try accessing through Angular DI
        if ((window as any).ng) {
          const appRoot = document.querySelector('app-root');
          if (appRoot) {
            const injector = (window as any).ng.getInjector(appRoot);
            const fileUploadService = injector.get('FileUploadService');
            
            if (fileUploadService) {
              return {
                success: true,
                serviceFound: true,
                hasDeleteMethod: typeof fileUploadService.deleteTemporaryEnhancedImage === 'function',
                authServiceFound: !!injector.get('AuthService'),
                methods: Object.getOwnPropertyNames(Object.getPrototypeOf(fileUploadService))
                  .filter(name => typeof fileUploadService[name] === 'function')
              };
            }
          }
        }
        
        return {
          success: false,
          error: 'FileUploadService not accessible'
        };
      } catch (error) {
        return {
          success: false,
          error: error.message
        };
      }
    });
    
    console.log('\n📦 Service Availability Test Results:');
    console.log('=' .repeat(50));
    console.log(JSON.stringify(serviceTest, null, 2));
    
    expect(serviceTest.success).toBe(true);
    expect(serviceTest.serviceFound).toBe(true);
    expect(serviceTest.hasDeleteMethod).toBe(true);
    expect(serviceTest.authServiceFound).toBe(true);
  });
  
  test('2. Test deleteTemporaryEnhancedImage with Authentication', async ({ page }) => {
    console.log('\n🔍 Testing deleteTemporaryEnhancedImage Method...');
    
    await page.goto('http://localhost:4200');
    await page.waitForTimeout(3000);
    
    // Set up authentication
    await page.evaluate(() => {
      const futureTimestamp = Math.floor(Date.now() / 1000) + 3600;
      const payload = btoa(JSON.stringify({
        sub: "1",
        name: "Test User",
        iat: Math.floor(Date.now() / 1000),
        exp: futureTimestamp
      }));
      localStorage.setItem('auth_token', `Bearer header.${payload}.signature`);
    });
    
    // Call the deleteTemporaryEnhancedImage method
    const deleteResult = await page.evaluate(() => {
      return new Promise((resolve) => {
        try {
          const appRoot = document.querySelector('app-root');
          const injector = (window as any).ng.getInjector(appRoot);
          const fileUploadService = injector.get('FileUploadService');
          
          if (fileUploadService && fileUploadService.deleteTemporaryEnhancedImage) {
            console.log('✅ Calling deleteTemporaryEnhancedImage...');
            
            fileUploadService.deleteTemporaryEnhancedImage('test-image.jpg').subscribe({
              next: (result: any) => {
                console.log('✅ Delete method executed:', result);
                resolve({
                  success: true,
                  called: true,
                  result: result
                });
              },
              error: (error: any) => {
                console.log('⚠️ Delete method called but returned error:', error);
                resolve({
                  success: true, // Method was called successfully
                  called: true,
                  error: error.message || error.toString()
                });
              }
            });
          } else {
            resolve({
              success: false,
              error: 'deleteTemporaryEnhancedImage method not found'
            });
          }
        } catch (error) {
          resolve({
            success: false,
            error: error.message
          });
        }
      });
    });
    
    // Wait for the request to be made
    await page.waitForTimeout(2000);
    
    console.log('\n🔧 Delete Method Test Results:');
    console.log('=' .repeat(50));
    console.log(JSON.stringify(deleteResult, null, 2));
    
    console.log('\n🌐 Network Request Analysis:');
    console.log('=' .repeat(50));
    console.log(`DELETE requests captured: ${capturedRequests.length}`);
    
    capturedRequests.forEach((req, index) => {
      console.log(`\n🗑️ DELETE Request ${index + 1}:`);
      console.log(`   URL: ${req.url}`);
      console.log(`   Status: ${req.status || 'Pending'}`);
      console.log(`   Has Authorization: ${req.headers.authorization ? '✅' : '❌'}`);
      console.log(`   Auth Header: ${req.headers.authorization || 'None'}`);
    });
    
    // Verify the method was called successfully
    expect(deleteResult.success).toBe(true);
    expect(deleteResult.called).toBe(true);
    
    // Verify we captured at least one DELETE request
    expect(capturedRequests.length).toBeGreaterThan(0);
    
    // Verify the DELETE request has authentication header
    const deleteRequest = capturedRequests[0];
    expect(deleteRequest.headers.authorization).toBeTruthy();
    expect(deleteRequest.headers.authorization).toContain('Bearer');
  });
  
  test('3. End-to-End Photo Enhancement Flow Test', async ({ page }) => {
    console.log('\n🔍 Testing Complete Photo Enhancement Flow...');
    
    await page.goto('http://localhost:4200/ai-enhancement');
    await page.waitForTimeout(3000);
    
    // Set up authentication
    await page.evaluate(() => {
      const futureTimestamp = Math.floor(Date.now() / 1000) + 3600;
      const payload = btoa(JSON.stringify({
        sub: "1",
        name: "Test User",
        iat: Math.floor(Date.now() / 1000),
        exp: futureTimestamp
      }));
      localStorage.setItem('auth_token', `Bearer header.${payload}.signature`);
    });
    
    // Look for file upload capability
    const uploadElement = page.locator('input[type="file"], .upload-area, [data-testid*="upload"]').first();
    const hasUpload = await uploadElement.isVisible({ timeout: 5000 });
    
    console.log(`📁 File upload element found: ${hasUpload ? '✅' : '❌'}`);
    
    // Check for enhancement interface
    const enhanceButton = page.locator('button:has-text("Enhance"), .enhance-btn, [data-testid*="enhance"]').first();
    const hasEnhanceButton = await enhanceButton.isVisible({ timeout: 2000 });
    
    console.log(`🎨 Enhancement button found: ${hasEnhanceButton ? '✅' : '❌'}`);
    
    // Verify page loaded correctly
    const pageTitle = await page.textContent('h1, .title, .heading');
    console.log(`📄 Page title: ${pageTitle || 'Not found'}`);
    
    // Check if PhotoEnhancementComponent can access FileUploadService
    const componentTest = await page.evaluate(() => {
      try {
        // Check if component is loaded and has access to services
        const componentElements = document.querySelectorAll('app-photo-enhancement');
        console.log(`Found ${componentElements.length} photo enhancement components`);
        
        if (componentElements.length > 0 && (window as any).ng) {
          const appRoot = document.querySelector('app-root');
          const injector = (window as any).ng.getInjector(appRoot);
          
          // Test that all required services are available
          const services = {
            fileUploadService: !!injector.get('FileUploadService'),
            authService: !!injector.get('AuthService'),
            configService: !!injector.get('ConfigService'),
            imageUrlService: !!injector.get('ImageUrlService')
          };
          
          return {
            success: true,
            componentFound: componentElements.length > 0,
            services: services,
            allServicesAvailable: Object.values(services).every(available => available)
          };
        }
        
        return {
          success: false,
          error: 'PhotoEnhancementComponent not found'
        };
      } catch (error) {
        return {
          success: false,
          error: error.message
        };
      }
    });
    
    console.log('\n🧩 Component Service Integration Test:');
    console.log('=' .repeat(50));
    console.log(JSON.stringify(componentTest, null, 2));
    
    expect(componentTest.success).toBe(true);
    expect(componentTest.allServicesAvailable).toBe(true);
  });
});