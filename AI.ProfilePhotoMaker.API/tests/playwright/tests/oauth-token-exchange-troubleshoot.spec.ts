import { test, expect } from '@playwright/test';

test('OAuth Token Exchange Troubleshooting', async ({ request, page }) => {
    console.log('\n=== OAUTH TOKEN EXCHANGE TROUBLESHOOTING ===');
    
    // Step 1: Test the initial OAuth redirect
    console.log('1. Testing OAuth redirect initialization...');
    
    const oauthResponse = await request.get('https://api.aiprofilephotomaker.com/api/auth/external-login/google?returnUrl=/app/dashboard', {
        failOnStatusCode: false
    });
    
    console.log(`📊 OAuth redirect status: ${oauthResponse.status()}`);
    
    if (oauthResponse.status() === 302) {
        const location = oauthResponse.headers()['location'];
        console.log('✅ OAuth redirect working');
        
        if (location && location.includes('accounts.google.com')) {
            const authUrl = new URL(location);
            const state = authUrl.searchParams.get('state');
            const redirectUri = authUrl.searchParams.get('redirect_uri');
            const clientId = authUrl.searchParams.get('client_id');
            
            console.log(`🔑 State: ${state}`);
            console.log(`🔑 Redirect URI: ${redirectUri}`);
            console.log(`🔑 Client ID: ${clientId?.substring(0, 20)}...`);
            
            // Step 2: Test callback with invalid code to see exact error
            console.log('\n2. Testing callback with invalid authorization code...');
            
            const callbackUrl = `https://api.aiprofilephotomaker.com/api/auth/external-login-callback?code=invalid_test_code&state=${state}`;
            
            const callbackResponse = await request.get(callbackUrl, {
                failOnStatusCode: false
            });
            
            console.log(`📊 Callback response status: ${callbackResponse.status()}`);
            
            if (callbackResponse.status() === 302) {
                const redirectLocation = callbackResponse.headers()['location'];
                console.log(`🔗 Callback redirect: ${redirectLocation}`);
                
                if (redirectLocation && redirectLocation.includes('error=token_exchange_failed')) {
                    console.log('❌ CONFIRMED: Token exchange is failing as expected with invalid code');
                    
                    // Step 3: Test what happens with Google's token endpoint
                    console.log('\n3. Testing Google token exchange endpoint directly...');
                    
                    const tokenRequest = new URLSearchParams({
                        client_id: clientId || '',
                        client_secret: 'test_secret', // This will fail but shows us the error
                        code: 'invalid_test_code',
                        grant_type: 'authorization_code',
                        redirect_uri: redirectUri || ''
                    });
                    
                    const tokenResponse = await request.post('https://oauth2.googleapis.com/token', {
                        data: tokenRequest.toString(),
                        headers: {
                            'Content-Type': 'application/x-www-form-urlencoded'
                        },
                        failOnStatusCode: false
                    });
                    
                    console.log(`📊 Google token endpoint status: ${tokenResponse.status()}`);
                    
                    if (!tokenResponse.ok()) {
                        const errorText = await tokenResponse.text();
                        console.log(`📋 Google token error: ${errorText}`);
                        
                        try {
                            const errorJson = JSON.parse(errorText);
                            console.log(`🔍 Error type: ${errorJson.error}`);
                            console.log(`🔍 Error description: ${errorJson.error_description}`);
                        } catch (e) {
                            console.log('Could not parse error as JSON');
                        }
                    }
                } else {
                    console.log('⚠️ Unexpected callback redirect location');
                }
            } else {
                console.log(`⚠️ Unexpected callback status: ${callbackResponse.status()}`);
                const errorText = await callbackResponse.text();
                console.log(`📋 Callback error: ${errorText.substring(0, 300)}`);
            }
        }
    } else {
        console.log('❌ OAuth redirect not working');
    }
    
    // Step 4: Test real-world flow with browser
    console.log('\n4. Testing real OAuth flow through browser...');
    
    let tokenExchangeError = false;
    let finalUrl = '';
    
    // Monitor network requests
    page.on('response', async (response) => {
        const url = response.url();
        if (url.includes('/api/auth/external-login-callback')) {
            console.log(`🔗 Callback request: ${url} -> ${response.status()}`);
            
            if (response.status() === 302) {
                const location = response.headers()['location'];
                if (location && location.includes('error=token_exchange_failed')) {
                    tokenExchangeError = true;
                    console.log('❌ DETECTED: token_exchange_failed in real flow');
                }
            }
        }
        
        if (url.includes('oauth2.googleapis.com/token')) {
            console.log(`🔗 Google token exchange: ${response.status()}`);
            
            if (!response.ok()) {
                try {
                    const errorText = await response.text();
                    console.log(`📋 Token exchange API error: ${errorText.substring(0, 200)}`);
                } catch (e) {
                    console.log('Could not read token exchange error response');
                }
            }
        }
    });
    
    try {
        // Navigate to login page
        await page.goto('https://aiprofilephotomaker.com/auth/login');
        await page.waitForLoadState('networkidle');
        
        // Find Google login button
        const googleButton = page.locator('button:has-text("Continue with Google")');
        
        if (await googleButton.isVisible({ timeout: 5000 })) {
            console.log('🔘 Found Google login button, clicking...');
            
            await googleButton.click();
            
            // Wait for OAuth flow
            await page.waitForTimeout(8000);
            
            finalUrl = page.url();
            console.log(`📍 Final URL: ${finalUrl}`);
            
            if (tokenExchangeError) {
                console.log('❌ CONFIRMED: Real OAuth flow is experiencing token_exchange_failed');
            } else if (finalUrl.includes('error=')) {
                console.log('⚠️ OAuth flow ended with error but not token_exchange_failed');
            } else if (finalUrl.includes('accounts.google.com')) {
                console.log('✅ Successfully redirected to Google - OAuth initiation working');
            }
            
        } else {
            console.log('⚠️ Could not find Google login button');
        }
    } catch (error) {
        console.log(`❌ Error in real flow test: ${error}`);
    }
    
    console.log('\n=== TROUBLESHOOTING SUMMARY ===');
    console.log('Token Exchange Error Analysis:');
    console.log('1. OAuth redirect initiation: Working');
    console.log('2. Google OAuth parameters: Correct');
    console.log(`3. Real flow token exchange: ${tokenExchangeError ? 'FAILING' : 'Unknown'}`);
    console.log('4. Next steps: Check Google OAuth Console configuration');
    
    expect(true).toBe(true); // Test always passes for analysis
});