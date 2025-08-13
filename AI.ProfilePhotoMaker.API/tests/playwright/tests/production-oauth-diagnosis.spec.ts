import { test, expect } from '@playwright/test';

const PRODUCTION_API_URL = 'https://api.aiprofilephotomaker.com';

test('Production OAuth Configuration Diagnosis', async ({ request }) => {
    console.log('\n=== PRODUCTION OAUTH DIAGNOSIS ===');
    
    // Step 1: Test if production API is responsive
    console.log('1. Testing production API connectivity...');
    try {
        const healthResponse = await request.get(`${PRODUCTION_API_URL}/api/health/basic`);
        console.log(`   API Health Status: ${healthResponse.status()}`);
        
        if (healthResponse.status() !== 200) {
            console.log('   ⚠️  Production API is not responding correctly');
            return;
        }
    } catch (error) {
        console.log('   ❌ Production API is not accessible:', error);
        return;
    }
    
    // Step 2: Check OAuth configuration
    console.log('2. Checking production OAuth configuration...');
    try {
        const oauthConfigResponse = await request.get(`${PRODUCTION_API_URL}/api/auth/debug/oauth-config`);
        
        if (oauthConfigResponse.status() === 200) {
            const config = await oauthConfigResponse.json();
            console.log('   OAuth Config Retrieved Successfully');
            console.log('   Final Values:');
            console.log(`     Client ID: ${config.finalValues?.clientId || 'NOT SET'}`);
            console.log(`     Client Secret: ${config.finalValues?.clientSecret || 'NOT SET'}`);
        } else {
            console.log(`   ⚠️  OAuth config endpoint returned: ${oauthConfigResponse.status()}`);
        }
    } catch (error) {
        console.log('   ❌ Failed to get OAuth config:', error);
    }
    
    // Step 3: Test OAuth URL generation
    console.log('3. Testing production OAuth URL generation...');
    try {
        const oauthUrlResponse = await request.get(`${PRODUCTION_API_URL}/api/auth/google-oauth-url?returnUrl=/app/dashboard`);
        
        if (oauthUrlResponse.status() === 200) {
            const urlData = await oauthUrlResponse.json();
            console.log('   OAuth URL Generated Successfully');
            
            // Parse the OAuth URL
            const authUrl = new URL(urlData.authUrl);
            const redirectUri = authUrl.searchParams.get('redirect_uri');
            const clientId = authUrl.searchParams.get('client_id');
            
            console.log(`   Redirect URI: ${redirectUri}`);
            console.log(`   Client ID: ${clientId?.substring(0, 20)}...`);
            
            // Validate production redirect URI
            const expectedRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
            
            if (redirectUri === expectedRedirectUri) {
                console.log('   ✅ Production redirect URI is CORRECT!');
            } else {
                console.log('   ❌ Production redirect URI is WRONG!');
                console.log(`      Expected: ${expectedRedirectUri}`);
                console.log(`      Got:      ${redirectUri}`);
            }
        } else {
            console.log(`   ⚠️  OAuth URL endpoint returned: ${oauthUrlResponse.status()}`);
            const errorText = await oauthUrlResponse.text();
            console.log(`   Error: ${errorText}`);
        }
    } catch (error) {
        console.log('   ❌ Failed to generate OAuth URL:', error);
    }
    
    // Step 4: Test the actual OAuth redirect
    console.log('4. Testing direct OAuth redirect...');
    try {
        const oauthRedirectResponse = await request.get(`${PRODUCTION_API_URL}/api/auth/external-login/google?returnUrl=/app/dashboard`, {
            failOnStatusCode: false
        });
        
        console.log(`   OAuth redirect status: ${oauthRedirectResponse.status()}`);
        
        if (oauthRedirectResponse.status() === 302) {
            const location = oauthRedirectResponse.headers()['location'];
            console.log('   Redirect Location:', location?.substring(0, 100) + '...');
            
            if (location && location.includes('accounts.google.com')) {
                const authUrl = new URL(location);
                const redirectUri = authUrl.searchParams.get('redirect_uri');
                
                console.log(`   Extracted Redirect URI: ${redirectUri}`);
                
                const expectedRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
                if (redirectUri === expectedRedirectUri) {
                    console.log('   ✅ Direct OAuth redirect uses CORRECT URI!');
                } else {
                    console.log('   ❌ Direct OAuth redirect uses WRONG URI!');
                    console.log(`      Expected: ${expectedRedirectUri}`);
                    console.log(`      Got:      ${redirectUri}`);
                }
            }
        }
    } catch (error) {
        console.log('   ❌ Failed to test OAuth redirect:', error);
    }
    
    console.log('\n=== NEXT STEPS ===');
    console.log('Based on this diagnosis:');
    console.log('1. If redirect URI is wrong, the backend needs to be fixed and redeployed');
    console.log('2. If redirect URI is correct, check Google OAuth Console configuration');
    console.log('3. Required redirect URI: https://api.aiprofilephotomaker.com/api/auth/external-login-callback');
    console.log('4. Client ID should be: 116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com');
});

test('Google OAuth Console Requirements', async () => {
    console.log('\n=== GOOGLE OAUTH CONSOLE CONFIGURATION REQUIRED ===');
    
    const clientId = '116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com';
    const requiredRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
    
    console.log('Steps to fix in Google Cloud Console:');
    console.log('=====================================');
    console.log('1. Go to: https://console.cloud.google.com/');
    console.log('2. Navigate to: APIs & Services > Credentials');
    console.log(`3. Find OAuth 2.0 Client ID: ${clientId}`);
    console.log('4. Click Edit');
    console.log('5. In "Authorized redirect URIs" section:');
    console.log(`   - Add: ${requiredRedirectUri}`);
    console.log('   - Remove any incorrect URIs (like localhost ones)');
    console.log('6. Save changes');
    console.log('7. Wait 1-2 minutes for changes to propagate');
    console.log('\n⚠️  CRITICAL: Both the backend code AND Google Console must have matching redirect URIs!');
    
    expect(true).toBe(true); // Always pass - this is documentation
});