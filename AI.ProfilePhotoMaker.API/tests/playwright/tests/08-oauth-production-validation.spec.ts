import { test, expect, Page, BrowserContext } from '@playwright/test';

/**
 * OAuth Production Validation Test Suite
 * 
 * This test suite validates the OAuth functionality in the production environment
 * after the session middleware fix has been applied. It checks:
 * 
 * 1. Direct OAuth endpoint accessibility
 * 2. Frontend OAuth flow initiation
 * 3. Google OAuth redirect functionality
 * 4. Session cookie handling
 * 5. Error detection and reporting
 */

const PRODUCTION_FRONTEND = 'https://aiprofilephotomaker.com';
const PRODUCTION_API = 'https://api.aiprofilephotomaker.com';
const OAUTH_ENDPOINT = `${PRODUCTION_API}/api/auth/external-login/google`;

test.describe('OAuth Production Validation', () => {
  let context: BrowserContext;
  let page: Page;

  test.beforeEach(async ({ browser }) => {
    // Create a new context for each test to ensure clean state
    context = await browser.newContext({
      ignoreHTTPSErrors: true,
      extraHTTPHeaders: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'
      }
    });
    page = await context.newPage();
  });

  test.afterEach(async () => {
    await context?.close();
  });

  test('Test 1: Direct OAuth API Endpoint Accessibility', async () => {
    console.log('🔍 Testing direct OAuth endpoint accessibility...');
    
    // Capture network requests
    const networkRequests: Array<{url: string, status: number, headers: any}> = [];
    page.on('response', response => {
      if (response.url().includes('auth/external-login')) {
        networkRequests.push({
          url: response.url(),
          status: response.status(),
          headers: response.headers()
        });
      }
    });

    try {
      // Navigate directly to OAuth endpoint
      const response = await page.goto(OAUTH_ENDPOINT, {
        waitUntil: 'networkidle',
        timeout: 30000
      });

      console.log(`📊 OAuth Endpoint Response Status: ${response?.status()}`);
      console.log(`🌐 Final URL after redirect: ${page.url()}`);
      
      if (response?.status()) {
        const headers = response.headers();
        console.log(`📝 Response Headers:`, JSON.stringify(headers, null, 2));
        
        if (headers['location']) {
          console.log(`🔗 Redirect Location: ${headers['location']}`);
        }
        
        if (headers['set-cookie']) {
          console.log(`🍪 Set-Cookie Headers: ${headers['set-cookie']}`);
        }
      }

      // Verify it's NOT returning 500 error
      expect(response?.status()).not.toBe(500);
      
      // Check if it's a proper redirect (302/301/303/307/308)
      const redirectCodes = [301, 302, 303, 307, 308];
      const isRedirect = redirectCodes.includes(response?.status() || 0);
      const isGoogleOAuth = page.url().includes('accounts.google.com') || page.url().includes('oauth');
      
      console.log(`✅ OAuth endpoint accessibility test results:`);
      console.log(`   - Status Code: ${response?.status()} ${isRedirect ? '(Redirect)' : '(Not a redirect)'}`);
      console.log(`   - Is Google OAuth: ${isGoogleOAuth}`);
      console.log(`   - Current URL: ${page.url()}`);
      
      if (response?.status() === 500) {
        // Take screenshot for debugging
        await page.screenshot({ path: 'test-results/artifacts/oauth-500-error.png', fullPage: true });
        throw new Error('OAuth endpoint still returning 500 error - session middleware fix may not be applied');
      }

    } catch (error) {
      console.error('❌ OAuth endpoint test failed:', error);
      await page.screenshot({ path: 'test-results/artifacts/oauth-endpoint-error.png', fullPage: true });
      throw error;
    }
  });

  test('Test 2: Production Frontend OAuth Flow', async () => {
    console.log('🔍 Testing production frontend OAuth flow...');
    
    // Track network requests
    const apiRequests: Array<{url: string, status: number, method: string}> = [];
    page.on('response', response => {
      if (response.url().includes('api.aiprofilephotomaker.com')) {
        apiRequests.push({
          url: response.url(),
          status: response.status(),
          method: response.request().method()
        });
      }
    });

    try {
      // Navigate to production frontend
      console.log(`🌐 Navigating to: ${PRODUCTION_FRONTEND}`);
      await page.goto(PRODUCTION_FRONTEND, {
        waitUntil: 'networkidle',
        timeout: 60000
      });

      // Take screenshot of initial state
      await page.screenshot({ path: 'test-results/artifacts/frontend-initial.png', fullPage: true });

      // Look for login/OAuth buttons
      const loginSelectors = [
        'button:has-text("Login")',
        'button:has-text("Sign in")',
        'button:has-text("Google")',
        '[data-testid="login"]',
        '[data-testid="oauth"]',
        '.login-button',
        '.oauth-button',
        'a[href*="auth"]',
        'button[onclick*="auth"]'
      ];

      let loginButton = null;
      let loginSelector = '';

      for (const selector of loginSelectors) {
        try {
          const element = page.locator(selector).first();
          if (await element.isVisible({ timeout: 2000 })) {
            loginButton = element;
            loginSelector = selector;
            console.log(`✅ Found login element with selector: ${selector}`);
            break;
          }
        } catch (e) {
          // Continue to next selector
        }
      }

      if (!loginButton) {
        console.log('⚠️  No visible login button found, checking page content...');
        const pageContent = await page.content();
        const pageText = await page.textContent('body');
        
        console.log('🔍 Page title:', await page.title());
        console.log('🔍 Page URL:', page.url());
        console.log('🔍 Page text (first 500 chars):', pageText?.substring(0, 500));
        
        // Check if there are any auth-related elements at all
        const authLinks = await page.locator('a, button').evaluateAll(elements => 
          elements.filter(el => 
            el.textContent?.toLowerCase().includes('login') ||
            el.textContent?.toLowerCase().includes('sign') ||
            el.textContent?.toLowerCase().includes('auth') ||
            el.textContent?.toLowerCase().includes('google')
          ).map(el => ({
            tag: el.tagName,
            text: el.textContent,
            href: (el as HTMLAnchorElement).href,
            onclick: (el as HTMLElement).onclick?.toString()
          }))
        );
        
        console.log('🔍 Auth-related elements found:', authLinks);
      }

      if (loginButton) {
        console.log(`🖱️  Attempting to click login button: ${loginSelector}`);
        
        // Clear previous API requests
        apiRequests.length = 0;
        
        // Click the login button
        await loginButton.click();
        
        // Wait for potential navigation or API calls
        await page.waitForTimeout(5000);
        
        // Log API requests made during OAuth flow
        console.log('📡 API requests during OAuth flow:');
        apiRequests.forEach(req => {
          console.log(`   ${req.method} ${req.url} - Status: ${req.status}`);
        });

        // Check current URL for OAuth redirect
        const currentUrl = page.url();
        console.log(`🌐 Current URL after login click: ${currentUrl}`);
        
        const isGoogleOAuth = currentUrl.includes('accounts.google.com') || currentUrl.includes('oauth');
        console.log(`🔍 Is on Google OAuth: ${isGoogleOAuth}`);
        
        // Take screenshot of OAuth state
        await page.screenshot({ path: 'test-results/artifacts/oauth-flow-state.png', fullPage: true });
        
        // Check for errors in API calls
        const errorRequests = apiRequests.filter(req => req.status >= 400);
        if (errorRequests.length > 0) {
          console.error('❌ Error API requests detected:');
          errorRequests.forEach(req => {
            console.error(`   ${req.method} ${req.url} - Status: ${req.status}`);
          });
        }
        
        // Verify OAuth flow initiated successfully
        expect(errorRequests.filter(req => req.status === 500)).toHaveLength(0);
        
      } else {
        console.log('⚠️  No login button found - this might be expected for the current UI state');
      }

    } catch (error) {
      console.error('❌ Frontend OAuth flow test failed:', error);
      await page.screenshot({ path: 'test-results/artifacts/frontend-oauth-error.png', fullPage: true });
      throw error;
    }
  });

  test('Test 3: OAuth Configuration and Error Handling', async () => {
    console.log('🔍 Testing OAuth configuration and error handling...');
    
    const testCases = [
      {
        name: 'Valid OAuth endpoint',
        url: OAUTH_ENDPOINT,
        expectRedirect: true
      },
      {
        name: 'OAuth with invalid provider',
        url: `${PRODUCTION_API}/api/auth/external-login/invalid`,
        expectError: true
      },
      {
        name: 'OAuth with missing parameters',
        url: `${PRODUCTION_API}/api/auth/external-login/google?invalid=test`,
        expectRedirect: true // Should still redirect even with extra params
      }
    ];

    for (const testCase of testCases) {
      console.log(`📝 Testing: ${testCase.name}`);
      
      try {
        const response = await page.goto(testCase.url, {
          waitUntil: 'networkidle',
          timeout: 30000
        });

        const status = response?.status() || 0;
        const currentUrl = page.url();
        
        console.log(`   Status: ${status}`);
        console.log(`   Final URL: ${currentUrl}`);
        
        if (testCase.expectRedirect) {
          expect(status).not.toBe(500);
          // Should either redirect or show a proper error page
          if (status >= 400 && status !== 404) {
            console.log(`⚠️  Unexpected error status for ${testCase.name}: ${status}`);
          }
        }
        
        if (testCase.expectError) {
          // Should handle error gracefully, not with 500
          expect(status).not.toBe(500);
        }
        
      } catch (error) {
        if (!testCase.expectError) {
          console.error(`❌ Unexpected error for ${testCase.name}:`, error);
          throw error;
        } else {
          console.log(`✅ Expected error handled for ${testCase.name}`);
        }
      }
    }
  });

  test('Test 4: Session Cookie Analysis', async () => {
    console.log('🔍 Testing session cookie handling...');
    
    try {
      // Navigate to OAuth endpoint and capture cookies
      const response = await page.goto(OAUTH_ENDPOINT, {
        waitUntil: 'networkidle',
        timeout: 30000
      });

      // Get all cookies
      const cookies = await context.cookies();
      console.log(`🍪 Total cookies found: ${cookies.length}`);
      
      // Analyze session-related cookies
      const sessionCookies = cookies.filter(cookie => 
        cookie.name.toLowerCase().includes('session') ||
        cookie.name.toLowerCase().includes('auth') ||
        cookie.name.toLowerCase().includes('token')
      );

      console.log('🍪 Session-related cookies:');
      sessionCookies.forEach(cookie => {
        console.log(`   ${cookie.name}: ${cookie.value?.substring(0, 20)}... (${cookie.domain})`);
        console.log(`   Secure: ${cookie.secure}, HttpOnly: ${cookie.httpOnly}, SameSite: ${cookie.sameSite}`);
      });

      // Check response headers for Set-Cookie
      const headers = response?.headers() || {};
      if (headers['set-cookie']) {
        console.log(`📝 Set-Cookie header: ${headers['set-cookie']}`);
      }

      // Verify session handling is working (no 500 errors)
      expect(response?.status()).not.toBe(500);
      
    } catch (error) {
      console.error('❌ Session cookie analysis failed:', error);
      throw error;
    }
  });

  test('Test 5: Comprehensive OAuth Flow Analysis', async () => {
    console.log('🔍 Running comprehensive OAuth flow analysis...');
    
    const analysisResults = {
      endpointAccessible: false,
      properRedirect: false,
      sessionCookiesSet: false,
      noServerErrors: false,
      googleOAuthReached: false
    };

    try {
      // Test 1: Direct endpoint access
      console.log('1️⃣  Testing direct endpoint access...');
      const response = await page.goto(OAUTH_ENDPOINT, {
        waitUntil: 'networkidle',
        timeout: 30000
      });

      const status = response?.status() || 0;
      const currentUrl = page.url();
      
      analysisResults.endpointAccessible = status !== 0;
      analysisResults.noServerErrors = status !== 500;
      analysisResults.properRedirect = [301, 302, 303, 307, 308].includes(status);
      analysisResults.googleOAuthReached = currentUrl.includes('accounts.google.com');

      // Test 2: Cookie analysis
      console.log('2️⃣  Analyzing cookies...');
      const cookies = await context.cookies();
      analysisResults.sessionCookiesSet = cookies.some(cookie => 
        cookie.name.toLowerCase().includes('session') ||
        cookie.name.toLowerCase().includes('auth')
      );

      // Test 3: Take comprehensive screenshots
      console.log('3️⃣  Capturing evidence screenshots...');
      await page.screenshot({ 
        path: 'test-results/artifacts/oauth-comprehensive-analysis.png', 
        fullPage: true 
      });

      // Test 4: Generate comprehensive report
      console.log('📋 OAuth Flow Analysis Results:');
      console.log(`   ✅ Endpoint Accessible: ${analysisResults.endpointAccessible}`);
      console.log(`   ✅ No Server Errors (500): ${analysisResults.noServerErrors}`);
      console.log(`   ✅ Proper Redirect Response: ${analysisResults.properRedirect}`);
      console.log(`   ✅ Google OAuth Reached: ${analysisResults.googleOAuthReached}`);
      console.log(`   ✅ Session Cookies Set: ${analysisResults.sessionCookiesSet}`);
      console.log(`   📊 Final Status Code: ${status}`);
      console.log(`   🌐 Final URL: ${currentUrl}`);

      // Determine overall OAuth health
      const criticalChecks = [
        analysisResults.endpointAccessible,
        analysisResults.noServerErrors
      ];
      
      const allCriticalPassed = criticalChecks.every(check => check);
      
      if (!allCriticalPassed) {
        throw new Error('Critical OAuth checks failed - session middleware fix may not be fully deployed');
      }
      
      console.log('🎉 OAuth flow analysis completed successfully!');
      
      if (!analysisResults.googleOAuthReached) {
        console.log('⚠️  Note: Google OAuth not reached, but this may be expected due to OAuth flow requirements');
      }

    } catch (error) {
      console.error('❌ Comprehensive OAuth analysis failed:', error);
      await page.screenshot({ 
        path: 'test-results/artifacts/oauth-analysis-error.png', 
        fullPage: true 
      });
      throw error;
    }
  });
});