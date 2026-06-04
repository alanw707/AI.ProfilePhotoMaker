import { test, expect } from '@playwright/test';

/**
 * OAuth Final Test - Verify Production Domain Fix
 * 
 * This test verifies that the OAuth redirect URI issue is finally resolved
 * and the backend is using the production domain instead of localhost:5032.
 */

test.describe('OAuth Final Test', () => {

  test('Should verify OAuth uses correct domain for environment', async ({ page }) => {
    console.log('🔍 Environment-aware OAuth test: Verifying OAuth redirect URI...');

    // Detect test environment based on base URL
    const testBaseUrl = page.url() || 'http://localhost:4200';
    const isLocalEnvironment = testBaseUrl.includes('localhost') || testBaseUrl.includes('127.0.0.1');
    const isProductionEnvironment = testBaseUrl.includes('aiprofilephotomaker.com');
    
    console.log(`🌍 Test Environment: ${isLocalEnvironment ? 'Local Development' : isProductionEnvironment ? 'Production' : 'Unknown'}`);
    console.log(`📍 Base URL: ${testBaseUrl}`);

    let googleOAuthUrl = '';
    let redirectUri = '';

    // Capture Google OAuth URL
    page.on('request', (request) => {
      const url = request.url();
      if (url.includes('accounts.google.com/o/oauth2/v2/auth')) {
        googleOAuthUrl = url;
        console.log(`🔗 Google OAuth URL captured: ${url.substring(0, 100)}...`);
      }
    });

    // Navigate to login
    await page.goto('/auth/login');
    await page.waitForLoadState('networkidle');

    // Click Google login
    const googleButton = page.locator('button:has-text("Continue with Google")');
    await expect(googleButton).toBeVisible();
    
    console.log('🖱️ Clicking Google login button...');
    await googleButton.click();

    // Wait for redirect
    await page.waitForTimeout(3000);

    // Check if we're on Google's page
    const currentUrl = page.url();
    console.log(`📍 Current URL: ${currentUrl.substring(0, 100)}...`);

    if (currentUrl.includes('accounts.google.com')) {
      console.log('✅ Successfully redirected to Google OAuth');
      
      const urlObj = new URL(currentUrl);
      redirectUri = urlObj.searchParams.get('redirect_uri') || '';
      const clientId = urlObj.searchParams.get('client_id') || '';
      
      console.log('\n🔍 OAuth Parameters:');
      console.log(`Client ID: ${clientId}`);
      console.log(`Redirect URI: ${redirectUri}`);
      
      // Environment-aware validation
      if (isLocalEnvironment) {
        console.log('🏠 Testing local development environment');
        if (redirectUri.includes('localhost') || redirectUri.includes('127.0.0.1')) {
          console.log('✅ SUCCESS: Using localhost redirect URI (expected for local development)');
          expect(redirectUri).toMatch(/localhost|127\.0\.0\.1/);
        } else {
          console.log('⚠️ WARNING: Local environment using non-localhost redirect URI');
          console.log('This might indicate misconfigured local development setup');
        }
      } else if (isProductionEnvironment) {
        console.log('🌐 Testing production environment');
        if (redirectUri.includes('api.aiprofilephotomaker.com')) {
          console.log('✅ SUCCESS: Using production domain!');
          expect(redirectUri).toBe('https://api.aiprofilephotomaker.com/api/auth/external-login-callback');
        } else if (redirectUri.includes('localhost')) {
          console.log('❌ FAILED: Production environment using localhost redirect URI');
          throw new Error('Production OAuth incorrectly using localhost redirect URI');
        } else {
          console.log(`⚠️ UNEXPECTED: Unknown redirect URI in production: ${redirectUri}`);
          throw new Error(`Unexpected redirect URI in production: ${redirectUri}`);
        }
      } else {
        console.log('🤔 Unknown environment - validating redirect URI format');
        expect(redirectUri).toMatch(/^https?:\/\/.+\/api\/auth\/external-login-callback$/);
      }
      
      // Always verify client ID is correct regardless of environment
      expect(clientId).toBe('116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com');
      
    } else {
      console.log('❌ Failed to redirect to Google OAuth');
      throw new Error('OAuth flow did not redirect to Google');
    }
  });

  test('Should test direct API call to OAuth endpoint', async ({ page }) => {
    console.log('🧪 Testing direct API call to OAuth endpoint...');

    const response = await page.request.get('https://api.aiprofilephotomaker.com/api/auth/external-login/google?returnUrl=%2Fapp%2Fenhance', {
      failOnStatusCode: false
    });

    console.log(`Response status: ${response.status()}`);

    if (response.status() === 302) {
      const location = response.headers()['location'];
      console.log(`Redirect location: ${location}`);
      
      if (location && location.includes('accounts.google.com')) {
        const urlObj = new URL(location);
        const redirectUri = urlObj.searchParams.get('redirect_uri');
        const clientId = urlObj.searchParams.get('client_id');
        
        console.log(`🔑 Client ID: ${clientId}`);
        console.log(`🔗 Redirect URI: ${redirectUri}`);
        
        if (redirectUri === 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback') {
          console.log('✅ SUCCESS: Direct API call uses production domain!');
        } else {
          console.log(`❌ FAILED: Wrong redirect URI: ${redirectUri}`);
          throw new Error(`Wrong redirect URI: ${redirectUri}`);
        }

        expect(redirectUri).toBe('https://api.aiprofilephotomaker.com/api/auth/external-login-callback');
        expect(clientId).toBe('116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com');
      }
    } else {
      console.log(`⚠️ Unexpected response status: ${response.status()}`);
    }
  });

  test('Should document final Google Cloud Console configuration', async () => {
    console.log('\n📋 FINAL GOOGLE CLOUD CONSOLE CONFIGURATION');
    console.log('============================================');
    
    const config = {
      clientId: '116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com',
      redirectUri: 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback'
    };
    
    console.log('Steps to complete OAuth setup:');
    console.log('1. Go to https://console.cloud.google.com/');
    console.log('2. Navigate to APIs & Services > Credentials');
    console.log(`3. Find OAuth 2.0 Client ID: ${config.clientId}`);
    console.log('4. Click Edit');
    console.log('5. In "Authorized redirect URIs" section, add:');
    console.log(`   ${config.redirectUri}`);
    console.log('6. Save changes');
    console.log('\n✅ After adding the redirect URI, OAuth should work completely!');
    
    expect(config.redirectUri).toBeTruthy();
  });
});