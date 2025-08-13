import { test, expect } from '@playwright/test';

test('debug production OAuth token exchange', async ({ page }) => {
  // Simulate the token exchange failure scenario
  console.log('🔍 Testing production OAuth token exchange...');
  
  // Navigate to the OAuth URL to see what Google Client ID is being used
  const response = await page.request.get('https://api.aiprofilephotomaker.com/api/auth/google-oauth-url');
  
  if (response.ok()) {
    const data = await response.json();
    console.log('✅ OAuth URL generated successfully');
    console.log('🔑 Client ID in URL:', data.authUrl?.match(/client_id=([^&]+)/)?.[1] || 'NOT_FOUND');
    console.log('🔗 Redirect URI:', decodeURIComponent(data.authUrl?.match(/redirect_uri=([^&]+)/)?.[1] || 'NOT_FOUND'));
  } else {
    console.log('❌ OAuth URL generation failed:', response.status());
    console.log('📝 Response:', await response.text());
  }

  // Test the callback with a simulated Google error to see our error handling
  const callbackResponse = await page.request.get(
    'https://api.aiprofilephotomaker.com/api/auth/external-login-callback?error=access_denied'
  );
  console.log('🔄 Callback redirect status:', callbackResponse.status());
  
  // Test direct token exchange to Google (this will fail but show us the error)
  try {
    const tokenExchangeResponse = await page.request.post('https://oauth2.googleapis.com/token', {
      form: {
        client_id: 'test_client_id',
        client_secret: 'test_secret',
        code: 'test_code',
        grant_type: 'authorization_code',
        redirect_uri: 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback'
      }
    });
    const tokenError = await tokenExchangeResponse.text();
    console.log('🔄 Google token exchange test response:', tokenExchangeResponse.status());
    console.log('📝 Google error response:', tokenError);
  } catch (error) {
    console.log('❌ Token exchange test failed:', error);
  }
});