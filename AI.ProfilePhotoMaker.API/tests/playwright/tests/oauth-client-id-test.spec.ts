import { test, expect } from '@playwright/test';

/**
 * OAuth Client ID Verification Test
 * 
 * This test verifies that the backend is using the correct Google OAuth Client ID
 * after the Azure Container App environment variables were updated.
 */

test.describe('OAuth Client ID Verification', () => {

  test('Should use correct Google Client ID in OAuth flow', async ({ page }) => {
    console.log('🔍 Testing OAuth Client ID in production environment...');

    let oauthRedirectUrl = '';
    let googleClientId = '';

    // Intercept requests to capture the OAuth redirect URL
    page.on('request', (request) => {
      const url = request.url();
      if (url.includes('external-login/google')) {
        oauthRedirectUrl = url;
        console.log(`🔗 Captured OAuth URL: ${url}`);
      }
      
      // Capture Google OAuth redirect to extract client_id
      if (url.includes('accounts.google.com/oauth2/auth')) {
        const urlObj = new URL(url);
        googleClientId = urlObj.searchParams.get('client_id') || '';
        console.log(`🔑 Extracted Google Client ID: ${googleClientId}`);
      }
    });

    // Navigate to login page
    console.log('📄 Navigating to login page...');
    await page.goto('/auth/login');
    await expect(page).toHaveTitle(/AI Profile Photo Maker/);

    // Find and click the Google login button
    console.log('🖱️ Looking for Google login button...');
    const googleLoginButton = page.locator('button:has-text("Continue with Google")');
    await expect(googleLoginButton).toBeVisible();

    console.log('🚀 Clicking Google login button...');
    
    // Click the button to trigger OAuth flow
    // This should redirect to our backend, then to Google
    await googleLoginButton.click();

    // Wait for the OAuth redirect chain to start
    await page.waitForTimeout(5000);

    // Validate the results
    console.log('📊 Analyzing OAuth flow results...');
    
    expect(oauthRedirectUrl).toBeTruthy();
    console.log(`✅ OAuth URL captured: ${oauthRedirectUrl}`);

    // Check if we got redirected to Google and captured the client_id
    if (googleClientId) {
      console.log(`🔑 Google Client ID found: ${googleClientId}`);
      
      // Verify it's the correct Client ID (not the old one)
      const correctClientId = '116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com';
      const wrongClientId = '331984288023-lh1upthod06meoko58g7hn9d7h68l311.apps.googleusercontent.com';
      
      if (googleClientId === correctClientId) {
        console.log('✅ SUCCESS: Using correct Google Client ID!');
      } else if (googleClientId === wrongClientId) {
        console.log('❌ FAILURE: Still using old/wrong Google Client ID!');
        throw new Error(`Still using wrong Client ID: ${wrongClientId}`);
      } else {
        console.log(`⚠️ UNKNOWN: Using unexpected Client ID: ${googleClientId}`);
        throw new Error(`Unexpected Client ID: ${googleClientId}`);
      }

      expect(googleClientId).toBe(correctClientId);
    } else {
      console.log('⏳ OAuth flow may not have completed redirect to Google yet...');
      console.log('🔍 Let\'s check if we\'re on Google\'s page...');
      
      // Check current URL to see if we're redirected to Google
      const currentUrl = page.url();
      console.log(`📍 Current URL: ${currentUrl}`);
      
      if (currentUrl.includes('accounts.google.com')) {
        // Extract client_id from current URL
        const urlObj = new URL(currentUrl);
        const clientIdFromUrl = urlObj.searchParams.get('client_id');
        
        if (clientIdFromUrl) {
          console.log(`🔑 Client ID from URL: ${clientIdFromUrl}`);
          
          const correctClientId = '116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com';
          expect(clientIdFromUrl).toBe(correctClientId);
          console.log('✅ SUCCESS: Correct Client ID found in Google OAuth URL!');
        }
      }
    }
  });

  test('Should verify backend configuration debug endpoint', async ({ page }) => {
    console.log('🔧 Testing backend configuration...');

    // Try to access a simple API endpoint to verify backend is running
    const response = await page.request.get('https://api.aiprofilephotomaker.com/api/health');
    
    expect(response.status()).toBe(200);
    const healthData = await response.json();
    console.log('🏥 Health check:', healthData);

    // Test if we can access the OAuth endpoint directly
    const oauthResponse = await page.request.get('https://api.aiprofilephotomaker.com/api/auth/external-login/google?returnUrl=%2Fapp%2Fdashboard', {
      failOnStatusCode: false
    });

    console.log(`🔗 OAuth endpoint status: ${oauthResponse.status()}`);
    
    if (oauthResponse.status() === 302) {
      const location = oauthResponse.headers()['location'];
      console.log(`↗️ Redirect location: ${location}`);
      
      if (location && location.includes('accounts.google.com')) {
        // Extract client_id from redirect URL
        const urlObj = new URL(location);
        const clientId = urlObj.searchParams.get('client_id');
        
        if (clientId) {
          console.log(`🔑 Client ID from redirect: ${clientId}`);
          const correctClientId = '116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com';
          
          if (clientId === correctClientId) {
            console.log('✅ SUCCESS: Backend is using correct Client ID!');
          } else {
            console.log(`❌ FAILURE: Backend is using wrong Client ID: ${clientId}`);
            throw new Error(`Wrong Client ID: ${clientId}`);
          }
          
          expect(clientId).toBe(correctClientId);
        }
      }
    }
  });
});