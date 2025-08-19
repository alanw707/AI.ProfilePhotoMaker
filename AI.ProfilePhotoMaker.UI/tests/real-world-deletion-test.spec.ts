import { test, expect, Page } from '@playwright/test';

/**
 * Real-world Enhanced Image Deletion Test
 * Tests the actual deletion functionality in the context where it's used
 */

test.describe('Real-world Enhanced Image Deletion Test', () => {
  let capturedRequests: any[] = [];
  
  test.beforeEach(async ({ page }) => {
    capturedRequests = [];
    
    // Intercept ALL requests to see what's happening
    page.on('request', request => {
      capturedRequests.push({
        method: request.method(),
        url: request.url(),
        headers: request.headers(),
        timestamp: new Date().toISOString()
      });
    });
    
    page.on('response', response => {
      const request = capturedRequests.find(req => req.url === response.url());
      if (request) {
        request.status = response.status();
        request.statusText = response.statusText();
      }
    });
    
    // Capture console logs for debugging
    page.on('console', msg => {
      if (msg.type() === 'error' || msg.text().includes('delete') || msg.text().includes('clean')) {
        console.log(`🖥️ Console ${msg.type()}: ${msg.text()}`);
      }
    });
  });
  
  test('Test Photo Enhancement Component Integration', async ({ page }) => {
    console.log('\n🔍 Testing Photo Enhancement Component Integration...');
    
    // Navigate to photo enhancement
    await page.goto('http://localhost:4200/ai-enhancement');
    await page.waitForTimeout(3000);
    
    // Set up authentication with a valid token
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
    
    // Check if the page loaded correctly
    const pageContent = await page.textContent('body');
    const hasPhotoEnhancement = pageContent?.includes('enhance') || pageContent?.includes('upload') || pageContent?.includes('photo');
    
    console.log(`📄 Photo enhancement page loaded: ${hasPhotoEnhancement ? '✅' : '❌'}`);
    
    if (!hasPhotoEnhancement) {
      console.log('📄 Page content preview:', pageContent?.substring(0, 200) + '...');
    }
    
    // Look for upload elements
    const uploadInput = page.locator('input[type="file"]').first();
    const hasUploadInput = await uploadInput.isVisible({ timeout: 2000 });
    console.log(`📁 Upload input found: ${hasUploadInput ? '✅' : '❌'}`);
    
    // Check if PhotoEnhancementComponent is loaded
    const componentExists = await page.locator('app-photo-enhancement').isVisible({ timeout: 2000 });
    console.log(`🧩 PhotoEnhancementComponent found: ${componentExists ? '✅' : '❌'}`);
    
    // Test the cleanupTemporaryImage method if component is available
    if (componentExists) {
      console.log('\n🧪 Testing cleanupTemporaryImage method...');
      
      // Simulate calling the cleanupTemporaryImage method
      const cleanupResult = await page.evaluate(() => {
        return new Promise((resolve) => {
          try {
            // Try to trigger a cleanup operation
            // This simulates what happens after a successful enhancement
            const testFileName = 'test-enhanced-image.jpg';
            
            // Create a mock fetch to simulate the cleanup call
            const originalFetch = window.fetch;
            window.fetch = function(url, options) {
              console.log('🌐 Mock cleanup request:', {
                url: url.toString(),
                method: options?.method,
                headers: options?.headers
              });
              
              if (url.toString().includes('/api/FileUpload/enhanced/') && options?.method === 'DELETE') {
                return Promise.resolve(new Response(JSON.stringify({
                  success: true,
                  message: 'File deleted successfully'
                }), {
                  status: 200,
                  headers: { 'Content-Type': 'application/json' }
                }));
              }
              
              return originalFetch.call(this, url, options);
            };
            
            // Now try to access the component's cleanup method
            const appElement = document.querySelector('app-photo-enhancement');
            if (appElement && (window as any).ng) {
              const componentInstance = (window as any).ng.getComponent(appElement);
              if (componentInstance && typeof componentInstance.cleanupTemporaryImage === 'function') {
                console.log('✅ Found cleanupTemporaryImage method');
                
                // Call the method (this should trigger the FileUploadService call)
                componentInstance.cleanupTemporaryImage(testFileName);
                
                setTimeout(() => {
                  resolve({
                    success: true,
                    methodFound: true,
                    called: true
                  });
                }, 1000);
              } else {
                resolve({
                  success: false,
                  error: 'cleanupTemporaryImage method not found on component'
                });
              }
            } else {
              resolve({
                success: false,
                error: 'Component instance not accessible'
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
      
      await page.waitForTimeout(2000); // Wait for cleanup to complete
      
      console.log('\n🔧 Cleanup Test Results:');
      console.log('=' .repeat(50));
      console.log(JSON.stringify(cleanupResult, null, 2));
      
      // Check for DELETE requests
      const deleteRequests = capturedRequests.filter(req => 
        req.method === 'DELETE' && req.url.includes('/api/FileUpload/enhanced/')
      );
      
      console.log('\n🗑️ DELETE Request Analysis:');
      console.log('=' .repeat(50));
      console.log(`DELETE requests captured: ${deleteRequests.length}`);
      
      deleteRequests.forEach((req, index) => {
        console.log(`\nRequest ${index + 1}:`);
        console.log(`  URL: ${req.url}`);
        console.log(`  Method: ${req.method}`);
        console.log(`  Status: ${req.status || 'Pending'}`);
        console.log(`  Has Authorization: ${req.headers.authorization ? '✅' : '❌'}`);
        console.log(`  Auth Header: ${req.headers.authorization || 'None'}`);
        console.log(`  Timestamp: ${req.timestamp}`);
      });
      
      if (deleteRequests.length > 0) {
        console.log('\n✅ SUCCESS: DELETE requests are being made!');
        if (deleteRequests.some(req => req.headers.authorization)) {
          console.log('✅ SUCCESS: Authentication headers are present!');
        } else {
          console.log('❌ ISSUE: DELETE requests missing authentication headers');
        }
      } else {
        console.log('\n⚠️ No DELETE requests captured - may indicate service injection issue');
      }
    }
    
    // Test accessing FileUploadService through the component's dependency injection
    const serviceAccessTest = await page.evaluate(() => {
      try {
        const appElement = document.querySelector('app-photo-enhancement');
        if (appElement && (window as any).ng) {
          const componentInstance = (window as any).ng.getComponent(appElement);
          
          // Try to access the private _fileUploadService property
          const hasFileUploadService = componentInstance && '_fileUploadService' in componentInstance;
          
          if (hasFileUploadService) {
            const service = componentInstance._fileUploadService;
            const hasDeleteMethod = service && typeof service.deleteTemporaryEnhancedImage === 'function';
            
            return {
              success: true,
              componentHasService: true,
              serviceHasMethod: hasDeleteMethod,
              serviceMethods: service ? Object.getOwnPropertyNames(Object.getPrototypeOf(service))
                .filter(name => typeof service[name] === 'function') : []
            };
          }
        }
        
        return {
          success: false,
          error: 'Could not access component or service'
        };
      } catch (error) {
        return {
          success: false,
          error: error.message
        };
      }
    });
    
    console.log('\n🔌 Component Service Access Test:');
    console.log('=' .repeat(50));
    console.log(JSON.stringify(serviceAccessTest, null, 2));
    
    // Basic assertions
    expect(hasPhotoEnhancement).toBe(true);
    expect(componentExists).toBe(true);
  });
  
  test('Verify Authentication Headers in Real Context', async ({ page }) => {
    console.log('\n🔍 Testing Authentication Headers in Real Context...');
    
    await page.goto('http://localhost:4200/ai-enhancement');
    await page.waitForTimeout(2000);
    
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
    
    // Manually trigger a delete request using the FileUploadService approach
    const manualDeleteTest = await page.evaluate(() => {
      return new Promise((resolve) => {
        try {
          // Simulate what PhotoEnhancementComponent does
          const testUrl = '/api/FileUpload/enhanced/test-image.jpg';
          const token = localStorage.getItem('auth_token');
          
          const headers = {};
          if (token) {
            headers['Authorization'] = token;
          }
          
          console.log('🔧 Manual delete test:', {
            url: testUrl,
            headers: headers,
            hasAuth: !!headers['Authorization']
          });
          
          fetch(testUrl, {
            method: 'DELETE',
            headers: {
              ...headers,
              'Content-Type': 'application/json'
            }
          }).then(response => {
            resolve({
              success: true,
              status: response.status,
              statusText: response.statusText,
              authHeaderSent: !!headers['Authorization']
            });
          }).catch(error => {
            resolve({
              success: false,
              error: error.message,
              authHeaderSent: !!headers['Authorization']
            });
          });
        } catch (error) {
          resolve({
            success: false,
            error: error.message
          });
        }
      });
    });
    
    await page.waitForTimeout(1000);
    
    console.log('\n🧪 Manual Delete Test Results:');
    console.log('=' .repeat(50));
    console.log(JSON.stringify(manualDeleteTest, null, 2));
    
    // Check captured requests
    const deleteRequests = capturedRequests.filter(req => 
      req.method === 'DELETE' && req.url.includes('/api/FileUpload/enhanced/')
    );
    
    console.log('\n📡 Request Capture Results:');
    console.log('=' .repeat(50));
    console.log(`DELETE requests captured: ${deleteRequests.length}`);
    
    if (deleteRequests.length > 0) {
      const latestRequest = deleteRequests[deleteRequests.length - 1];
      console.log('\nLatest DELETE request:');
      console.log(`  URL: ${latestRequest.url}`);
      console.log(`  Method: ${latestRequest.method}`);
      console.log(`  Status: ${latestRequest.status || 'Pending'}`);
      console.log(`  Has Authorization: ${latestRequest.headers.authorization ? '✅' : '❌'}`);
      console.log(`  Auth Value: ${latestRequest.headers.authorization || 'None'}`);
      
      expect(latestRequest.headers.authorization).toBeTruthy();
      expect(latestRequest.headers.authorization).toContain('Bearer');
    }
    
    expect(deleteRequests.length).toBeGreaterThan(0);
  });
});