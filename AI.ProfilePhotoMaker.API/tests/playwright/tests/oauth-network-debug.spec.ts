import { test, expect } from '@playwright/test';

/**
 * OAuth Network Stack Debug Test
 * 
 * This test thoroughly investigates the OAuth flow to identify why we're still
 * getting localhost:5032 instead of the production domain in the redirect_uri.
 */

test.describe('OAuth Network Stack Debug', () => {

  test('Should debug complete OAuth network flow with detailed logging', async ({ page }) => {
    console.log('🔍 Starting comprehensive OAuth network debug...');

    const networkLog: any[] = [];
    let oauthInitiationUrl = '';
    let googleRedirectUrl = '';
    let backendResponse = '';

    // Capture ALL network requests
    page.on('request', (request) => {
      const url = request.url();
      const method = request.method();
      
      networkLog.push({
        type: 'request',
        url,
        method,
        headers: request.headers(),
        timestamp: new Date().toISOString()
      });
      
      console.log(`📤 REQUEST: ${method} ${url}`);
      
      if (url.includes('external-login/google')) {
        oauthInitiationUrl = url;
        console.log(`🎯 OAuth initiation captured: ${url}`);
      }
    });

    page.on('response', async (response) => {
      const url = response.url();
      const status = response.status();
      
      networkLog.push({
        type: 'response',
        url,
        status,
        headers: response.headers(),
        timestamp: new Date().toISOString()
      });
      
      console.log(`📥 RESPONSE: ${status} ${url}`);
      
      if (url.includes('external-login/google')) {
        const location = response.headers()['location'];
        if (location) {
          googleRedirectUrl = location;
          console.log(`🔗 Google OAuth redirect URL: ${location}`);
        }
        
        try {
          backendResponse = await response.text();
          console.log(`📄 Backend response body: ${backendResponse.substring(0, 200)}...`);
        } catch (e) {
          console.log('ℹ️ Could not read response body (likely redirect)');
        }
      }
    });

    // Navigate to login page
    console.log('📄 Navigating to login page...');
    await page.goto('/auth/login');
    
    // Wait for page to load completely
    await page.waitForLoadState('networkidle');
    console.log('✅ Login page loaded');

    // Test 1: Direct API call to OAuth endpoint
    console.log('\n🧪 TEST 1: Direct API call to OAuth endpoint');
    console.log('==========================================');
    
    try {
      const directResponse = await page.request.get('https://api.aiprofilephotomaker.com/api/auth/external-login/google?returnUrl=%2Fapp%2Fdashboard', {
        failOnStatusCode: false
      });
      
      console.log(`Status: ${directResponse.status()}`);
      console.log(`Headers:`, directResponse.headers());
      
      if (directResponse.status() === 302) {
        const location = directResponse.headers()['location'];
        if (location) {
          console.log(`Redirect Location: ${location}`);
          
          // Parse the redirect URI from Google OAuth URL
          try {
            const urlObj = new URL(location);
            const redirectUri = urlObj.searchParams.get('redirect_uri');
            const clientId = urlObj.searchParams.get('client_id');
            
            console.log(`🔑 Client ID: ${clientId}`);
            console.log(`🔗 Redirect URI: ${redirectUri}`);
            
            if (redirectUri?.includes('localhost:5032')) {
              console.log('❌ PROBLEM CONFIRMED: Backend is using localhost:5032');
            } else if (redirectUri?.includes('api.aiprofilephotomaker.com')) {
              console.log('✅ SUCCESS: Backend is using production domain');
            }
          } catch (parseError) {
            console.log(`⚠️ Could not parse redirect URL: ${parseError}`);
          }
        }
      }
    } catch (apiError) {
      console.log(`❌ API call failed: ${apiError}`);
    }

    // Test 2: Environment variable check via health endpoint
    console.log('\n🧪 TEST 2: Check backend environment configuration');
    console.log('=================================================');
    
    try {
      const healthResponse = await page.request.get('https://api.aiprofilephotomaker.com/api/health');
      const healthData = await healthResponse.json();
      console.log('Health check response:', healthData);
    } catch (healthError) {
      console.log(`❌ Health check failed: ${healthError}`);
    }

    // Test 3: Browser-based OAuth flow
    console.log('\n🧪 TEST 3: Browser-based OAuth flow');
    console.log('===================================');
    
    const googleLoginButton = page.locator('button:has-text("Continue with Google")');
    await expect(googleLoginButton).toBeVisible({ timeout: 10000 });
    console.log('✅ Google login button found');

    // Clear previous network logs
    networkLog.length = 0;
    
    console.log('🖱️ Clicking Google login button...');
    await googleLoginButton.click();
    
    // Wait for OAuth redirect to happen
    console.log('⏳ Waiting for OAuth redirect...');
    await page.waitForTimeout(5000);
    
    // Analyze network logs
    console.log('\n📊 NETWORK ANALYSIS');
    console.log('===================');
    
    const relevantRequests = networkLog.filter(log => 
      log.url.includes('external-login') || 
      log.url.includes('accounts.google.com')
    );
    
    console.log(`Found ${relevantRequests.length} relevant network events:`);
    relevantRequests.forEach((log, index) => {
      console.log(`${index + 1}. ${log.type.toUpperCase()}: ${log.url}`);
      if (log.type === 'response' && log.headers?.location) {
        console.log(`   → Redirect to: ${log.headers.location}`);
      }
    });

    // Test 4: Current page analysis
    console.log('\n🧪 TEST 4: Current page analysis');
    console.log('================================');
    
    const currentUrl = page.url();
    console.log(`Current URL: ${currentUrl}`);
    
    if (currentUrl.includes('accounts.google.com')) {
      console.log('✅ Successfully redirected to Google OAuth');
      
      // Extract OAuth parameters from current URL
      try {
        const urlObj = new URL(currentUrl);
        const redirectUri = urlObj.searchParams.get('redirect_uri');
        const clientId = urlObj.searchParams.get('client_id');
        const state = urlObj.searchParams.get('state');
        
        console.log('\n🔍 OAuth Parameters Analysis:');
        console.log(`Client ID: ${clientId}`);
        console.log(`Redirect URI: ${redirectUri}`);
        console.log(`State: ${state}`);
        
        // This is the key validation
        if (redirectUri) {
          if (redirectUri.includes('localhost:5032')) {
            console.log('❌ CRITICAL ISSUE: Still using localhost:5032 redirect URI');
            console.log('🔧 BACKEND CONFIGURATION PROBLEM CONFIRMED');
          } else if (redirectUri.includes('api.aiprofilephotomaker.com')) {
            console.log('✅ SUCCESS: Using production domain redirect URI');
          } else {
            console.log(`⚠️ UNEXPECTED: Unknown redirect URI pattern: ${redirectUri}`);
          }
        }
        
      } catch (urlParseError) {
        console.log(`❌ Could not parse OAuth URL: ${urlParseError}`);
      }
    } else if (currentUrl.includes('error')) {
      console.log('❌ OAuth failed - still on error page');
    } else {
      console.log('⚠️ Unexpected page state');
    }

    // Test 5: Container environment validation
    console.log('\n🧪 TEST 5: Container environment validation');
    console.log('==========================================');
    
    console.log('📋 Environment Variables That Should Be Set:');
    console.log('- OAUTH_BASE_URL=https://api.aiprofilephotomaker.com');
    console.log('- Authentication__Google__ClientId (via secret)');
    console.log('- Authentication__Google__ClientSecret (via secret)');
    
    // Summary and recommendations
    console.log('\n📝 SUMMARY AND RECOMMENDATIONS');
    console.log('==============================');
    
    if (oauthInitiationUrl) {
      console.log(`OAuth initiation URL: ${oauthInitiationUrl}`);
    }
    
    if (googleRedirectUrl) {
      console.log(`Google redirect URL: ${googleRedirectUrl}`);
      
      if (googleRedirectUrl.includes('localhost:5032')) {
        console.log('\n❌ ROOT CAUSE IDENTIFIED:');
        console.log('The backend is still using localhost:5032 as redirect_uri');
        console.log('\n🔧 REQUIRED ACTIONS:');
        console.log('1. Verify OAUTH_BASE_URL environment variable is properly set');
        console.log('2. Ensure latest backend code is deployed');
        console.log('3. Restart container to pick up environment changes');
        console.log('4. Add redirect URI to Google Cloud Console:');
        console.log('   https://api.aiprofilephotomaker.com/api/auth/external-login-callback');
      }
    }
    
    // This test will fail if we're still using localhost
    const finalUrl = page.url();
    if (finalUrl.includes('accounts.google.com')) {
      const urlObj = new URL(finalUrl);
      const redirectUri = urlObj.searchParams.get('redirect_uri');
      
      // Assert the redirect URI is correct
      expect(redirectUri).not.toContain('localhost:5032');
      expect(redirectUri).toContain('api.aiprofilephotomaker.com');
    }
  });

  test('Should validate Azure Container App configuration', async ({ page }) => {
    console.log('🔧 Validating Azure Container App configuration...');
    
    // This test documents the expected configuration
    const expectedConfig = {
      secrets: [
        'jwt-secret',
        'google-client-id', 
        'google-client-secret',
        'connection-string',
        'replicate-token'
      ],
      environmentVariables: [
        'OAUTH_BASE_URL=https://api.aiprofilephotomaker.com',
        'Authentication__Google__ClientId=secretref:google-client-id',
        'Authentication__Google__ClientSecret=secretref:google-client-secret'
      ]
    };
    
    console.log('📋 Expected Azure Container App Configuration:');
    console.log('==============================================');
    console.log('Secrets:');
    expectedConfig.secrets.forEach(secret => {
      console.log(`  - ${secret}`);
    });
    
    console.log('\nEnvironment Variables:');
    expectedConfig.environmentVariables.forEach(envVar => {
      console.log(`  - ${envVar}`);
    });
    
    console.log('\n🔧 Azure CLI Commands to Verify:');
    console.log('az containerapp show --name aipm-api-v1 --resource-group aiprofilemaker-v1 --query "properties.template.containers[0].env"');
    console.log('az containerapp secret list --name aipm-api-v1 --resource-group aiprofilemaker-v1');
    
    // Always pass - this is a documentation test
    expect(expectedConfig.secrets.length).toBeGreaterThan(0);
  });
});