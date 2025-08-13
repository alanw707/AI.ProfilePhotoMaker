import { test, expect } from '@playwright/test';

/**
 * Simple OAuth Health Check
 * 
 * This test performs a basic health check on the OAuth endpoint
 * to determine the exact nature of the failure.
 */

const PRODUCTION_API = 'https://api.aiprofilephotomaker.com';
const OAUTH_ENDPOINT = `${PRODUCTION_API}/api/auth/external-login/google`;

test.describe('Simple OAuth Health Check', () => {

  test('Basic API Health Check', async ({ page }) => {
    console.log('🔍 Testing basic API connectivity...');
    
    // Test basic API connectivity first
    try {
      const response = await page.request.get(PRODUCTION_API, {
        timeout: 15000
      });
      
      console.log(`📊 Base API Status: ${response.status()}`);
      console.log(`📝 Base API Headers:`, response.headers());
      
      if (response.status() === 200) {
        const body = await response.text();
        console.log(`📋 Base API Response (first 200 chars): ${body.substring(0, 200)}`);
      }
      
    } catch (error) {
      console.error('❌ Base API not accessible:', error);
    }
  });

  test('OAuth Endpoint Request Analysis', async ({ page }) => {
    console.log('🔍 Analyzing OAuth endpoint with HTTP request...');
    
    try {
      // Use page.request.get for more detailed error information
      const response = await page.request.get(OAUTH_ENDPOINT, {
        timeout: 15000,
        ignoreHTTPSErrors: true
      });

      console.log(`📊 OAuth Endpoint Status: ${response.status()}`);
      console.log(`📝 OAuth Endpoint Headers:`, response.headers());
      
      if (response.status() >= 200 && response.status() < 400) {
        console.log('✅ OAuth endpoint is accessible');
      } else if (response.status() === 500) {
        console.log('❌ OAuth endpoint still returning 500 - session middleware not deployed');
        const body = await response.text();
        console.log(`📋 Error Response Body: ${body}`);
      } else {
        console.log(`⚠️  OAuth endpoint returned status: ${response.status()}`);
        const body = await response.text();
        console.log(`📋 Response Body: ${body.substring(0, 500)}`);
      }

    } catch (error) {
      console.error('❌ OAuth endpoint request failed:', error);
      
      // Try to extract more specific error information
      if (error.message.includes('ERR_HTTP_RESPONSE_CODE_FAILURE')) {
        console.error('🚨 This indicates the server is returning an HTTP error status code');
        console.error('🔍 This could be a 500 Internal Server Error or similar');
      }
      
      if (error.message.includes('net::ERR_NAME_NOT_RESOLVED')) {
        console.error('🚨 DNS resolution failed - domain not accessible');
      }
      
      if (error.message.includes('net::ERR_CONNECTION_REFUSED')) {
        console.error('🚨 Connection refused - server not responding');
      }
    }
  });

  test('Frontend Application Check', async ({ page }) => {
    console.log('🔍 Testing frontend application accessibility...');
    
    try {
      await page.goto('https://app.aiprofilephotomaker.com', {
        waitUntil: 'domcontentloaded',
        timeout: 30000
      });

      const title = await page.title();
      console.log(`📄 Frontend Page Title: ${title}`);
      console.log(`🌐 Frontend URL: ${page.url()}`);
      console.log('✅ Frontend application is accessible');
      
    } catch (error) {
      console.error('❌ Frontend application not accessible:', error);
    }
  });

  test('API Endpoint Discovery', async ({ page }) => {
    console.log('🔍 Discovering available API endpoints...');
    
    const endpointsToTest = [
      `${PRODUCTION_API}`,
      `${PRODUCTION_API}/health`,
      `${PRODUCTION_API}/api`,
      `${PRODUCTION_API}/api/health`,
      `${PRODUCTION_API}/api/auth`,
      `${PRODUCTION_API}/swagger`,
      `${PRODUCTION_API}/api/swagger`
    ];

    for (const endpoint of endpointsToTest) {
      try {
        const response = await page.request.get(endpoint, {
          timeout: 10000,
          ignoreHTTPSErrors: true
        });

        console.log(`📊 ${endpoint} - Status: ${response.status()}`);
        
        if (response.status() === 200) {
          const contentType = response.headers()['content-type'] || '';
          if (contentType.includes('json')) {
            try {
              const body = await response.json();
              console.log(`   JSON Response: ${JSON.stringify(body).substring(0, 100)}...`);
            } catch {
              console.log(`   JSON parsing failed`);
            }
          }
        }
        
      } catch (error) {
        console.log(`❌ ${endpoint} - Error: ${error.message.substring(0, 100)}`);
      }
    }
  });
});