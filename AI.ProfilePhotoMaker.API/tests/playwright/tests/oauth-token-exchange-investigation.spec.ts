import { test, expect } from '@playwright/test';

// Allow overriding BASE_URL via environment variable for local testing
const BASE_URL = process.env.BASE_URL || 'https://api.aiprofilephotomaker.com';
const FRONTEND_URL = 'https://app.aiprofilephotomaker.com';

test.describe('OAuth Token Exchange Critical Investigation', () => {
  test('Check OAuth Configuration and Credentials', async ({ request }) => {
    console.log('\n=== OAUTH CONFIGURATION CHECK ===\n');
    
    // 1. Check OAuth debug endpoint
    const debugResponse = await request.get(`${BASE_URL}/api/auth/debug/oauth-config`);
    expect(debugResponse.ok()).toBeTruthy();
    const debugData = await debugResponse.json();
    
    console.log('OAuth Configuration Debug:');
    console.log('- Config Values:', debugData.configValues);
    console.log('- Final Values:', debugData.finalValues);
    
    // Check if credentials are properly set
    const hasClientId = debugData.finalValues?.clientId && 
                       !debugData.finalValues.clientId.includes('NULL') &&
                       !debugData.finalValues.clientId.includes('EMPTY');
    const hasClientSecret = debugData.finalValues?.clientSecret && 
                           debugData.finalValues.clientSecret === 'SET';
    
    console.log('\nCredential Status:');
    console.log(`- Client ID: ${hasClientId ? 'CONFIGURED' : 'MISSING/INVALID'}`);
    console.log(`- Client Secret: ${hasClientSecret ? 'CONFIGURED' : 'MISSING/INVALID'}`);
    
    if (!hasClientId || !hasClientSecret) {
      console.error('\n❌ CRITICAL: Google OAuth credentials are not properly configured!');
      console.error('This is the root cause of token exchange failure.');
      console.error('\nRequired Actions:');
      console.error('1. Verify Google OAuth credentials in Azure Key Vault');
      console.error('2. Update Container App environment variables to use direct values instead of secretRef');
      console.error('3. Or ensure Key Vault secrets "google-client-id" and "google-client-secret" exist');
    }
    
    // 2. Test OAuth URL generation
    const oauthUrlResponse = await request.get(`${BASE_URL}/api/auth/google-oauth-url`);
    if (oauthUrlResponse.ok()) {
      const oauthData = await oauthUrlResponse.json();
      console.log('\n✅ OAuth URL generation works');
      
      // Parse and validate the auth URL
      if (oauthData.authUrl) {
        const url = new URL(oauthData.authUrl);
        const clientId = url.searchParams.get('client_id');
        console.log(`- Client ID in URL: ${clientId ? clientId.substring(0, 20) + '...' : 'MISSING'}`);
        console.log(`- Redirect URI: ${url.searchParams.get('redirect_uri')}`);
      }
    } else {
      const errorText = await oauthUrlResponse.text();
      console.error('\n❌ OAuth URL generation failed:', errorText);
    }
  });

  test('Simulate OAuth Callback with Mock Code', async ({ request }) => {
    console.log('\n=== SIMULATING OAUTH CALLBACK ===\n');
    
    // Generate a state value
    const state = Math.random().toString(36).substring(7);
    const mockCode = 'mock_authorization_code_for_testing';
    
    // First, we need to establish a session
    console.log('1. Getting OAuth URL to establish session...');
    const oauthUrlResponse = await request.get(`${BASE_URL}/api/auth/google-oauth-url`);
    
    if (oauthUrlResponse.ok()) {
      const headers = await oauthUrlResponse.headersArray();
      const cookieHeaders = headers.filter(h => h.name.toLowerCase() === 'set-cookie');
      const cookies = cookieHeaders.map(h => h.value).join('; ');
      console.log(`- Session established: ${cookies ? 'YES' : 'NO'}`);
      
      // Now attempt callback with mock code
      console.log('\n2. Calling OAuth callback endpoint...');
      const callbackUrl = `${BASE_URL}/api/auth/external-login-callback?code=${mockCode}&state=${state}`;
      
      const callbackResponse = await request.get(callbackUrl, {
        maxRedirects: 0, // Don't follow redirects
        headers: cookies ? { 'Cookie': cookies } : {},
        ignoreHTTPSErrors: true
      });
      
      const status = callbackResponse.status();
      console.log(`- Response status: ${status}`);
      
      if (status === 302 || status === 301) {
        const locationHeader = await callbackResponse.headerValue('location');
        console.log(`- Redirect location: ${locationHeader}`);
        
        // Analyze the redirect URL
        if (locationHeader) {
          try {
            const url = new URL(locationHeader, BASE_URL);
            const error = url.searchParams.get('error');
            
            if (error) {
              console.log(`\n❌ OAuth callback failed with error: ${error}`);
              
              // Provide specific diagnosis based on error
              switch(error) {
                case 'session_expired':
                  console.log('→ Session management issue - session state not persisted');
                  break;
                case 'invalid_state':
                  console.log('→ State mismatch - session state doesn\'t match callback state');
                  break;
                case 'token_exchange_failed':
                  console.log('→ Token exchange with Google failed - likely due to invalid credentials or code');
                  break;
                case 'missing_code':
                  console.log('→ Authorization code not provided in callback');
                  break;
                default:
                  console.log(`→ Unexpected error: ${error}`);
              }
            } else {
              console.log('✅ Callback processed without error (would fail at token exchange with mock code)');
            }
          } catch (urlError) {
            console.log('Could not parse redirect URL:', locationHeader);
          }
        }
      }
    }
  });

  test('Test Direct Token Exchange with Google', async ({ request }) => {
    console.log('\n=== DIRECT TOKEN EXCHANGE TEST ===\n');
    
    // First get the OAuth config to see what credentials are configured
    const configResponse = await request.get(`${BASE_URL}/api/auth/debug/oauth-config`);
    if (!configResponse.ok()) {
      console.error('Failed to get OAuth configuration');
      return;
    }
    
    const configData = await configResponse.json();
    console.log('Current OAuth Configuration:');
    console.log(JSON.stringify(configData, null, 2));
    
    // Try to make a direct request to Google's token endpoint with a fake code
    // This will fail but will show us the exact error
    const tokenEndpoint = 'https://oauth2.googleapis.com/token';
    const redirectUri = `${BASE_URL}/api/auth/external-login-callback`;
    
    const formData = new URLSearchParams({
      code: 'test_invalid_code',
      client_id: '1234567890.apps.googleusercontent.com', // Fake client ID
      client_secret: 'fake_secret',
      redirect_uri: redirectUri,
      grant_type: 'authorization_code'
    });
    
    console.log('\nTesting Google token endpoint with mock credentials...');
    console.log(`- Redirect URI: ${redirectUri}`);
    
    try {
      const tokenResponse = await request.post(tokenEndpoint, {
        data: formData.toString(),
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded'
        },
        ignoreHTTPSErrors: true
      });
      
      const status = tokenResponse.status();
      const responseData = await tokenResponse.json().catch(() => tokenResponse.text());
      
      console.log(`\nGoogle Token Endpoint Response:`);
      console.log(`- Status: ${status}`);
      console.log(`- Response:`, responseData);
      
      if (status === 400 || status === 401) {
        console.log('\n✅ Google OAuth endpoint is reachable');
        console.log('The error above is expected with fake credentials');
      }
    } catch (error) {
      console.error('Failed to reach Google token endpoint:', error);
    }
  });

  test('Check Container App Environment Configuration', async ({ request }) => {
    console.log('\n=== CONTAINER APP ENVIRONMENT CHECK ===\n');
    
    // Check authentication schemes
    const response = await request.get(`${BASE_URL}/api/auth/debug/auth-schemes`);
    if (response.ok()) {
      const data = await response.json();
      console.log('Authentication schemes configured:', JSON.stringify(data.schemes, null, 2));
      
      const hasGoogle = data.schemes?.some((s: any) => s.Name === 'Google');
      console.log(`\nGoogle OAuth scheme: ${hasGoogle ? 'REGISTERED' : 'NOT FOUND'}`);
    }
    
    // Check if the API is running and accessible
    console.log('\nAPI Health Check:');
    const healthResponse = await request.get(`${BASE_URL}/api/auth/test-redirect`, {
      maxRedirects: 0,
      ignoreHTTPSErrors: true
    });
    console.log(`- API Response Status: ${healthResponse.status()}`);
    
    if (healthResponse.status() === 302 || healthResponse.status() === 301) {
      const location = await healthResponse.headerValue('location');
      console.log(`- Redirect test successful, redirects to: ${location}`);
    }
  });

  test('Test Error Redirect Path', async ({ page }) => {
    console.log('\n=== ERROR REDIRECT PATH TEST ===\n');
    
    // Test if the frontend properly handles error redirects
    const errorUrl = `${FRONTEND_URL}/auth/login?error=token_exchange_failed`;
    console.log(`Testing error URL: ${errorUrl}`);
    
    await page.goto(errorUrl, { waitUntil: 'networkidle' });
    
    const finalUrl = page.url();
    console.log(`Final URL after navigation: ${finalUrl}`);
    
    // Check if /auth is preserved in the URL
    if (!finalUrl.includes('/auth/')) {
      console.log('⚠️ WARNING: /auth path was stripped from URL!');
      console.log('This could be due to frontend routing configuration.');
    }
    
    // Check for error message display
    const errorVisible = await page.locator('text=/error|failed|problem/i').isVisible().catch(() => false);
    console.log(`Error message displayed: ${errorVisible ? 'YES' : 'NO'}`);
    
    // Check what's actually on the page
    const pageTitle = await page.title();
    console.log(`Page title: ${pageTitle}`);
    
    // Look for login form
    const hasLoginForm = await page.locator('input[type="email"], input[type="password"]').count() > 0;
    console.log(`Login form visible: ${hasLoginForm ? 'YES' : 'NO'}`);
  });

  test('Verify OAuth Redirect URIs', async () => {
    console.log('\n=== OAUTH REDIRECT URI VERIFICATION ===\n');
    
    const expectedRedirectUri = `${BASE_URL}/api/auth/external-login-callback`;
    console.log(`Expected Redirect URI: ${expectedRedirectUri}`);
    
    console.log('\n⚠️ Manual Verification Required:');
    console.log('1. Go to Google Cloud Console');
    console.log('2. Navigate to APIs & Services > Credentials');
    console.log('3. Check OAuth 2.0 Client IDs');
    console.log('4. Verify Authorized redirect URIs includes:');
    console.log(`   - ${expectedRedirectUri}`);
    console.log('5. Also check for production domain:');
    console.log('   - https://api.aiprofilephotomaker.com/api/auth/external-login-callback');
  });
});

