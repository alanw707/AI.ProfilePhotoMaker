import { test, expect } from '@playwright/test';

const API_BASE_URL = process.env.API_BASE_URL || 'https://api.aiprofilephotomaker.com';

test('should debug token exchange failure', async ({ request, page }) => {
    // Test direct access to the callback endpoint with sample parameters
    console.log('Testing OAuth callback endpoint with debug parameters...');
    
    // First, let's check if the endpoint exists and responds
    const callbackUrl = `${API_BASE_URL}/api/auth/external-login-callback`;
    
    // Test with missing parameters (should redirect with missing_code error)
    const missingCodeResponse = await request.get(callbackUrl + '?state=test-state');
    expect(missingCodeResponse.status()).toBe(302);
    
    const location = missingCodeResponse.headers()['location'];
    expect(location).toContain('error=missing_code');
    console.log('Missing code test passed:', location);
    
    // Test with invalid state (should redirect with invalid_state error)  
    const invalidStateResponse = await request.get(callbackUrl + '?code=test-code&state=invalid-state');
    expect(invalidStateResponse.status()).toBe(302);
    
    const invalidStateLocation = invalidStateResponse.headers()['location'];
    expect(invalidStateLocation).toContain('error=invalid_state');
    console.log('Invalid state test passed:', invalidStateLocation);
    
    // Test token exchange with a real OAuth flow simulation
    await page.goto(`${API_BASE_URL}/api/auth/external-login/google`);
    
    // Check if we get redirected to Google
    await page.waitForLoadState('networkidle');
    const currentUrl = page.url();
    
    if (currentUrl.includes('accounts.google.com')) {
      console.log('✓ Successfully redirected to Google OAuth');
      
      // Extract the OAuth parameters from the URL
      const url = new URL(currentUrl);
      const clientId = url.searchParams.get('client_id');
      const redirectUri = url.searchParams.get('redirect_uri');
      const scope = url.searchParams.get('scope');
      const state = url.searchParams.get('state');
      
      console.log('OAuth Parameters:');
      console.log('  Client ID:', clientId?.substring(0, 20) + '...');
      console.log('  Redirect URI:', redirectUri);
      console.log('  Scope:', scope);
      console.log('  State:', state);
      
      // Verify the redirect URI is correct
      expect(redirectUri).toBe('https://api.aiprofilephotomaker.com/api/auth/external-login-callback');
      
      // Test simulated callback with invalid code to trigger token_exchange_failed
      const simulatedCallback = `${API_BASE_URL}/api/auth/external-login-callback?code=invalid_test_code&state=${state}`;
      
      // Navigate directly to callback with invalid code
      await page.goto(simulatedCallback);
      await page.waitForLoadState('networkidle');
      
      const finalUrl = page.url();
      console.log('Final URL after callback:', finalUrl);
      
      // Should redirect to login with token_exchange_failed error
      expect(finalUrl).toContain('error=token_exchange_failed');
      
    } else {
      console.log('⚠️  Did not redirect to Google OAuth. Current URL:', currentUrl);
      
      // Check if there's an error response
      if (currentUrl.includes('error=')) {
        const url = new URL(currentUrl);
        const error = url.searchParams.get('error');
        console.log('OAuth error:', error);
      }
    }
});

test('should test direct token exchange endpoint', async ({ request }) => {
    // Test the token exchange method directly by inspecting its behavior
    console.log('Testing direct token exchange with Google OAuth API...');
    
    // Simulate what happens in ExchangeCodeForTokenAsync
    const tokenEndpoint = 'https://oauth2.googleapis.com/token';
    
    // Test with invalid authorization code (this should fail and return null)
    const formData = new URLSearchParams({
      client_id: 'invalid_client_id',
      client_secret: 'invalid_client_secret', 
      code: 'invalid_authorization_code',
      grant_type: 'authorization_code',
      redirect_uri: 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback'
    });
    
    const tokenResponse = await request.post(tokenEndpoint, {
      data: formData.toString(),
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded'
      }
    });
    
    console.log('Token exchange response status:', tokenResponse.status());
    
    if (!tokenResponse.ok()) {
      const errorText = await tokenResponse.text();
      console.log('Token exchange error response:', errorText);
      
      // This confirms why ExchangeCodeForTokenAsync returns null
      // Google returns 400 Bad Request for invalid credentials/code
      expect(tokenResponse.status()).toBe(400);
    }
});