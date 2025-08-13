import { test, expect } from '@playwright/test';

test.describe('Deployment Investigation', () => {
  test('Check custom domain and OAuth configuration', async ({ page, request }) => {
    console.log('=== DEPLOYMENT INVESTIGATION ===');
    
    // Test 1: Check if custom domains are reachable
    console.log('\n1. Testing Custom Domains:');
    
    try {
      const apiResponse = await request.get('https://api.aiprofilephotomaker.com/api/health/live');
      console.log('API Domain Status:', apiResponse.status(), apiResponse.statusText());
      console.log('API Headers:', await apiResponse.headers());
    } catch (error) {
      console.error('API Domain Error:', error);
    }
    
    try {
      const appResponse = await request.get('https://app.aiprofilephotomaker.com');
      console.log('App Domain Status:', appResponse.status(), appResponse.statusText());
      console.log('App Headers:', await appResponse.headers());
    } catch (error) {
      console.error('App Domain Error:', error);
    }
    
    // Test 2: Check OAuth redirect generation
    console.log('\n2. Testing OAuth Redirect:');
    
    try {
      const oauthResponse = await request.get('https://api.aiprofilephotomaker.com/api/auth/external-login/google', {
        maxRedirects: 0,
        failOnStatusCode: false
      });
      
      console.log('OAuth Status:', oauthResponse.status());
      const location = oauthResponse.headers()['location'];
      console.log('Redirect Location:', location);
      
      if (location) {
        const url = new URL(location);
        const redirectUri = url.searchParams.get('redirect_uri');
        console.log('OAuth Redirect URI:', redirectUri);
        
        if (redirectUri) {
          const isHttps = redirectUri.startsWith('https://');
          const domain = new URL(redirectUri).hostname;
          console.log('  - Protocol:', isHttps ? 'HTTPS ✓' : 'HTTP ✗');
          console.log('  - Domain:', domain);
          console.log('  - Is Custom Domain:', domain === 'api.aiprofilephotomaker.com' ? 'Yes ✓' : 'No ✗');
        }
      }
    } catch (error) {
      console.error('OAuth Error:', error);
    }
    
    // Test 3: Check Azure default domains
    console.log('\n3. Testing Azure Default Domains:');
    
    try {
      const backendRevision = await request.get('https://aipm-api-v1--0000013.bravehill-124f6a57.eastus2.azurecontainerapps.io/api/health/live');
      console.log('Backend Revision Status:', backendRevision.status());
    } catch (error) {
      console.error('Backend Revision Error:', error);
    }
    
    try {
      const frontendRevision = await request.get('https://aipm-web-v1--0000012.bravehill-124f6a57.eastus2.azurecontainerapps.io');
      console.log('Frontend Revision Status:', frontendRevision.status());
    } catch (error) {
      console.error('Frontend Revision Error:', error);
    }
    
    // Test 4: Navigate to frontend and check for URL issues
    console.log('\n4. Checking Frontend Navigation:');
    
    try {
      // Try custom domain first
      await page.goto('https://app.aiprofilephotomaker.com', { 
        waitUntil: 'domcontentloaded',
        timeout: 10000 
      });
      console.log('Custom domain loaded successfully');
      console.log('Current URL:', page.url());
    } catch (error) {
      console.log('Custom domain failed, trying Azure domain...');
      try {
        await page.goto('https://aipm-web-v1--0000012.bravehill-124f6a57.eastus2.azurecontainerapps.io', {
          waitUntil: 'domcontentloaded',
          timeout: 10000
        });
        console.log('Azure domain loaded successfully');
        console.log('Current URL:', page.url());
      } catch (innerError) {
        console.error('Both domains failed:', innerError);
      }
    }
    
    // Test 5: Check for network requests with duplicated URLs
    console.log('\n5. Monitoring Network Requests:');
    
    page.on('request', request => {
      const url = request.url();
      if (url.includes('api.aiprofilephotomaker.com')) {
        console.log('API Request:', url);
        // Check for URL duplication
        const pattern = /(api\.aiprofilephotomaker\.com.*api\.aiprofilephotomaker\.com)/;
        if (pattern.test(url)) {
          console.error('!!! URL DUPLICATION DETECTED:', url);
        }
      }
    });
    
    // Try to trigger an API call
    if (page.url().includes('aiprofilephotomaker') || page.url().includes('azurecontainerapps')) {
      try {
        // Look for any buttons or links that might trigger API calls
        const loginButton = await page.locator('text=/login/i').first();
        if (await loginButton.isVisible()) {
          console.log('Found login button, clicking...');
          await loginButton.click();
          await page.waitForTimeout(2000);
        }
      } catch (error) {
        console.log('No login button found or click failed');
      }
    }
    
    console.log('\n=== INVESTIGATION COMPLETE ===');
  });
  
  test('Direct API endpoint tests', async ({ request }) => {
    console.log('=== DIRECT API TESTS ===');
    
    const endpoints = [
      'https://api.aiprofilephotomaker.com/api/health/live',
      'https://api.aiprofilephotomaker.com/api/health/ready',
      'https://api.aiprofilephotomaker.com/api/auth/external-login/google',
    ];
    
    for (const endpoint of endpoints) {
      console.log(`\nTesting: ${endpoint}`);
      try {
        const response = await request.get(endpoint, {
          maxRedirects: 0,
          failOnStatusCode: false,
          timeout: 5000
        });
        
        console.log('  Status:', response.status());
        console.log('  Headers:', JSON.stringify(response.headers(), null, 2));
        
        if (response.status() === 302 || response.status() === 301) {
          const location = response.headers()['location'];
          console.log('  Redirect to:', location);
        }
      } catch (error) {
        console.error('  Error:', error);
      }
    }
    
    console.log('\n=== TESTS COMPLETE ===');
  });
});

