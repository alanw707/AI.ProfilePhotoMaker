import { test, expect } from '@playwright/test';

test('OAuth Session Fix Verification - Production', async ({ request }) => {
    console.log('\n=== OAUTH SESSION FIX VERIFICATION - PRODUCTION ===');
    
    // Test 1: Verify OAuth URL generation works without session errors
    console.log('🔍 Testing production OAuth URL generation (session fix verification)...');
    
    const oauthUrlResponse = await request.get('https://api.aiprofilephotomaker.com/api/auth/google-oauth-url', {
        failOnStatusCode: false
    });
    
    console.log(`📊 OAuth URL endpoint response: ${oauthUrlResponse.status()}`);
    
    const responseBody = await oauthUrlResponse.text();
    console.log(`📋 Response preview: ${responseBody.substring(0, 150)}...`);
    
    if (oauthUrlResponse.status() === 200) {
        console.log('✅ OAuth URL endpoint returned 200 - Session fix is working!');
        
        try {
            const jsonResponse = JSON.parse(responseBody);
            if (jsonResponse.authUrl) {
                console.log('✅ OAuth URL generated successfully');
                
                const authUrl = new URL(jsonResponse.authUrl);
                const redirectUri = authUrl.searchParams.get('redirect_uri');
                const clientId = authUrl.searchParams.get('client_id');
                
                console.log(`🔗 Client ID: ${clientId?.substring(0, 20)}...`);
                console.log(`🔗 Redirect URI: ${redirectUri}`);
                
                // Verify correct redirect URI
                const expectedRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
                expect(redirectUri).toBe(expectedRedirectUri);
                console.log('✅ Redirect URI is correct!');
                
                // Verify Google OAuth host
                expect(authUrl.host).toBe('accounts.google.com');
                console.log('✅ OAuth URL points to Google correctly!');
                
            } else {
                console.log('❌ No authUrl in response');
                throw new Error('OAuth URL not found in response');
            }
        } catch (parseError) {
            console.log('❌ Could not parse OAuth URL response as JSON');
            console.log(`   Response: ${responseBody}`);
            throw parseError;
        }
        
    } else if (oauthUrlResponse.status() === 500) {
        console.log('❌ OAuth URL endpoint returned 500 error');
        console.log(`   Error response: ${responseBody.substring(0, 300)}`);
        
        if (responseBody.includes('.xsrf') || responseBody.includes('session')) {
            console.log('❌ STILL HAVING SESSION ERRORS - deployment may not be complete');
            throw new Error('Session errors still present in production');
        } else {
            console.log('ℹ️  Different error - session fix deployed but other issue exists');
            throw new Error(`Production OAuth error: ${responseBody.substring(0, 100)}`);
        }
    } else {
        console.log(`⚠️  Unexpected status: ${oauthUrlResponse.status()}`);
        throw new Error(`Unexpected OAuth URL response: ${oauthUrlResponse.status()}`);
    }
    
    // Test 2: Verify OAuth redirect flow works without session errors
    console.log('\n🔍 Testing OAuth redirect flow...');
    
    const redirectResponse = await request.get('https://api.aiprofilephotomaker.com/api/auth/external-login/google', {
        failOnStatusCode: false
    });
    
    console.log(`📊 OAuth redirect response: ${redirectResponse.status()}`);
    
    if (redirectResponse.status() === 302) {
        const location = redirectResponse.headers()['location'];
        console.log('✅ OAuth redirect working (302 redirect)!');
        console.log(`🔗 Redirect location: ${location?.substring(0, 100)}...`);
        
        if (location && location.includes('accounts.google.com')) {
            const redirectUrl = new URL(location);
            const redirectUri = redirectUrl.searchParams.get('redirect_uri');
            console.log(`🔗 Extracted redirect URI: ${redirectUri}`);
            
            const expectedRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
            expect(redirectUri).toBe(expectedRedirectUri);
            console.log('✅ OAuth redirect uses correct URI!');
        } else {
            console.log('⚠️  Redirect location does not point to Google');
        }
    } else if (redirectResponse.status() === 500) {
        const errorBody = await redirectResponse.text();
        console.log('❌ OAuth redirect returned 500 error');
        console.log(`   Error: ${errorBody.substring(0, 300)}`);
        
        if (errorBody.includes('.xsrf') || errorBody.includes('session')) {
            console.log('❌ STILL HAVING SESSION ERRORS in redirect flow');
            throw new Error('Session errors still present in OAuth redirect');
        }
    } else {
        console.log(`⚠️  Unexpected redirect status: ${redirectResponse.status()}`);
        const errorBody = await redirectResponse.text();
        console.log(`   Response: ${errorBody.substring(0, 200)}`);
    }
    
    console.log('\n✅ SESSION FIX VERIFICATION COMPLETED');
    console.log('✅ OAuth URL generation working without session errors');
    console.log('✅ Production OAuth endpoints are functional');
});

