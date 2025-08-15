import { test, expect } from '@playwright/test';

const PRODUCTION_URL = 'https://api.aiprofilephotomaker.com';
const FRONTEND_URL = 'https://app.aiprofilephotomaker.com';

test.describe('OAuth Production Fix Verification', () => {
  
  test('verify Google OAuth configuration issue', async ({ page }) => {
    console.log('🔍 Testing Google OAuth configuration in production...');
    
    // Navigate to login page
    await page.goto(`${FRONTEND_URL}/auth/login`);
    await page.waitForLoadState('networkidle');
    
    // Click Google OAuth button and capture the redirect
    const responsePromise = page.waitForResponse(response => 
      response.url().includes('accounts.google.com') || 
      response.url().includes('oauth')
    );
    
    // Click the Google OAuth button
    await page.click('button:has-text("Continue with Google")');
    
    // Wait for redirect or error
    try {
      const response = await responsePromise;
      const url = response.url();
      
      console.log(`🔗 Redirected to: ${url}`);
      
      // Check if it's an error page
      if (url.includes('error') || url.includes('invalid_client')) {
        console.log('❌ OAuth Error Detected');
        
        // Extract error details from URL
        const urlParams = new URL(url).searchParams;
        const authError = urlParams.get('authError');
        const clientId = urlParams.get('client_id');
        
        console.log(`   Error: ${authError}`);
        console.log(`   Client ID: ${clientId}`);
        
        // Check if client_id contains the problematic text
        if (clientId && clientId.includes('Specify --help')) {
          console.log('🚨 FOUND THE ISSUE: client_id contains help text instead of real OAuth client ID');
          console.log('   This indicates GOOGLE_CLIENT_ID environment variable is misconfigured');
        }
      } else {
        console.log('✅ Successful redirect to Google OAuth');
      }
    } catch (error) {
      console.log('⚠️  No redirect to Google OAuth detected');
      console.log(`   Error: ${error}`);
    }
  });
  
  test('check production environment variables indirectly', async ({ request }) => {
    console.log('🔍 Checking production configuration indirectly...');
    
    // Check if Google OAuth is configured by testing login endpoint behavior
    const loginResponse = await request.get(`${PRODUCTION_URL}/api/auth/google-login-url`);
    
    console.log(`📊 Google login URL endpoint status: ${loginResponse.status()}`);
    
    if (loginResponse.status() === 404) {
      console.log('ℹ️  Google OAuth endpoint not found - may not be configured');
    } else if (loginResponse.status() >= 400) {
      console.log('⚠️  Google OAuth endpoint error - configuration issue likely');
      
      try {
        const errorText = await loginResponse.text();
        console.log(`   Response: ${errorText.substring(0, 200)}...`);
        
        if (errorText.includes('invalid_client') || errorText.includes('OAuth')) {
          console.log('🎯 Confirmed: OAuth configuration issue detected via API');
        }
      } catch (e) {
        console.log('   Unable to parse error response');
      }
    } else {
      console.log('✅ Google OAuth endpoint responding normally');
    }
  });
  
  test('provide OAuth fix recommendations', async ({ page }) => {
    console.log('💡 OAuth Fix Recommendations:');
    console.log('');
    console.log('🔧 To fix the Google OAuth issue:');
    console.log('');
    console.log('1. Check current production environment variables:');
    console.log('   - GOOGLE_CLIENT_ID should be a valid Google OAuth client ID');
    console.log('   - Format: 123456789-abc123.apps.googleusercontent.com');
    console.log('   - NOT: "Specify --help for a list of available options and commands."');
    console.log('');
    console.log('2. Update production environment variables:');
    console.log('   az containerapp update --name your-app --resource-group your-rg \\');
    console.log('     --set-env-vars GOOGLE_CLIENT_ID=your-real-client-id');
    console.log('');
    console.log('3. Ensure Google Cloud Console OAuth client is configured:');
    console.log('   - Authorized redirect URIs include: https://app.aiprofilephotomaker.com/signin-google');
    console.log('   - Authorized JavaScript origins include: https://app.aiprofilephotomaker.com');
    console.log('');
    console.log('4. Use our enhanced secret validation:');
    console.log('   ./scripts/validate-secrets.sh Production');
    console.log('');
    console.log('5. Deploy after fixing environment variables');
    
    // This test always passes - it's just for documentation
    expect(true).toBe(true);
  });
});