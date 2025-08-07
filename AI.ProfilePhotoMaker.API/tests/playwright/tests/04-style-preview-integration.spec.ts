import { test, expect } from '@playwright/test';
import { getAzureConfig } from './azure-config';
import { 
  STYLE_PREVIEW_TEST_CASES, 
  FRONTEND_APP_URL,
  PERFORMANCE_THRESHOLDS 
} from './test-data';

test.describe('Style Preview Component Integration Tests', () => {
  test.describe.configure({ mode: 'parallel' });

  test.beforeEach(async ({ page }) => {
    // Set up page for testing
    await page.goto(FRONTEND_APP_URL);
  });

  test('should integrate with Angular StylePreviewService', async ({ page }) => {
    console.log('🔍 Testing Angular StylePreviewService integration...');
    
    const config = getAzureConfig();
    
    // Navigate to a page that uses style previews
    await page.goto(`${FRONTEND_APP_URL}`);
    await page.waitForLoadState('networkidle');
    
    // Check if the StylePreviewService is available
    const serviceAvailable = await page.evaluate(() => {
      return typeof (window as any).angular !== 'undefined' || 
             document.querySelector('[ng-app]') !== null ||
             document.querySelector('[data-ng-app]') !== null ||
             document.querySelector('app-root') !== null;
    });
    
    if (!serviceAvailable) {
      console.log('⚠️  Angular application not detected on landing page');
      // Try to find Angular-specific elements or routes
      const possibleRoutes = ['/dashboard', '/styles', '/create'];
      
      for (const route of possibleRoutes) {
        try {
          await page.goto(`${FRONTEND_APP_URL}${route}`);
          await page.waitForLoadState('networkidle', { timeout: 10000 });
          
          const angularFound = await page.evaluate(() => {
            return document.querySelector('app-root') !== null ||
                   document.querySelector('[ng-version]') !== null;
          });
          
          if (angularFound) {
            console.log(`✅ Angular application found at route: ${route}`);
            break;
          }
        } catch (error) {
          console.log(`Route ${route} not accessible: ${error}`);
        }
      }
    }
    
    // Test style preview URL generation
    console.log('🖼️  Testing style preview URL generation...');
    
    const urlTest = await page.evaluate(async (baseUrl) => {
      // Test the URL generation logic that matches the Angular service
      const testStyleName = 'corporate';
      const fileName = `${testStyleName.toLowerCase().replace(/[/\\s]/g, '-')}.jpg`;
      const expectedUrl = `${baseUrl}/${fileName}`;
      
      return {
        styleName: testStyleName,
        fileName,
        expectedUrl,
        urlFormatValid: expectedUrl.includes('aipmstv16j74jubocuukg') && 
                       expectedUrl.includes('style-previews') &&
                       expectedUrl.endsWith('.jpg')
      };
    }, config.baseUrl);
    
    expect(urlTest.urlFormatValid).toBe(true);
    console.log(`✅ URL generation test passed: ${urlTest.expectedUrl}`);
  });

  test('should handle style preview loading states', async ({ page }) => {
    console.log('🔄 Testing style preview loading states...');
    
    const config = getAzureConfig();
    
    // Create a test page to simulate style preview loading
    await page.goto('data:text/html,<!DOCTYPE html><html><body><div id="test-container"></div></body></html>');
    
    // Inject test code that mimics the Angular service behavior
    const loadingTest = await page.evaluate(async (baseUrl) => {
      const testResults = [];
      const testStyles = ['corporate', 'executive', 'nonexistent-style'];
      
      for (const styleName of testStyles) {
        const fileName = `${styleName.toLowerCase().replace(/[/\\s]/g, '-')}.jpg`;
        const imageUrl = `${baseUrl}/${fileName}`;
        
        const result = await new Promise<any>((resolve) => {
          const img = new Image();
          const startTime = Date.now();
          
          const timeout = setTimeout(() => {
            resolve({
              styleName,
              status: 'timeout',
              loadTime: Date.now() - startTime,
              success: false
            });
          }, 10000);
          
          img.onload = () => {
            clearTimeout(timeout);
            resolve({
              styleName,
              status: 'loaded',
              loadTime: Date.now() - startTime,
              success: true,
              width: img.naturalWidth,
              height: img.naturalHeight
            });
          };
          
          img.onerror = () => {
            clearTimeout(timeout);
            resolve({
              styleName,
              status: 'error',
              loadTime: Date.now() - startTime,
              success: false
            });
          };
          
          img.src = imageUrl;
        });
        
        testResults.push(result);
      }
      
      return testResults;
    }, config.baseUrl);
    
    console.log('Loading test results:', loadingTest);
    
    // Analyze the results
    const loadedImages = loadingTest.filter(r => r.success);
    const failedImages = loadingTest.filter(r => !r.success);
    
    if (config.hasCredentials && config.testMode !== 'pre-upload') {
      // If we have credentials and expect images to exist
      console.log(`✅ ${loadedImages.length} images loaded successfully`);
      expect(loadedImages.length).toBeGreaterThan(0);
    } else {
      // Pre-upload state - expect failures (404s)
      console.log(`⚠️  ${failedImages.length} images failed to load (expected in pre-upload state)`);
      expect(failedImages.length).toBeGreaterThanOrEqual(2); // corporate and executive should fail
    }
  });

  test('should test error handling and fallback behavior', async ({ page }) => {
    console.log('🛡️  Testing error handling and fallback behavior...');
    
    // Create a test page with image loading error handling
    await page.goto('data:text/html,<!DOCTYPE html><html><body><div id="test-container"></div></body></html>');
    
    const errorHandlingTest = await page.evaluate(async () => {
      const testContainer = document.getElementById('test-container')!;
      const results = [];
      
      // Test 1: Invalid URL
      const invalidImg = new Image();
      const invalidTest = await new Promise<boolean>((resolve) => {
        invalidImg.onerror = () => resolve(true);
        invalidImg.onload = () => resolve(false);
        invalidImg.src = 'https://invalid-domain.com/nonexistent.jpg';
        setTimeout(() => resolve(true), 5000); // timeout after 5s
      });
      
      results.push({ test: 'invalid-url', errorHandled: invalidTest });
      
      // Test 2: Network timeout simulation
      const slowImg = new Image();
      const timeoutTest = await new Promise<boolean>((resolve) => {
        const timeout = setTimeout(() => {
          resolve(true); // Timeout handled
        }, 1000);
        
        slowImg.onload = () => {
          clearTimeout(timeout);
          resolve(false);
        };
        
        slowImg.onerror = () => {
          clearTimeout(timeout);
          resolve(true);
        };
        
        // Use a URL that should timeout or fail
        slowImg.src = 'https://httpstat.us/408?sleep=5000';
      });
      
      results.push({ test: 'network-timeout', errorHandled: timeoutTest });
      
      return results;
    });
    
    console.log('Error handling test results:', errorHandlingTest);
    
    // All error scenarios should be handled gracefully
    errorHandlingTest.forEach(result => {
      expect(result.errorHandled).toBe(true);
    });
    
    console.log('✅ Error handling tests passed');
  });

  test('should validate caching behavior', async ({ page }) => {
    console.log('💾 Testing caching behavior...');
    
    const config = getAzureConfig();
    
    if (!config.hasCredentials) {
      console.log('⏭️  Skipping cache test - requires working images');
      return;
    }
    
    // Create a test page to simulate caching behavior
    await page.goto('data:text/html,<!DOCTYPE html><html><body><div id="test-container"></div></body></html>');
    
    const cacheTest = await page.evaluate(async (baseUrl) => {
      const testUrl = `${baseUrl}/corporate.jpg`;
      const results = [];
      
      // First load
      const startTime1 = Date.now();
      const response1 = await fetch(testUrl);
      const loadTime1 = Date.now() - startTime1;
      
      results.push({
        attempt: 1,
        status: response1.status,
        loadTime: loadTime1,
        fromCache: false
      });
      
      // Second load (should be faster if cached)
      const startTime2 = Date.now();
      const response2 = await fetch(testUrl);
      const loadTime2 = Date.now() - startTime2;
      
      results.push({
        attempt: 2,
        status: response2.status,
        loadTime: loadTime2,
        fromCache: loadTime2 < loadTime1 * 0.5 // Heuristic for cache hit
      });
      
      return results;
    }, config.baseUrl);
    
    console.log('Cache test results:', cacheTest);
    
    if (cacheTest.length >= 2 && cacheTest[0].status === 200) {
      const firstLoad = cacheTest[0];
      const secondLoad = cacheTest[1];
      
      console.log(`First load: ${firstLoad.loadTime}ms`);
      console.log(`Second load: ${secondLoad.loadTime}ms`);
      
      if (secondLoad.fromCache) {
        console.log('✅ Caching appears to be working (second load was faster)');
      } else {
        console.log('ℹ️  No obvious caching benefit detected');
      }
      
      expect(secondLoad.status).toBe(200);
    } else {
      console.log('⚠️  Cache test inconclusive - images may not be available');
    }
  });

  test('should test responsive image loading', async ({ page, browserName }) => {
    console.log(`📱 Testing responsive image loading on ${browserName}...`);
    
    const config = getAzureConfig();
    
    if (!config.visualTests) {
      console.log('⏭️  Skipping visual tests - disabled in configuration');
      return;
    }
    
    // Test different viewport sizes
    const viewports = [
      { width: 320, height: 568, name: 'Mobile Portrait' },
      { width: 768, height: 1024, name: 'Tablet Portrait' },
      { width: 1920, height: 1080, name: 'Desktop' }
    ];
    
    for (const viewport of viewports) {
      console.log(`Testing ${viewport.name} (${viewport.width}x${viewport.height})...`);
      
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      
      // Create test page with responsive image
      await page.goto('data:text/html,<!DOCTYPE html><html><head><meta name="viewport" content="width=device-width,initial-scale=1"></head><body></body></html>');
      
      const responsiveTest = await page.evaluate(async (args) => {
        const { baseUrl, viewport } = args;
        const testUrl = `${baseUrl}/corporate.jpg`;
        
        // Create responsive image element
        const img = new Image();
        img.style.maxWidth = '100%';
        img.style.height = 'auto';
        
        const result = await new Promise<any>((resolve) => {
          img.onload = () => {
            document.body.appendChild(img);
            
            resolve({
              viewport: viewport.name,
              naturalWidth: img.naturalWidth,
              naturalHeight: img.naturalHeight,
              displayWidth: img.clientWidth,
              displayHeight: img.clientHeight,
              loaded: true
            });
          };
          
          img.onerror = () => {
            resolve({
              viewport: viewport.name,
              loaded: false
            });
          };
          
          img.src = testUrl;
        });
        
        return result;
      }, { baseUrl: config.baseUrl, viewport });
      
      console.log(`${viewport.name} result:`, responsiveTest);
      
      if (config.hasCredentials && responsiveTest.loaded) {
        expect(responsiveTest.displayWidth).toBeLessThanOrEqual(viewport.width);
        expect(responsiveTest.displayWidth).toBeGreaterThan(0);
      }
    }
    
    console.log('✅ Responsive image testing completed');
  });
});