test.describe('502 Bad Gateway Investigation', () => {
  test('Investigate 502 error on /api/image/images endpoint', async ({ request, page }) => {
    console.log('=== 502 BAD GATEWAY INVESTIGATION ===');
    
    const problemUrl = 'https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api/image/images';
    
    // Test 1: Direct API call to the failing endpoint
    console.log('\n1. Testing Failing Endpoint:');
    console.log('URL:', problemUrl);
    
    try {
      const response = await request.get(problemUrl, {
        maxRedirects: 0,
        failOnStatusCode: false,
        timeout: 10000
      });
      
      console.log('Status:', response.status());
      console.log('Status Text:', response.statusText());
      console.log('Headers:', JSON.stringify(response.headers(), null, 2));
      
      if (response.status() === 502) {
        console.log('✓ Confirmed 502 Bad Gateway error');
      }
      
      // Try to get response body for more details
      try {
        const body = await response.text();
        console.log('Response Body:', body);
      } catch (e) {
        console.log('Could not read response body');
      }
      
    } catch (error) {
      console.error('Request failed:', error);
    }
    
    // Test 2: Check if the backend API is reachable at all
    console.log('\n2. Testing Base API Health:');
    
    const baseApiUrl = 'https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io';
    
    try {
      const healthResponse = await request.get(`${baseApiUrl}/api/health/live`, {
        failOnStatusCode: false,
        timeout: 10000
      });
      console.log('Health Check Status:', healthResponse.status());
      
      if (healthResponse.ok()) {
        console.log('✓ Base API is reachable');
      } else {
        console.log('✗ Base API health check failed');
      }
    } catch (error) {
      console.error('Health check failed:', error);
    }
    
    // Test 3: Test other image-related endpoints to isolate the issue
    console.log('\n3. Testing Related Endpoints:');
    
    const relatedEndpoints = [
      '/api/image',
      '/api/image/upload',
      '/api/image/process',
      '/api/health/ready'
    ];
    
    for (const endpoint of relatedEndpoints) {
      const fullUrl = `${baseApiUrl}${endpoint}`;
      console.log(`\nTesting: ${endpoint}`);
      
      try {
        const response = await request.get(fullUrl, {
          failOnStatusCode: false,
          timeout: 5000
        });
        
        console.log(`  Status: ${response.status()} ${response.statusText()}`);
        
        if (response.status() === 502) {
          console.log('  ✗ Also returns 502 - widespread backend issue');
        } else if (response.status() === 404) {
          console.log('  ◯ 404 - endpoint may not exist (normal)');
        } else if (response.ok()) {
          console.log('  ✓ Working');
        } else {
          console.log('  ? Other error');
        }
        
      } catch (error) {
        console.error(`  Error: ${error}`);
      }
    }
    
    // Test 4: Check if this is a routing issue
    console.log('\n4. Testing URL Variants:');
    
    const urlVariants = [
      'https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api/image/images',
      'https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api/images',
      'https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api/image/list',
      // Check if there's a different revision that might work
      'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api/image/images'
    ];
    
    for (const url of urlVariants) {
      console.log(`\nTesting variant: ${url}`);
      
      try {
        const response = await request.get(url, {
          failOnStatusCode: false,
          timeout: 5000
        });
        
        console.log(`  Status: ${response.status()}`);
        
        if (response.status() !== 502) {
          console.log('  ✓ This variant does not return 502');
          if (response.ok()) {
            console.log('  ✓ This variant is working!');
          }
        }
        
      } catch (error) {
        console.error(`  Error: ${error}`);
      }
    }
    
    // Test 5: Check with different HTTP methods
    console.log('\n5. Testing Different HTTP Methods:');
    
    const methods = ['GET', 'POST', 'OPTIONS'];
    
    for (const method of methods) {
      console.log(`\nTesting ${method} ${problemUrl}`);
      
      try {
        let response;
        if (method === 'GET') {
          response = await request.get(problemUrl, { failOnStatusCode: false });
        } else if (method === 'POST') {
          response = await request.post(problemUrl, { 
            failOnStatusCode: false,
            data: {}
          });
        } else if (method === 'OPTIONS') {
          response = await request.fetch(problemUrl, { 
            method: 'OPTIONS',
            failOnStatusCode: false
          });
        }
        
        console.log(`  Status: ${response.status()}`);
        
      } catch (error) {
        console.error(`  Error: ${error}`);
      }
    }
    
    console.log('\n=== 502 INVESTIGATION COMPLETE ===');
  });
});
