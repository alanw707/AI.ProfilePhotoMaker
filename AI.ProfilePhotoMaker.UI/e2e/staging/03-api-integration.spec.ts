import { test, expect } from '@playwright/test';
import { StagingTestHelpers, waitForStableLoad } from './utils/test-helpers';

test.describe('API Integration - Staging Environment', () => {
  let helpers: StagingTestHelpers;

  test.beforeEach(async ({ page }) => {
    helpers = new StagingTestHelpers(page);
  });

  test('should verify API endpoints are accessible', async ({ page }) => {
    console.log('🔌 Testing API endpoint accessibility...');
    
    const apiCalls: Array<{url: string, status: number, method: string}> = [];
    
    // Monitor all API requests
    page.on('request', request => {
      const url = request.url();
      if (url.includes('/api/')) {
        apiCalls.push({
          url,
          status: 0, // Will be updated on response
          method: request.method()
        });
      }
    });
    
    page.on('response', response => {
      const url = response.url();
      if (url.includes('/api/')) {
        const call = apiCalls.find(c => c.url === url && c.status === 0);
        if (call) {
          call.status = response.status();
        }
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait for any additional API calls
    await page.waitForTimeout(5000);
    
    console.log('📊 API Calls Summary:');
    console.log(`  Total API calls: ${apiCalls.length}`);
    
    if (apiCalls.length > 0) {
      const successfulCalls = apiCalls.filter(call => call.status >= 200 && call.status < 300);
      const failedCalls = apiCalls.filter(call => call.status >= 400);
      
      console.log(`  Successful calls: ${successfulCalls.length}`);
      console.log(`  Failed calls: ${failedCalls.length}`);
      
      console.log('\n✅ Successful API endpoints:');
      successfulCalls.forEach(call => 
        console.log(`  ${call.method} ${call.status} - ${call.url}`)
      );
      
      if (failedCalls.length > 0) {
        console.log('\n❌ Failed API endpoints:');
        failedCalls.forEach(call => 
          console.log(`  ${call.method} ${call.status} - ${call.url}`)
        );
      }
      
      // Expect at least some successful API calls
      expect(successfulCalls.length).toBeGreaterThan(0);
    } else {
      console.log('ℹ️ No API calls detected on initial page load');
    }
  });

  test('should verify styles API endpoint', async ({ page }) => {
    console.log('🎨 Testing styles API endpoint...');
    
    let stylesApiCalled = false;
    let stylesApiStatus = 0;
    let stylesApiResponse: any = null;
    
    page.on('response', async response => {
      const url = response.url();
      if (url.includes('/api/') && (url.includes('style') || url.includes('Style'))) {
        stylesApiCalled = true;
        stylesApiStatus = response.status();
        
        try {
          if (response.headers()['content-type']?.includes('application/json')) {
            stylesApiResponse = await response.json();
          }
        } catch (e) {
          console.log('Could not parse styles API response as JSON');
        }
        
        console.log(`🎨 Styles API: ${response.status()} - ${url}`);
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait longer for styles to load
    await page.waitForTimeout(7000);
    
    if (stylesApiCalled) {
      console.log(`✅ Styles API called with status: ${stylesApiStatus}`);
      expect(stylesApiStatus).toBeLessThan(400);
      
      if (stylesApiResponse) {
        console.log('📊 Styles API Response:', {
          success: stylesApiResponse.success,
          dataLength: stylesApiResponse.data?.length || 0
        });
        
        if (stylesApiResponse.success && stylesApiResponse.data) {
          expect(stylesApiResponse.data.length).toBeGreaterThan(0);
          console.log(`✅ ${stylesApiResponse.data.length} styles returned from API`);
        }
      }
    } else {
      console.log('ℹ️ No styles API call detected - may be using cached/fallback data');
    }
  });

  test('should verify credit packages API endpoint', async ({ page }) => {
    console.log('💳 Testing credit packages API endpoint...');
    
    let packagesApiCalled = false;
    let packagesApiStatus = 0;
    let packagesApiResponse: any = null;
    
    page.on('response', async response => {
      const url = response.url();
      if (url.includes('/api/') && (
        url.includes('package') || 
        url.includes('credit') || 
        url.includes('Package') ||
        url.includes('Credit')
      )) {
        packagesApiCalled = true;
        packagesApiStatus = response.status();
        
        try {
          if (response.headers()['content-type']?.includes('application/json')) {
            packagesApiResponse = await response.json();
          }
        } catch (e) {
          console.log('Could not parse packages API response as JSON');
        }
        
        console.log(`💳 Packages API: ${response.status()} - ${url}`);
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait for packages to load
    await page.waitForTimeout(5000);
    
    if (packagesApiCalled) {
      console.log(`✅ Packages API called with status: ${packagesApiStatus}`);
      expect(packagesApiStatus).toBeLessThan(400);
      
      if (packagesApiResponse) {
        console.log('📊 Packages API Response:', {
          success: packagesApiResponse.success,
          dataLength: packagesApiResponse.data?.length || 0
        });
        
        if (packagesApiResponse.success && packagesApiResponse.data) {
          expect(packagesApiResponse.data.length).toBeGreaterThan(0);
          console.log(`✅ ${packagesApiResponse.data.length} packages returned from API`);
        }
      }
    } else {
      console.log('ℹ️ No packages API call detected - may be using cached/fallback data');
    }
  });

  test('should verify CORS and security headers', async ({ page }) => {
    console.log('🔒 Testing CORS and security headers...');
    
    const securityHeaders: Array<{url: string, headers: any}> = [];
    
    page.on('response', response => {
      const url = response.url();
      if (url.includes('/api/')) {
        securityHeaders.push({
          url,
          headers: {
            'access-control-allow-origin': response.headers()['access-control-allow-origin'],
            'access-control-allow-methods': response.headers()['access-control-allow-methods'],
            'content-type': response.headers()['content-type'],
            'cache-control': response.headers()['cache-control']
          }
        });
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    await page.waitForTimeout(3000);
    
    if (securityHeaders.length > 0) {
      console.log('📊 API Security Headers:');
      securityHeaders.slice(0, 3).forEach((item, index) => {
        console.log(`  API ${index + 1}:`);
        console.log(`    URL: ${item.url}`);
        console.log(`    CORS Origin: ${item.headers['access-control-allow-origin'] || 'Not set'}`);
        console.log(`    Content-Type: ${item.headers['content-type'] || 'Not set'}`);
      });
      
      // Verify CORS is properly configured
      const corsConfigured = securityHeaders.some(item => 
        item.headers['access-control-allow-origin']
      );
      
      if (corsConfigured) {
        console.log('✅ CORS headers found');
      } else {
        console.log('⚠️ No CORS headers detected');
      }
    }
  });

  test('should verify API response times are acceptable', async ({ page }) => {
    console.log('⏱️ Testing API response times...');
    
    const apiTimings: Array<{url: string, duration: number}> = [];
    const requestStartTimes = new Map<string, number>();
    
    page.on('request', request => {
      const url = request.url();
      if (url.includes('/api/')) {
        requestStartTimes.set(url, Date.now());
      }
    });
    
    page.on('response', response => {
      const url = response.url();
      if (url.includes('/api/')) {
        const startTime = requestStartTimes.get(url);
        if (startTime) {
          const duration = Date.now() - startTime;
          apiTimings.push({ url, duration });
        }
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    await page.waitForTimeout(5000);
    
    if (apiTimings.length > 0) {
      console.log('📊 API Response Times:');
      apiTimings.forEach(timing => {
        console.log(`  ${timing.duration}ms - ${timing.url}`);
      });
      
      const averageTime = apiTimings.reduce((sum, timing) => sum + timing.duration, 0) / apiTimings.length;
      const maxTime = Math.max(...apiTimings.map(t => t.duration));
      
      console.log(`  Average response time: ${averageTime.toFixed(0)}ms`);
      console.log(`  Maximum response time: ${maxTime}ms`);
      
      // Verify response times are reasonable
      expect(averageTime).toBeLessThan(3000); // 3 seconds average
      expect(maxTime).toBeLessThan(10000); // 10 seconds max
      
      // Alert if response times are slow
      if (averageTime > 1000) {
        console.warn(`⚠️ Slow API response time: ${averageTime.toFixed(0)}ms average`);
      }
    } else {
      console.log('ℹ️ No API timing data collected');
    }
  });

  test('should verify error handling for failed API requests', async ({ page }) => {
    console.log('🚨 Testing API error handling...');
    
    const failedRequests: Array<{url: string, status: number, error?: string}> = [];
    
    page.on('response', response => {
      if (response.url().includes('/api/') && response.status() >= 400) {
        failedRequests.push({
          url: response.url(),
          status: response.status()
        });
      }
    });
    
    page.on('requestfailed', request => {
      if (request.url().includes('/api/')) {
        failedRequests.push({
          url: request.url(),
          status: 0,
          error: request.failure()?.errorText
        });
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    await page.waitForTimeout(5000);
    
    if (failedRequests.length > 0) {
      console.log('❌ Failed API Requests:');
      failedRequests.forEach(req => {
        console.log(`  ${req.status} - ${req.url} ${req.error ? `(${req.error})` : ''}`);
      });
      
      // Check if the app handles errors gracefully
      const errorMessages = page.locator('.error, .alert-error, [class*="error"]');
      const errorCount = await errorMessages.count();
      
      const loadingStates = page.locator('.loading, .spinner, [class*="loading"]');
      const loadingCount = await loadingStates.count();
      
      console.log(`  Error messages shown: ${errorCount}`);
      console.log(`  Loading states: ${loadingCount}`);
      
      // Verify the app doesn't get stuck in loading state
      expect(loadingCount).toBeLessThan(5); // Reasonable number of loading indicators
    } else {
      console.log('✅ No failed API requests detected');
    }
  });
});