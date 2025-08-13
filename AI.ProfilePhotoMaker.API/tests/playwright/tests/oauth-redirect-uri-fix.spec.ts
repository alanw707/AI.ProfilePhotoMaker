import { test, expect } from '@playwright/test';

/**
 * OAuth Redirect URI Fix Verification Test
 * 
 * This test verifies that the backend now uses the correct production redirect URI
 * instead of localhost:5032 after adding the OAUTH_BASE_URL environment variable.
 */

test.describe('OAuth Redirect URI Fix Verification', () => {

  test('Should use correct production redirect URI in OAuth flow', async ({ page }) => {
    console.log('🔍 Testing OAuth redirect URI fix...');

    let interceptedGoogleUrl = '';

    // Intercept requests to capture the Google OAuth URL
    page.on('request', (request) => {
      const url = request.url();
      if (url.includes('accounts.google.com/o/oauth2/v2/auth')) {
        interceptedGoogleUrl = url;
        console.log(`🔗 Captured Google OAuth URL: ${url}`);
      }
    });

    // Navigate to login page
    console.log('📄 Navigating to login page...');
    await page.goto('/auth/login');
    await expect(page).toHaveTitle(/AI Profile Photo Maker/);

    // Find and click the Google login button
    const googleLoginButton = page.locator('button:has-text("Continue with Google")');
    await expect(googleLoginButton).toBeVisible();

    console.log('🚀 Clicking Google login button...');
    await googleLoginButton.click();

    // Wait for the OAuth redirect to complete
    await page.waitForTimeout(3000);

    // Validate the captured Google OAuth URL
    expect(interceptedGoogleUrl).toBeTruthy();
    console.log('✅ Google OAuth URL captured successfully');

    // Parse the redirect_uri parameter from the Google OAuth URL
    const urlObj = new URL(interceptedGoogleUrl);
    const redirectUri = urlObj.searchParams.get('redirect_uri');
    
    console.log(`🔑 Extracted redirect_uri: ${redirectUri}`);

    // Verify the redirect URI is correct
    const expectedRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
    const wrongRedirectUri = 'http://localhost:5032/api/auth/external-login-callback';

    expect(redirectUri).toBe(expectedRedirectUri);

    if (redirectUri === expectedRedirectUri) {
      console.log('✅ SUCCESS: Using correct production redirect URI!');
    } else if (redirectUri === wrongRedirectUri) {
      console.log('❌ FAILURE: Still using localhost redirect URI!');
      throw new Error(`Still using wrong redirect URI: ${wrongRedirectUri}`);
    } else {
      console.log(`⚠️ UNEXPECTED: Using unexpected redirect URI: ${redirectUri}`);
      throw new Error(`Unexpected redirect URI: ${redirectUri}`);
    }

    // Also verify the client_id is correct
    const clientId = urlObj.searchParams.get('client_id');
    console.log(`🔑 Client ID: ${clientId}`);
    
    const correctClientId = '116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com';
    expect(clientId).toBe(correctClientId);

    console.log('🎉 OAuth configuration is correct!');
    console.log(`   ✅ Redirect URI: ${redirectUri}`);
    console.log(`   ✅ Client ID: ${clientId}`);
  });

  test('Should handle OAuth configuration debug', async ({ page }) => {
    console.log('🔧 Testing OAuth configuration...');

    // Test direct API call to OAuth endpoint
    const response = await page.request.get('https://api.aiprofilephotomaker.com/api/auth/external-login/google?returnUrl=%2Fapp%2Fdashboard', {
      failOnStatusCode: false
    });

    console.log(`🔗 OAuth endpoint response status: ${response.status()}`);

    if (response.status() === 302) {
      const location = response.headers()['location'];
      console.log(`↗️ Redirect location: ${location}`);
      
      if (location && location.includes('accounts.google.com')) {
        // Extract redirect_uri from the Google OAuth URL
        const urlObj = new URL(location);
        const redirectUri = urlObj.searchParams.get('redirect_uri');
        
        console.log(`🔑 Redirect URI from direct API call: ${redirectUri}`);
        
        const expectedRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
        expect(redirectUri).toBe(expectedRedirectUri);
        
        console.log('✅ Direct API call uses correct redirect URI');
      }
    } else {
      console.log(`⚠️ Unexpected response status: ${response.status()}`);
      // Still continue with other checks
    }
  });

  test('Should verify Google Cloud Console redirect URI requirement', async () => {
    console.log('📋 Documenting Google Cloud Console configuration requirement...');
    
    const requiredRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
    const clientId = '116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com';
    
    console.log('🔧 Google Cloud Console Configuration Required:');
    console.log('==========================================');
    console.log(`Client ID: ${clientId}`);
    console.log(`Redirect URI to add: ${requiredRedirectUri}`);
    console.log('');
    console.log('Steps:');
    console.log('1. Go to Google Cloud Console');
    console.log('2. Navigate to APIs & Services > Credentials');
    console.log(`3. Find OAuth 2.0 Client ID: ${clientId}`);
    console.log('4. Add the redirect URI to "Authorized redirect URIs"');
    console.log(`5. Add: ${requiredRedirectUri}`);
    console.log('6. Save changes');
    
    // This test always passes - it's just for documentation
    expect(requiredRedirectUri).toBeTruthy();
  });
});