import { test, expect } from '@playwright/test';

test('OAuth Domain Diagnosis - Custom vs Internal Domain', async ({ request }) => {
    console.log('\n=== OAUTH DOMAIN DIAGNOSIS ===');
    console.log('🔍 Testing custom domain vs internal Container App domain mismatch...');
    
    const customDomain = 'https://api.aiprofilephotomaker.com';
    const internalDomain = 'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io';
    
    // Test 1: Domain Resolution Comparison
    console.log('\n1. Testing domain resolution on both endpoints...');
    
    const endpoints = [
        { name: 'Custom Domain', url: customDomain },
        { name: 'Internal Domain', url: internalDomain }
    ];
    
    for (const endpoint of endpoints) {
        console.log(`\n--- Testing ${endpoint.name}: ${endpoint.url} ---`);
        
        try {
            // Test health endpoint
            const healthResponse = await request.get(`${endpoint.url}/api/health/basic`, {
                failOnStatusCode: false,
                timeout: 10000
            });
            
            console.log(`   Health endpoint: ${healthResponse.status()}`);
            
            if (healthResponse.status() === 200) {
                console.log(`   ✅ ${endpoint.name} is accessible`);
                
                // Test OAuth initiation
                const oauthResponse = await request.get(`${endpoint.url}/api/auth/external-login/google?returnUrl=/app/dashboard`, {
                    failOnStatusCode: false,
                    timeout: 10000
                });
                
                console.log(`   OAuth initiation: ${oauthResponse.status()}`);
                
                if (oauthResponse.status() === 302) {
                    const location = oauthResponse.headers()['location'];
                    console.log(`   ✅ OAuth redirect working`);
                    
                    if (location && location.includes('accounts.google.com')) {
                        const authUrl = new URL(location);
                        const redirectUri = authUrl.searchParams.get('redirect_uri');
                        const clientId = authUrl.searchParams.get('client_id');
                        const state = authUrl.searchParams.get('state');
                        
                        console.log(`   🔗 Redirect URI: ${redirectUri}`);
                        console.log(`   🔑 Client ID: ${clientId?.substring(0, 20)}...`);
                        console.log(`   🎲 State: ${state?.substring(0, 10)}...`);
                        
                        // Critical check: Does redirect URI match the domain we're testing?
                        const expectedRedirectUri = `${endpoint.url}/api/auth/external-login-callback`;
                        
                        if (redirectUri === expectedRedirectUri) {
                            console.log(`   ✅ Redirect URI matches ${endpoint.name}`);
                        } else {
                            console.log(`   ❌ REDIRECT URI MISMATCH!`);
                            console.log(`      Expected: ${expectedRedirectUri}`);
                            console.log(`      Got:      ${redirectUri}`);
                        }
                        
                        // Test callback endpoint with this state
                        console.log(`   Testing callback endpoint...`);
                        const callbackResponse = await request.get(`${endpoint.url}/api/auth/external-login-callback?code=test_invalid_code&state=${state}`, {
                            failOnStatusCode: false,
                            timeout: 10000
                        });
                        
                        console.log(`   Callback response: ${callbackResponse.status()}`);
                        
                        if (callbackResponse.status() === 302) {
                            const callbackLocation = callbackResponse.headers()['location'];
                            console.log(`   Callback redirect: ${callbackLocation?.substring(0, 100)}...`);
                            
                            if (callbackLocation && callbackLocation.includes('error=')) {
                                const errorUrl = new URL(callbackLocation);
                                const error = errorUrl.searchParams.get('error');
                                console.log(`   Error type: ${error}`);
                                
                                if (error === 'token_exchange_failed') {
                                    console.log(`   ❌ ${endpoint.name} still showing token_exchange_failed`);
                                } else {
                                    console.log(`   ℹ️  ${endpoint.name} showing different error: ${error}`);
                                }
                            }
                        } else {
                            console.log(`   ⚠️  Unexpected callback status on ${endpoint.name}`);
                        }
                        
                    } else {
                        console.log(`   ❌ OAuth redirect not pointing to Google`);
                    }
                } else if (oauthResponse.status() === 200) {
                    console.log(`   ℹ️  Got 200 (possibly following redirect automatically)`);
                } else {
                    console.log(`   ❌ OAuth initiation failed with ${oauthResponse.status()}`);
                }
                
            } else {
                console.log(`   ❌ ${endpoint.name} not accessible: ${healthResponse.status()}`);
            }
            
        } catch (error) {
            console.log(`   ❌ ${endpoint.name} connection error: ${error}`);
        }
    }
    
    // Test 2: Direct Domain Resolution Test
    console.log('\n2. Testing direct domain resolution...');
    
    try {
        // Test if custom domain resolves to the same content as internal domain
        const customHealthResponse = await request.get(`${customDomain}/api/health/basic`, {
            failOnStatusCode: false
        });
        
        const internalHealthResponse = await request.get(`${internalDomain}/api/health/basic`, {
            failOnStatusCode: false
        });
        
        console.log(`   Custom domain health: ${customHealthResponse.status()}`);
        console.log(`   Internal domain health: ${internalHealthResponse.status()}`);
        
        if (customHealthResponse.status() === 200 && internalHealthResponse.status() === 200) {
            console.log(`   ✅ Both domains are accessible and working`);
            console.log(`   💡 The issue might be in OAuth redirect URI configuration`);
        } else {
            console.log(`   ⚠️  Domain accessibility mismatch detected`);
        }
        
    } catch (error) {
        console.log(`   ❌ Domain resolution test failed: ${error}`);
    }
    
    // Test 3: Check Google OAuth Console Requirements
    console.log('\n3. Analyzing OAuth configuration requirements...');
    
    const expectedCustomRedirect = `${customDomain}/api/auth/external-login-callback`;
    const expectedInternalRedirect = `${internalDomain}/api/auth/external-login-callback`;
    
    console.log('📋 Google OAuth Console should have configured:');
    console.log(`   Custom domain redirect: ${expectedCustomRedirect}`);
    console.log(`   Internal domain redirect: ${expectedInternalRedirect} (if needed)`);
    
    console.log('\n=== DIAGNOSIS SUMMARY ===');
    console.log('🔍 Key findings:');
    console.log('1. Check if both domains generate the same OAuth redirect URIs');
    console.log('2. Verify Google OAuth Console has correct redirect URIs configured');
    console.log('3. Identify which domain is actually being used for callbacks');
    console.log('4. Look for any Container Apps forwarding header issues');
    
    console.log('\n💡 Next steps:');
    console.log('1. Check Google OAuth Console redirect URI configuration');
    console.log('2. Verify Container Apps custom domain forwarding');
    console.log('3. Add enhanced logging to ResolveBackendBaseUrl()');
    console.log('4. Test with actual OAuth flow to see which domain fails');
    
    // Always pass for diagnostic purposes
    expect(true).toBe(true);
});

