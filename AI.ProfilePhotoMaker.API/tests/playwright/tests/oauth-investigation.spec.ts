import { test, expect } from '@playwright/test';

test.describe('OAuth 500 Error Investigation', () => {
  const API_BASE_URL = 'https://api.aiprofilephotomaker.com';
  
  test('Direct OAuth endpoint - capture full error details', async ({ request, context }) => {
    console.log('\n=== OAUTH 500 ERROR INVESTIGATION ===\n');
    
    // Test 1: Basic OAuth endpoint call
    console.log('Test 1: Direct OAuth endpoint call');
    const response = await request.get(`${API_BASE_URL}/api/auth/external-login/google`, {
      ignoreHTTPSErrors: true,
      failOnStatusCode: false,
      headers: {
        'Accept': '*/*',
        'User-Agent': 'Playwright OAuth Debug'
      }
    });
    
    console.log('Status:', response.status());
    console.log('Status Text:', response.statusText());
    console.log('Headers:', response.headers());
    
    const body = await response.text().catch(() => '');
    console.log('Response Body:', body || '(empty)');
    
    // Test 2: Check if session middleware is working
    console.log('\n\nTest 2: Session endpoint test');
    const sessionResponse = await request.get(`${API_BASE_URL}/api/auth/test-redirect`, {
      ignoreHTTPSErrors: true,
      failOnStatusCode: false
    });
    
    console.log('Test Redirect Status:', sessionResponse.status());
    console.log('Test Redirect Headers:', sessionResponse.headers());
    
    // Test 3: Debug endpoints
    console.log('\n\nTest 3: Debug auth schemes');
    const debugResponse = await request.get(`${API_BASE_URL}/api/auth/debug/auth-schemes`, {
      ignoreHTTPSErrors: true,
      failOnStatusCode: false
    });
    
    console.log('Debug Status:', debugResponse.status());
    if (debugResponse.ok()) {
      const debugData = await debugResponse.json();
      console.log('Auth Schemes:', JSON.stringify(debugData, null, 2));
    } else {
      console.log('Debug Body:', await debugResponse.text().catch(() => '(empty)'));
    }
    
    // Test 4: Google OAuth debug endpoint
    console.log('\n\nTest 4: Google OAuth debug');
    const googleDebugResponse = await request.get(`${API_BASE_URL}/api/auth/debug/google-oauth`, {
      ignoreHTTPSErrors: true,
      failOnStatusCode: false
    });
    
    console.log('Google Debug Status:', googleDebugResponse.status());
    if (googleDebugResponse.ok()) {
      const googleData = await googleDebugResponse.json();
      console.log('Google OAuth Config:', JSON.stringify(googleData, null, 2));
    } else {
      console.log('Google Debug Body:', await googleDebugResponse.text().catch(() => '(empty)'));
    }
    
    // Test 5: Try with session cookie
    console.log('\n\nTest 5: OAuth with session attempt');
    
    // First, try to establish a session
    const page = await context.newPage();
    await page.goto(`${API_BASE_URL}/api/auth/test-redirect`, { 
      waitUntil: 'domcontentloaded',
      timeout: 10000 
    }).catch(e => console.log('Page navigation error:', e.message));
    
    // Now try OAuth with potential session
    const oauthWithSession = await request.get(`${API_BASE_URL}/api/auth/external-login/google`, {
      ignoreHTTPSErrors: true,
      failOnStatusCode: false,
      headers: {
        'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
      }
    });
    
    console.log('OAuth with Session Status:', oauthWithSession.status());
    console.log('OAuth with Session Headers:', oauthWithSession.headers());
    
    await page.close();
    
    // Test 6: Check health endpoint for comparison
    console.log('\n\nTest 6: Health check for comparison');
    const healthResponse = await request.get(`${API_BASE_URL}/api/health`, {
      ignoreHTTPSErrors: true,
      failOnStatusCode: false
    });
    
    console.log('Health Status:', healthResponse.status());
    if (healthResponse.ok()) {
      const healthData = await healthResponse.json();
      console.log('Health Data:', JSON.stringify(healthData, null, 2));
    }
    
    // Final analysis
    console.log('\n\n=== ANALYSIS SUMMARY ===');
    console.log('OAuth Endpoint Returns:', response.status());
    console.log('Body is empty:', !body);
    console.log('Session-based redirect works:', sessionResponse.status() === 302);
    console.log('Debug endpoints accessible:', debugResponse.ok() || googleDebugResponse.ok());
    console.log('Health check works:', healthResponse.ok());
    
    // Test assertions
    expect(response.status()).toBe(500);
    
    console.log('\n=== END INVESTIGATION ===\n');
  });
  
  test('Alternative: Direct HTTP client test', async ({ request }) => {
    console.log('\n=== ALTERNATIVE HTTP CLIENT TEST ===\n');
    
    // Test with minimal headers
    const minimalResponse = await request.get(`${API_BASE_URL}/api/auth/external-login/google`, {
      ignoreHTTPSErrors: true,
      failOnStatusCode: false,
      headers: {} // No headers at all
    });
    
    console.log('Minimal Headers Response:', minimalResponse.status());
    
    // Test with OPTIONS preflight
    const optionsResponse = await request.fetch(`${API_BASE_URL}/api/auth/external-login/google`, {
      method: 'OPTIONS',
      ignoreHTTPSErrors: true,
      failOnStatusCode: false
    });
    
    console.log('OPTIONS Response:', optionsResponse.status());
    console.log('CORS Headers:', Object.fromEntries(
      Object.entries(optionsResponse.headers())
        .filter(([key]) => key.toLowerCase().includes('access-control'))
    ));
    
    console.log('\n=== END ALTERNATIVE TEST ===\n');
  });
});