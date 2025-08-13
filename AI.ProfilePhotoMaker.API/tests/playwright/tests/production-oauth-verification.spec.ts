import { test, expect } from '@playwright/test';

test('Production OAuth Redirect URI Verification', async ({ page }) => {
    console.log('\n=== PRODUCTION OAUTH REDIRECT URI VERIFICATION ===');
    
    console.log('🎯 Testing PRODUCTION OAuth URL directly...');
    
    let capturedOAuthUrl = '';
    
    // Intercept OAuth redirect to Google
    page.on('response', async (response) => {
        if (response.status() === 302 && response.headers()['location']?.includes('accounts.google.com')) {
            capturedOAuthUrl = response.headers()['location'];
            console.log('🔗 Captured Production OAuth URL:', capturedOAuthUrl);
        }
    });
    
    // Navigate directly to the production OAuth endpoint
    console.log('📍 Navigating to production OAuth endpoint...');
    await page.goto('https://api.aiprofilephotomaker.com/api/auth/external-login/google?returnUrl=/app/dashboard');
    
    // Wait for redirect to complete
    await page.waitForTimeout(3000);
    
    const currentUrl = page.url();
    console.log('📍 Current URL after redirect:', currentUrl);
    
    if (currentUrl.includes('accounts.google.com')) {
        console.log('✅ Successfully redirected to Google OAuth');
        
        // Extract redirect URI from the current URL
        const urlParams = new URL(currentUrl);
        const redirectUri = urlParams.searchParams.get('redirect_uri');
        const clientId = urlParams.searchParams.get('client_id');
        
        console.log('\n🔍 Production OAuth Parameters:');
        console.log('   Client ID:', clientId);
        console.log('   Redirect URI:', redirectUri);
        
        // Verify the redirect URI is the correct production URL
        const expectedRedirectUri = 'https://api.aiprofilephotomaker.com/api/auth/external-login-callback';
        
        if (redirectUri === expectedRedirectUri) {
            console.log('✅ SUCCESS: Production uses correct HTTPS redirect URI!');
            expect(redirectUri).toBe(expectedRedirectUri);
        } else {
            console.log('❌ FAILED: Production redirect URI is incorrect');
            console.log(`   Expected: ${expectedRedirectUri}`);
            console.log(`   Got:      ${redirectUri}`);
            throw new Error(`Production redirect URI mismatch. Expected: ${expectedRedirectUri}, Got: ${redirectUri}`);
        }
        
        // Verify Client ID is correct
        const expectedClientId = '116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com';
        expect(clientId).toBe(expectedClientId);
        
        console.log('\n🎉 PRODUCTION OAUTH FIX VERIFIED!');
        console.log('✅ Backend correctly uses production HTTPS redirect URI');
        console.log('✅ OAuth configuration is working in production');
        
    } else {
        console.log('❌ FAILED: Did not redirect to Google OAuth');
        console.log('Current URL:', currentUrl);
        throw new Error('Production OAuth redirect failed');
    }
});

test('Production vs Local Comparison', async ({ page }) => {
    console.log('\n=== PRODUCTION VS LOCAL OAUTH COMPARISON ===');
    
    // Test production
    console.log('🚀 Testing Production OAuth...');
    await page.goto('https://api.aiprofilephotomaker.com/api/auth/external-login/google');
    await page.waitForTimeout(2000);
    
    let productionRedirectUri = '';
    if (page.url().includes('accounts.google.com')) {
        const urlParams = new URL(page.url());
        productionRedirectUri = urlParams.searchParams.get('redirect_uri') || '';
        console.log('   Production Redirect URI:', productionRedirectUri);
    }
    
    // Test local
    console.log('🏠 Testing Local OAuth...');
    await page.goto('http://localhost:5032/api/auth/external-login/google');
    await page.waitForTimeout(2000);
    
    let localRedirectUri = '';
    if (page.url().includes('accounts.google.com')) {
        const urlParams = new URL(page.url());
        localRedirectUri = urlParams.searchParams.get('redirect_uri') || '';
        console.log('   Local Redirect URI:', localRedirectUri);
    }
    
    // Verify they're different and correct
    console.log('\n📊 Comparison Results:');
    console.log(`   Production: ${productionRedirectUri}`);
    console.log(`   Local:      ${localRedirectUri}`);
    
    if (productionRedirectUri.includes('api.aiprofilephotomaker.com') && 
        localRedirectUri.includes('localhost:5032')) {
        console.log('✅ SUCCESS: Both environments use correct redirect URIs!');
    } else {
        console.log('❌ FAILED: Redirect URIs are not correct for their environments');
        throw new Error('Environment-specific redirect URIs are not working correctly');
    }
});