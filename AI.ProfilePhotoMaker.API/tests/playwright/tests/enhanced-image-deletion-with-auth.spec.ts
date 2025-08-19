import { test, expect, Page } from '@playwright/test';

/**
 * Enhanced Image Deletion with Authentication Test Suite
 * 
 * PURPOSE: Test enhanced image deletion with proper authentication
 * CONTEXT: Previous tests showed 401 errors - need to test with auth
 * 
 * TEST SCENARIOS:
 * 1. Simulate user authentication (OAuth or direct login)
 * 2. Test DELETE /api/image/enhanced/{fileName} with auth token
 * 3. Test both successful and failed deletion scenarios
 * 4. Verify actual deletion occurs in storage
 */

const NGROK_BASE_URL = 'https://clear-anteater-usually.ngrok-free.app';
const API_BASE_URL = `${NGROK_BASE_URL}/api`;

interface AuthTestResult {
  step: string;
  success: boolean;
  details: string;
  statusCode?: number;
  responseTime?: number;
  timestamp: string;
}

test.describe('Enhanced Image Deletion with Authentication', () => {
  
  let authResults: AuthTestResult[] = [];
  let authToken: string | null = null;
  let isAuthenticated = false;

  test.beforeEach(async ({ page }) => {
    test.setTimeout(120000); // 2 minutes
    authResults = [];
    authToken = null;
    isAuthenticated = false;
    
    console.log('🔐 Testing enhanced image deletion with authentication...');
  });

  test.afterEach(async ({ page }) => {
    console.log('\n📊 AUTHENTICATION TEST REPORT');
    console.log('=' + '='.repeat(40));
    
    const totalSteps = authResults.length;
    const successfulSteps = authResults.filter(r => r.success).length;
    
    console.log(`Total steps: ${totalSteps}`);
    console.log(`Successful: ${successfulSteps}`);
    console.log(`Success rate: ${((successfulSteps / totalSteps) * 100).toFixed(1)}%`);
    
    authResults.forEach((result, index) => {
      const status = result.success ? '✅' : '❌';
      console.log(`${index + 1}. ${status} ${result.step}`);
      console.log(`   ${result.details}`);
      if (result.statusCode) console.log(`   HTTP ${result.statusCode}`);
      if (result.responseTime) console.log(`   ${result.responseTime}ms`);
      console.log('');
    });
  });

  async function addAuthResult(step: string, success: boolean, details: string, extra?: {
    statusCode?: number;
    responseTime?: number;
  }) {
    const result: AuthTestResult = {
      step,
      success,
      details,
      timestamp: new Date().toISOString(),
      ...extra
    };
    authResults.push(result);
    
    const status = success ? '✅' : '❌';
    console.log(`${status} ${step}: ${details}`);
    if (extra?.statusCode) console.log(`   HTTP ${extra.statusCode}`);
    if (extra?.responseTime) console.log(`   ${extra.responseTime}ms`);
  }

  test('should test enhanced image deletion through browser authentication workflow', async ({ page }) => {
    console.log('🌐 Testing deletion through browser authentication...');

    // Step 1: Navigate to the application and check authentication status
    try {
      await page.goto(NGROK_BASE_URL, { timeout: 30000 });
      await page.waitForTimeout(3000); // Allow app to load
      
      await addAuthResult('Application Navigation', true, 'Successfully navigated to application');

      // Check if user is already authenticated by looking for auth-related elements
      const authIndicators = [
        'text=Sign Out',
        'text=Logout',
        'text=Profile',
        '[data-authenticated="true"]',
        '.authenticated',
        '.user-menu'
      ];

      let foundAuthIndicator = false;
      for (const indicator of authIndicators) {
        const count = await page.locator(indicator).count();
        if (count > 0) {
          foundAuthIndicator = true;
          await addAuthResult('Authentication Check', true, `Found auth indicator: ${indicator}`);
          break;
        }
      }

      if (!foundAuthIndicator) {
        await addAuthResult('Authentication Check', false, 'No authentication indicators found in UI');
        
        // Try to trigger authentication flow
        await attemptAuthentication(page);
      } else {
        isAuthenticated = true;
      }

    } catch (error) {
      await addAuthResult('Application Navigation', false, `Failed to navigate: ${error}`);
      return;
    }

    // Step 2: Extract authentication token from browser if available
    if (isAuthenticated) {
      await extractAuthToken(page);
    }

    // Step 3: Test deletion with browser context (including any cookies/tokens)
    await testDeletionWithBrowserAuth(page);

    // Step 4: If we have a token, test API deletion directly
    if (authToken) {
      await testDeletionWithTokenAuth(page);
    }
  });

  test('should test deletion by bypassing authentication (development mode)', async ({ page }) => {
    console.log('🔓 Testing deletion bypassing authentication...');

    // In development, some endpoints might not require auth or use a simpler auth
    // Let's test various scenarios

    // Scenario 1: Test if there's a development/admin bypass
    await testDevelopmentBypass(page);

    // Scenario 2: Test with mock/sample user authentication
    await testMockAuthentication(page);

    // Scenario 3: Test direct storage deletion (if possible)
    await testDirectStorageDeletion(page);
  });

  async function attemptAuthentication(page: Page) {
    try {
      // Look for login/auth buttons
      const authButtons = [
        'text=Sign In',
        'text=Login',
        'text=Authenticate',
        'button:has-text("Sign")',
        '.login-btn',
        '.auth-btn'
      ];

      for (const buttonSelector of authButtons) {
        const buttonCount = await page.locator(buttonSelector).count();
        if (buttonCount > 0) {
          await addAuthResult('Auth Button Found', true, `Found auth button: ${buttonSelector}`);
          
          try {
            await page.locator(buttonSelector).first().click();
            await page.waitForTimeout(2000);
            
            // Check if authentication flow started
            const currentUrl = page.url();
            if (currentUrl.includes('oauth') || currentUrl.includes('auth') || currentUrl.includes('login')) {
              await addAuthResult('Auth Flow Started', true, `Authentication flow initiated: ${currentUrl}`);
              // Note: In a real test, we'd complete the OAuth flow here
              // For now, we'll see if the app provides any test/mock authentication
            }
            
          } catch (clickError) {
            await addAuthResult('Auth Button Click', false, `Failed to click auth button: ${clickError}`);
          }
          break;
        }
      }
    } catch (error) {
      await addAuthResult('Authentication Attempt', false, `Failed to attempt authentication: ${error}`);
    }
  }

  async function extractAuthToken(page: Page) {
    try {
      // Method 1: Look for JWT token in localStorage
      const localStorageToken = await page.evaluate(() => {
        const tokenKeys = ['token', 'authToken', 'jwt', 'accessToken', 'access_token'];
        for (const key of tokenKeys) {
          const token = localStorage.getItem(key);
          if (token) return { key, token };
        }
        return null;
      });

      if (localStorageToken) {
        authToken = localStorageToken.token;
        await addAuthResult('Token Extraction', true, `Found token in localStorage: ${localStorageToken.key}`);
        return;
      }

      // Method 2: Look for token in sessionStorage
      const sessionStorageToken = await page.evaluate(() => {
        const tokenKeys = ['token', 'authToken', 'jwt', 'accessToken', 'access_token'];
        for (const key of tokenKeys) {
          const token = sessionStorage.getItem(key);
          if (token) return { key, token };
        }
        return null;
      });

      if (sessionStorageToken) {
        authToken = sessionStorageToken.token;
        await addAuthResult('Token Extraction', true, `Found token in sessionStorage: ${sessionStorageToken.key}`);
        return;
      }

      // Method 3: Look for token in cookies
      const cookies = await page.context().cookies();
      const authCookie = cookies.find(cookie => 
        cookie.name.toLowerCase().includes('token') || 
        cookie.name.toLowerCase().includes('auth') ||
        cookie.name.toLowerCase().includes('jwt')
      );

      if (authCookie) {
        authToken = authCookie.value;
        await addAuthResult('Token Extraction', true, `Found token in cookie: ${authCookie.name}`);
        return;
      }

      await addAuthResult('Token Extraction', false, 'No authentication token found');

    } catch (error) {
      await addAuthResult('Token Extraction', false, `Failed to extract token: ${error}`);
    }
  }

  async function testDeletionWithBrowserAuth(page: Page) {
    try {
      // Use the browser's existing authentication context
      const testFileName = 'browser_test_enhanced.png';
      const deleteUrl = `${API_BASE_URL}/image/enhanced/${testFileName}`;

      console.log(`\n🌐 Testing deletion with browser auth: ${deleteUrl}`);

      // Make the DELETE request using the page's request context (includes cookies/headers)
      const startTime = Date.now();
      const response = await page.request.delete(deleteUrl);
      const responseTime = Date.now() - startTime;

      let responseBody = '';
      try {
        responseBody = await response.text();
      } catch {
        responseBody = 'Could not read response body';
      }

      const success = response.status() !== 401; // Any non-401 is progress
      await addAuthResult(
        'Browser Auth Deletion',
        success,
        `Status: ${response.status()}, Body: ${responseBody.substring(0, 100)}`,
        { statusCode: response.status(), responseTime }
      );

      // If still getting 401, try to find and use any available auth headers
      if (response.status() === 401) {
        await testWithExplicitHeaders(page, testFileName);
      }

    } catch (error) {
      await addAuthResult('Browser Auth Deletion', false, `Request failed: ${error}`);
    }
  }

  async function testWithExplicitHeaders(page: Page, fileName: string) {
    try {
      // Try with common authorization headers
      const authHeaders = [
        { 'Authorization': 'Bearer test-token' },
        { 'X-API-Key': 'test-api-key' },
        { 'X-Auth-Token': 'test-token' },
      ];

      for (const headers of authHeaders) {
        const response = await page.request.delete(`${API_BASE_URL}/image/enhanced/${fileName}`, {
          headers
        });

        const success = response.status() !== 401;
        await addAuthResult(
          `Explicit Headers Test (${Object.keys(headers)[0]})`,
          success,
          `Status: ${response.status()}`,
          { statusCode: response.status() }
        );

        if (success) break; // Found working auth method
      }
    } catch (error) {
      await addAuthResult('Explicit Headers Test', false, `Failed: ${error}`);
    }
  }

  async function testDevelopmentBypass(page: Page) {
    try {
      // Test if there's a development mode that bypasses auth
      const devEndpoints = [
        '/api/dev/image/enhanced/test.png',
        '/api/admin/image/enhanced/test.png',
        '/api/internal/image/enhanced/test.png'
      ];

      for (const endpoint of devEndpoints) {
        try {
          const response = await page.request.delete(`${NGROK_BASE_URL}${endpoint}`);
          const success = response.status() !== 404; // Not found is better than auth error
          
          await addAuthResult(
            `Development Bypass (${endpoint})`,
            success,
            `Status: ${response.status()}`,
            { statusCode: response.status() }
          );
        } catch (error) {
          console.log(`   Dev endpoint ${endpoint} failed: ${error}`);
        }
      }
    } catch (error) {
      await addAuthResult('Development Bypass', false, `Failed: ${error}`);
    }
  }

  async function testMockAuthentication(page: Page) {
    try {
      // Test if there's a mock/demo user we can use
      const mockAuthEndpoints = [
        '/api/auth/demo',
        '/api/auth/test',
        '/api/auth/mock'
      ];

      for (const endpoint of mockAuthEndpoints) {
        try {
          const authResponse = await page.request.post(`${NGROK_BASE_URL}${endpoint}`, {
            data: { user: 'test', password: 'test' }
          });

          if (authResponse.ok()) {
            const authData = await authResponse.json();
            if (authData.token || authData.accessToken) {
              authToken = authData.token || authData.accessToken;
              await addAuthResult('Mock Authentication', true, `Got mock token from ${endpoint}`);
              
              // Test deletion with mock token
              await testDeletionWithTokenAuth(page);
              return;
            }
          }
        } catch (error) {
          console.log(`   Mock auth ${endpoint} failed: ${error}`);
        }
      }

      await addAuthResult('Mock Authentication', false, 'No mock authentication endpoints found');
    } catch (error) {
      await addAuthResult('Mock Authentication', false, `Failed: ${error}`);
    }
  }

  async function testDirectStorageDeletion(page: Page) {
    try {
      // Check if we can verify the deletion occurred even if the API is protected
      
      // Find an existing enhanced image
      const testImageUrl = 'https://clear-anteater-usually.ngrok-free.app/devstoreaccount1/profile-images/generated/test-user/image_enhanced.png';
      
      // Check if it exists
      const beforeResponse = await page.request.get(testImageUrl);
      const existsBefore = beforeResponse.status() === 200;
      
      await addAuthResult(
        'Direct Storage Check',
        true,
        `Test image exists before: ${existsBefore}`,
        { statusCode: beforeResponse.status() }
      );

      if (existsBefore) {
        // Try the deletion API call (will likely fail with 401, but let's see what happens to storage)
        try {
          await page.request.delete(`${API_BASE_URL}/image/enhanced/image_enhanced.png`);
        } catch {
          // Expected to fail
        }

        // Check if image still exists after deletion attempt
        await page.waitForTimeout(1000);
        const afterResponse = await page.request.get(testImageUrl);
        const existsAfter = afterResponse.status() === 200;

        await addAuthResult(
          'Storage State After Deletion',
          true,
          `Test image exists after deletion attempt: ${existsAfter}`,
          { statusCode: afterResponse.status() }
        );

        if (existsBefore && !existsAfter) {
          await addAuthResult('Deletion Success', true, 'Image was successfully deleted despite API auth issues');
        } else if (existsBefore && existsAfter) {
          await addAuthResult('Deletion Failed', false, 'Image still exists - deletion did not occur');
        }
      }

    } catch (error) {
      await addAuthResult('Direct Storage Deletion', false, `Failed: ${error}`);
    }
  }

  async function testDeletionWithTokenAuth(page: Page) {
    if (!authToken) {
      await addAuthResult('Token Auth Deletion', false, 'No auth token available');
      return;
    }

    try {
      const testFileName = 'token_test_enhanced.png';
      const response = await page.request.delete(`${API_BASE_URL}/image/enhanced/${testFileName}`, {
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        }
      });

      let responseBody = '';
      try {
        responseBody = await response.text();
      } catch {
        responseBody = 'Could not read response body';
      }

      const success = response.status() !== 401;
      await addAuthResult(
        'Token Auth Deletion',
        success,
        `Status: ${response.status()}, Body: ${responseBody.substring(0, 100)}`,
        { statusCode: response.status() }
      );

    } catch (error) {
      await addAuthResult('Token Auth Deletion', false, `Failed: ${error}`);
    }
  }

  test('should analyze the authentication configuration', async ({ page }) => {
    console.log('🔍 Analyzing authentication configuration...');

    try {
      // Check if the endpoint actually requires authentication vs. having an auth bug
      const response = await page.request.options(`${API_BASE_URL}/image/enhanced/test.png`);
      
      await addAuthResult(
        'OPTIONS Request',
        response.status() !== 401,
        `OPTIONS status: ${response.status()} - indicates if auth is properly configured`,
        { statusCode: response.status() }
      );

      // Check CORS headers
      const corsHeaders = response.headers();
      if (corsHeaders['access-control-allow-methods']) {
        const allowedMethods = corsHeaders['access-control-allow-methods'];
        await addAuthResult(
          'CORS Methods Check',
          allowedMethods.includes('DELETE'),
          `Allowed methods: ${allowedMethods}`
        );
      }

      // Check if auth is selectively applied
      const publicEndpoints = [
        '/api/health',
        '/api/styles',
        '/api/info'
      ];

      for (const endpoint of publicEndpoints) {
        try {
          const testResponse = await page.request.get(`${NGROK_BASE_URL}${endpoint}`);
          const isPublic = testResponse.status() !== 401;
          
          await addAuthResult(
            `Public Endpoint (${endpoint})`,
            true,
            `Status: ${testResponse.status()} - ${isPublic ? 'Public' : 'Protected'}`,
            { statusCode: testResponse.status() }
          );
        } catch (error) {
          console.log(`   Endpoint ${endpoint} test failed: ${error}`);
        }
      }

    } catch (error) {
      await addAuthResult('Auth Configuration Analysis', false, `Failed: ${error}`);
    }
  });
});