test('OAuth ResolveBackendBaseUrl Debug Test', async ({ request }) => {
    console.log('\n=== OAUTH BACKEND URL RESOLUTION DEBUG ===');
    
    // Test both domains to see what ResolveBackendBaseUrl returns
    const domains = [
        'https://api.aiprofilephotomaker.com',
        'https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io'
    ];
    
    for (const domain of domains) {
        console.log(`\nTesting backend URL resolution via ${domain}...`);
        
        try {
            const response = await request.get(`${domain}/api/auth/google-oauth-url?returnUrl=/app/dashboard`, {
                failOnStatusCode: false,
                timeout: 15000
            });
            
            console.log(`Response status: ${response.status()}`);
            
            if (response.status() === 200) {
                const responseBody = await response.text();
                
                try {
                    const jsonResponse = JSON.parse(responseBody);
                    if (jsonResponse.authUrl) {
                        const authUrl = new URL(jsonResponse.authUrl);
                        const redirectUri = authUrl.searchParams.get('redirect_uri');
                        
                        console.log(`✅ OAuth URL generated successfully`);
                        console.log(`🔗 Generated redirect URI: ${redirectUri}`);
                        
                        // Check if redirect URI matches the domain we're testing
                        const expectedUri = `${domain}/api/auth/external-login-callback`;
                        if (redirectUri === expectedUri) {
                            console.log(`✅ Redirect URI matches request domain`);
                        } else {
                            console.log(`❌ DOMAIN MISMATCH DETECTED!`);
                            console.log(`   Request domain: ${domain}`);
                            console.log(`   Expected URI:   ${expectedUri}`);
                            console.log(`   Generated URI:  ${redirectUri}`);
                            
                            if (redirectUri?.includes('api.aiprofilephotomaker.com')) {
                                console.log(`💡 ResolveBackendBaseUrl() is returning custom domain`);
                            } else if (redirectUri?.includes('azurecontainerapps.io')) {
                                console.log(`💡 ResolveBackendBaseUrl() is returning internal domain`);
                            }
                        }
                    } else {
                        console.log(`❌ No authUrl in response`);
                    }
                } catch (parseError) {
                    console.log(`❌ Could not parse response as JSON`);
                    console.log(`Response preview: ${responseBody.substring(0, 200)}...`);
                }
                
            } else {
                console.log(`❌ Failed to get OAuth URL: ${response.status()}`);
                const errorText = await response.text();
                console.log(`Error: ${errorText.substring(0, 200)}...`);
            }
            
        } catch (error) {
            console.log(`❌ Connection error: ${error}`);
        }
    }
    
    console.log('\n=== URL RESOLUTION ANALYSIS ===');
    console.log('This test shows which domain ResolveBackendBaseUrl() returns');
    console.log('when accessed via different entry points.');
    console.log('\nIf there\'s a mismatch, the issue is likely:');
    console.log('1. Container Apps not properly forwarding Host headers');
    console.log('2. ResolveBackendBaseUrl() not prioritizing custom domain');
    console.log('3. Google OAuth Console missing redirect URI configuration');
    
    expect(true).toBe(true);
});