test.describe('Root Cause Analysis Summary', () => {
  test('Generate Investigation Report', async () => {
    console.log('\n' + '='.repeat(60));
    console.log('OAUTH TOKEN EXCHANGE FAILURE - ROOT CAUSE ANALYSIS');
    console.log('='.repeat(60) + '\n');
    
    console.log('SYMPTOMS OBSERVED:');
    console.log('1. OAuth flow initiates successfully');
    console.log('2. Google authorization completes');
    console.log('3. Callback receives valid code and state');
    console.log('4. Token exchange fails with "token_exchange_failed" error');
    console.log('5. User redirected to /login instead of /auth/login\n');
    
    console.log('LIKELY ROOT CAUSES:');
    console.log('1. PRIMARY: Google OAuth credentials not properly configured in Container App');
    console.log('   - Environment variables using secretRef instead of direct values');
    console.log('   - Key Vault secrets may not exist or be accessible');
    console.log('   - Container App revision 0000072 may not have updated env vars\n');
    
    console.log('2. SECONDARY: Frontend routing issue');
    console.log('   - /login redirects to /auth/login but may lose query parameters\n');
    
    console.log('EVIDENCE FROM CODE ANALYSIS:');
    console.log('- AuthController.cs line 237: Returns token_exchange_failed when ExchangeCodeForTokenAsync returns null');
    console.log('- ExchangeCodeForTokenAsync (lines 278-327): Makes HTTP POST to Google token endpoint');
    console.log('- GetGoogleClientSettings (lines 329-375): Tries multiple sources for credentials');
    console.log('- Container App config uses secretRef for GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET\n');
    
    console.log('VERIFICATION STEPS:');
    console.log('1. Check Azure Container App environment variables');
    console.log('2. Verify Key Vault secrets exist: google-client-id, google-client-secret');
    console.log('3. Test with direct environment variable values instead of secretRef');
    console.log('4. Verify Google Cloud Console redirect URI configuration\n');
    
    console.log('RECOMMENDED FIXES:');
    console.log('1. IMMEDIATE: Update Container App configuration:');
    console.log('   az containerapp update --name aipm-api-v1 \\');
    console.log('     --resource-group AI.ProfilePhotoMaker-Production \\');
    console.log('     --set-env-vars \\');
    console.log('       GOOGLE_CLIENT_ID="actual-client-id-value" \\');
    console.log('       GOOGLE_CLIENT_SECRET="actual-client-secret-value"');
    console.log('');
    console.log('2. OR fix Key Vault integration:');
    console.log('   - Verify managed identity has access to Key Vault');
    console.log('   - Confirm secrets exist with correct names');
    console.log('   - Check Container App has proper Key Vault references');
    console.log('');
    console.log('3. Frontend: Preserve query parameters on redirect:');
    console.log('   - Update routing to maintain error parameters when redirecting /login to /auth/login\n');
    
    console.log('TEST COMMAND:');
    console.log('npm test -- oauth-token-exchange-investigation.spec.ts');
    console.log('='.repeat(60));
  });
});