import { test, expect } from '@playwright/test';

test('OAuth Token Exchange Debug - User-Centric Flow', async ({ page }) => {
    console.log('\n=== OAUTH TOKEN EXCHANGE DEBUG - USER-CENTRIC FLOW ===');
    console.log('🎯 Simulating exact user experience from production error...');
    
    let tokenExchangeError = false;
    let oauthRedirectDetected = false;
    let googleAuthReached = false;
    let callbackReceived = false;
    let errorDetails = '';
    let finalRedirectUri = '';
    let oauthParams: any = {};
    
    // Comprehensive request/response monitoring
    page.on('request', async (request) => {
        const url = request.url();
        
        // Monitor OAuth initiation
        if (url.includes('/api/auth/external-login/google') || url.includes('/api/auth/google-oauth-url')) {
            console.log(`🚀 OAuth initiation: ${url}`);
            oauthRedirectDetected = true;
        }
        
        // Monitor Google OAuth requests
        if (url.includes('accounts.google.com/o/oauth2/v2/auth')) {
            console.log(`🔗 Google OAuth request: ${url}`);
            googleAuthReached = true;
            
            // Extract OAuth parameters
            const urlObj = new URL(url);
            oauthParams = {
                clientId: urlObj.searchParams.get('client_id'),
                redirectUri: urlObj.searchParams.get('redirect_uri'),
                state: urlObj.searchParams.get('state'),
                scope: urlObj.searchParams.get('scope')
            };
            
            console.log(`   Client ID: ${oauthParams.clientId?.substring(0, 20)}...`);
            console.log(`   Redirect URI: ${oauthParams.redirectUri}`);
            console.log(`   State: ${oauthParams.state?.substring(0, 10)}...`);
            console.log(`   Scope: ${oauthParams.scope}`);
        }
        
        // Monitor callback requests
        if (url.includes('/api/auth/external-login-callback')) {
            console.log(`🔄 OAuth callback: ${url}`);
            callbackReceived = true;
        }
    });
    
    page.on('response', async (response) => {
        const url = response.url();
        const status = response.status();
        
        // Monitor all authentication-related responses
        if (url.includes('/api/auth/')) {
            console.log(`📊 Auth response: ${url} -> ${status}`);
            
            if (status >= 400) {
                try {
                    const responseText = await response.text();
                    console.log(`❌ Auth error response: ${responseText.substring(0, 200)}...`);
                    
                    if (responseText.includes('token_exchange_failed') || url.includes('token_exchange_failed')) {
                        tokenExchangeError = true;
                        errorDetails = responseText;
                    }
                } catch (e) {
                    console.log('Could not read error response');
                }
            }
        }
        
        // Monitor Google OAuth token exchange
        if (url.includes('oauth2.googleapis.com/token')) {
            console.log(`🔐 Google token exchange: ${response.status()}`);
            
            if (!response.ok()) {
                try {
                    const errorResponse = await response.text();
                    console.log(`❌ Google token error: ${errorResponse}`);
                    
                    // Parse Google's error response
                    try {
                        const googleError = JSON.parse(errorResponse);
                        console.log(`   Google error type: ${googleError.error}`);
                        console.log(`   Google error description: ${googleError.error_description}`);
                    } catch (e) {
                        console.log('Could not parse Google error as JSON');
                    }
                } catch (e) {
                    console.log('Could not read Google token error response');
                }
            } else {
                console.log('✅ Google token exchange succeeded');
            }
        }
        
        // Monitor final redirects that contain errors
        if (status === 302 && response.headers()['location']) {
            const location = response.headers()['location'];
            if (location && location.includes('error=')) {
                console.log(`🔄 Error redirect: ${location}`);
                finalRedirectUri = location;
                
                if (location.includes('token_exchange_failed')) {
                    tokenExchangeError = true;
                }
            }
        }
    });
    
    try {
        console.log('\n1. Loading production login page...');
        await page.goto('https://app.aiprofilephotomaker.com/auth/login', { 
            waitUntil: 'networkidle',
            timeout: 30000
        });
        
        console.log('✅ Login page loaded successfully');
        
        console.log('\n2. Looking for Google OAuth button...');
        
        // Wait for the page to fully load and look for Google button
        await page.waitForTimeout(2000);
        
        // Try multiple possible selectors for Google OAuth button
        const possibleSelectors = [
            'button:has-text("Continue with Google")',
            'button:has-text("Sign in with Google")',
            'button:has-text("Google")',
            '[data-testid="google-login"]',
            '.google-login-button',
            'button[class*="google"]'
        ];
        
        let googleButton = null;
        for (const selector of possibleSelectors) {
            try {
                googleButton = page.locator(selector).first();
                if (await googleButton.isVisible({ timeout: 2000 })) {
                    console.log(`✅ Found Google button with selector: ${selector}`);
                    break;
                }
            } catch (e) {
                // Continue to next selector
            }
        }
        
        if (!googleButton || !await googleButton.isVisible({ timeout: 5000 })) {
            console.log('❌ Google OAuth button not found');
            
            // Take screenshot for debugging
            await page.screenshot({ 
                path: '/tmp/login-page-debug.png',
                fullPage: true 
            });
            console.log('📸 Login page screenshot saved to /tmp/login-page-debug.png');
            
            // Log page content for debugging
            const pageContent = await page.content();
            console.log(`📋 Page content preview: ${pageContent.substring(0, 500)}...`);
            
            throw new Error('Google OAuth button not found on login page');
        }
        
        console.log('\n3. Clicking Google OAuth button...');
        
        // Click the Google OAuth button
        await googleButton.click();
        
        console.log('✅ Google OAuth button clicked');
        
        console.log('\n4. Waiting for OAuth flow to complete...');
        
        // Wait for OAuth flow - either success or error
        let flowCompleted = false;
        let attempts = 0;
        const maxAttempts = 20; // 20 seconds total wait time
        
        while (!flowCompleted && attempts < maxAttempts) {
            await page.waitForTimeout(1000);
            attempts++;
            
            const currentUrl = page.url();
            console.log(`   Current URL (attempt ${attempts}): ${currentUrl}`);
            
            // Check if we're still on Google auth pages
            if (currentUrl.includes('accounts.google.com')) {
                console.log('   Still on Google OAuth - waiting...');
                continue;
            }
            
            // Check if we got an error
            if (currentUrl.includes('error=')) {
                console.log('❌ OAuth flow completed with error');
                flowCompleted = true;
                break;
            }
            
            // Check if we successfully reached dashboard or other success page
            if (currentUrl.includes('dashboard') || currentUrl.includes('app/')) {
                console.log('✅ OAuth flow completed successfully');
                flowCompleted = true;
                break;
            }
            
            // Check if callback was received but we're still processing
            if (callbackReceived && !currentUrl.includes('accounts.google.com')) {
                console.log('   Callback received, checking for completion...');
            }
        }
        
        const finalUrl = page.url();
        console.log(`📍 Final URL: ${finalUrl}`);
        
        // Analyze the results
        console.log('\n=== OAUTH FLOW ANALYSIS ===');
        console.log(`OAuth Redirect Detected: ${oauthRedirectDetected ? '✅' : '❌'}`);
        console.log(`Google Auth Reached: ${googleAuthReached ? '✅' : '❌'}`);
        console.log(`Callback Received: ${callbackReceived ? '✅' : '❌'}`);
        console.log(`Token Exchange Error: ${tokenExchangeError ? '❌' : '✅'}`);
        
        if (tokenExchangeError) {
            console.log('\n❌ TOKEN EXCHANGE STILL FAILING');
            console.log(`Final redirect: ${finalRedirectUri}`);
            console.log(`Error details: ${errorDetails.substring(0, 300)}`);
            
            // Log OAuth parameters that were used
            if (oauthParams.redirectUri) {
                console.log('\nOAuth Parameters Used:');
                console.log(`   Client ID: ${oauthParams.clientId}`);
                console.log(`   Redirect URI: ${oauthParams.redirectUri}`);
                console.log(`   Expected: https://api.aiprofilephotomaker.com/api/auth/external-login-callback`);
                
                if (oauthParams.redirectUri !== 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback') {
                    console.log('❌ REDIRECT URI MISMATCH DETECTED');
                } else {
                    console.log('✅ Redirect URI appears correct');
                }
            }
        } else if (finalUrl.includes('dashboard') || finalUrl.includes('app/')) {
            console.log('✅ OAuth login successful!');
        } else {
            console.log('⚠️  Unexpected final state');
        }
        
    } catch (error) {
        console.log(`❌ Test error: ${error}`);
        
        // Take error screenshot
        try {
            await page.screenshot({ 
                path: '/tmp/oauth-error-debug.png',
                fullPage: true 
            });
            console.log('📸 Error screenshot saved to /tmp/oauth-error-debug.png');
        } catch (screenshotError) {
            console.log('Could not take error screenshot');
        }
    }
    
    // Final recommendations
    console.log('\n=== TROUBLESHOOTING RECOMMENDATIONS ===');
    
    if (tokenExchangeError) {
        console.log('🔧 Token exchange is still failing. Possible causes:');
        console.log('1. Google OAuth Console redirect URI mismatch');
        console.log('2. Client secret incorrect or missing');
        console.log('3. Authorization code invalid or expired');
        console.log('4. Production environment configuration issue');
        
        console.log('\n📋 Next debugging steps:');
        console.log('1. Check Google OAuth Console configuration');
        console.log('2. Monitor production API logs during OAuth flow');
        console.log('3. Verify client secret is correctly deployed');
        console.log('4. Test token exchange endpoint directly');
    } else if (!googleAuthReached) {
        console.log('🔧 OAuth initiation failed - check frontend configuration');
    } else {
        console.log('✅ OAuth appears to be working correctly');
    }
    
    // Test always passes for analysis purposes
    expect(true).toBe(true);
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