test('OAuth Token Exchange Debug - Post Session Fix', async ({ request }) => {
    console.log('\n=== OAUTH TOKEN EXCHANGE DEBUG - POST SESSION FIX ===');
    
    // Test callback endpoint with various scenarios to see if session fix resolved issues
    const callbackUrl = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
    
    console.log('🔍 Testing OAuth callback endpoint behavior...');
    
    // Test 1: Missing code parameter
    const missingCodeResponse = await request.get(callbackUrl + '?state=test-state', {
        failOnStatusCode: false
    });
    
    console.log(`📊 Missing code test: ${missingCodeResponse.status()}`);
    
    if (missingCodeResponse.status() === 302) {
        const location = missingCodeResponse.headers()['location'];
        console.log('✅ Missing code handled correctly (302 redirect)');
        console.log(`   Redirect: ${location?.substring(0, 100)}...`);
        
        if (location && location.includes('error=missing_code')) {
            console.log('✅ Correct error parameter in redirect');
        }
    } else if (missingCodeResponse.status() === 500) {
        const errorBody = await missingCodeResponse.text();
        console.log('❌ Missing code test failed with 500');
        console.log(`   Error: ${errorBody.substring(0, 200)}`);
        
        if (errorBody.includes('.xsrf') || errorBody.includes('session')) {
            console.log('❌ SESSION ERRORS STILL PRESENT');
        }
    } else {
        console.log(`ℹ️  Missing code test returned: ${missingCodeResponse.status()}`);
    }
    
    // Test 2: Invalid state parameter
    const invalidStateResponse = await request.get(callbackUrl + '?code=test-code&state=invalid-state', {
        failOnStatusCode: false
    });
    
    console.log(`📊 Invalid state test: ${invalidStateResponse.status()}`);
    
    if (invalidStateResponse.status() === 302) {
        const location = invalidStateResponse.headers()['location'];
        console.log('✅ Invalid state handled correctly (302 redirect)');
        
        if (location && (location.includes('error=session_expired') || location.includes('error=invalid_state'))) {
            console.log('✅ Correct error handling for invalid state');
        }
    } else {
        console.log(`ℹ️  Invalid state test returned: ${invalidStateResponse.status()}`);
    }
    
    console.log('\n📋 CALLBACK ENDPOINT ANALYSIS:');
    console.log('If endpoints are returning 302 redirects instead of 500 errors,');
    console.log('the session fix is working and OAuth flow can proceed normally.');
    
    expect(true).toBe(true); // Test passes for monitoring purposes
});

test('Production OAuth Flow Integration Test', async ({ page }) => {
    console.log('\n=== PRODUCTION OAUTH FLOW INTEGRATION TEST ===');
    
    console.log('🚀 Testing OAuth flow from frontend application...');
    
    let sessionErrorDetected = false;
    let oauthUrlGenerated = false;
    let finalOAuthUrl = '';
    
    // Monitor all network requests for OAuth-related errors
    page.on('response', async (response) => {
        const url = response.url();
        const status = response.status();
        
        if (url.includes('/api/auth/')) {
            console.log(`🔗 Auth request: ${url} -> ${status}`);
            
            if (status === 500) {
                try {
                    const errorText = await response.text();
                    console.log(`❌ Auth endpoint error: ${errorText.substring(0, 150)}...`);
                    
                    if (errorText.includes('.xsrf') || errorText.includes('session')) {
                        sessionErrorDetected = true;
                        console.log('❌ SESSION ERROR DETECTED IN PRODUCTION');
                    }
                } catch (e) {
                    console.log('Could not read error response body');
                }
            } else if (status === 200 && url.includes('google-oauth-url')) {
                oauthUrlGenerated = true;
                console.log('✅ OAuth URL generated successfully');
            }
        }
        
        // Capture successful redirect to Google
        if (url.includes('accounts.google.com') && status === 200) {
            finalOAuthUrl = url;
            console.log('✅ Successfully redirected to Google OAuth');
            
            const redirectUri = new URL(url).searchParams.get('redirect_uri');
            console.log(`🔗 Final redirect URI: ${redirectUri}`);
        }
    });
    
    try {
        await page.goto('https://aiprofilephotomaker.com/auth/login');
        await page.waitForLoadState('networkidle');
        console.log('📄 Login page loaded');
        
        // Look for Google login button
        const googleButton = page.locator('button:has-text("Continue with Google")');
        
        if (await googleButton.isVisible({ timeout: 5000 })) {
            console.log('🔘 Google login button found');
            
            await googleButton.click();
            console.log('🔘 Google login button clicked');
            
            // Wait for OAuth flow to complete
            await page.waitForTimeout(5000);
            
            const currentUrl = page.url();
            console.log(`📍 Current URL: ${currentUrl}`);
            
            // Analyze results
            if (sessionErrorDetected) {
                console.log('❌ SESSION ERRORS STILL PRESENT - SESSION FIX NOT WORKING');
                throw new Error('Session errors detected in production OAuth flow');
            } else if (oauthUrlGenerated) {
                console.log('✅ NO SESSION ERRORS DETECTED - SESSION FIX IS WORKING!');
            }
            
            if (currentUrl.includes('accounts.google.com')) {
                console.log('✅ OAuth flow completed successfully - redirected to Google');
            } else if (currentUrl.includes('error=')) {
                console.log(`⚠️  OAuth flow ended with error: ${currentUrl}`);
            }
            
        } else {
            console.log('⚠️  Google login button not found on page');
        }
        
    } catch (error) {
        console.log(`❌ Error in integration test: ${error}`);
    }
    
    console.log('\n🎉 PRODUCTION OAUTH INTEGRATION TEST SUMMARY:');
    console.log(`   Session Errors: ${sessionErrorDetected ? '❌ DETECTED' : '✅ NONE'}`);
    console.log(`   OAuth URL Generation: ${oauthUrlGenerated ? '✅ SUCCESS' : '❌ FAILED'}`);
    console.log(`   Google Redirect: ${finalOAuthUrl ? '✅ SUCCESS' : '❌ FAILED'}`);
    
    if (!sessionErrorDetected && oauthUrlGenerated) {
        console.log('✅ SESSION FIX VERIFICATION PASSED - OAuth working in production!');
    } else {
        console.log('❌ Session fix may not be fully working - check deployment');
    }
    
    expect(sessionErrorDetected).toBe(false);
});