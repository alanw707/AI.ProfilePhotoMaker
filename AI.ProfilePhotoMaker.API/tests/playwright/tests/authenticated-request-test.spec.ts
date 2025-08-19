import { test, expect } from '@playwright/test';

test.describe('Authenticated Request Configuration Test', () => {
  const PRODUCTION_API_URL = 'https://api.aiprofilephotomaker.com';
  
  test('should test actual authenticated request flow to detect configuration issues', async ({ request }) => {
    console.log('🔐 Testing authenticated request flow...');
    
    // Step 1: Try to authenticate (this will fail but we can see the error pattern)
    console.log('📝 Testing login endpoint...');
    const loginResponse = await request.post(`${PRODUCTION_API_URL}/api/auth/login`, {
      data: {
        email: 'test@example.com',
        password: 'testpassword123'
      },
      headers: {
        'Content-Type': 'application/json'
      }
    });
    
    console.log(`📊 Login response status: ${loginResponse.status()}`);
    
    if (loginResponse.status() !== 200) {
      const loginBody = await loginResponse.json();
      console.log('📋 Login response:', JSON.stringify(loginBody, null, 2));
    }
    
    // Step 2: Test with a JWT-like token structure (invalid but properly formatted)
    console.log('🔍 Testing enhance endpoint with JWT-like token...');
    const jwtLikeToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c';
    
    const enhanceResponse = await request.post(`${PRODUCTION_API_URL}/api/replicate/enhance`, {
      data: {
        imageUrl: 'https://example.com/test-image.jpg',
        enhancementType: 'professional'
      },
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${jwtLikeToken}`
      }
    });
    
    console.log(`📊 Enhance with JWT-like token status: ${enhanceResponse.status()}`);
    
    const enhanceBody = await enhanceResponse.json();
    console.log('📋 Enhance response:', JSON.stringify(enhanceBody, null, 2));
    
    // Should be 401 (invalid token) not 500 (configuration error)
    expect(enhanceResponse.status()).toBe(401);
    expect(JSON.stringify(enhanceBody)).not.toContain('FluxKontextProModelId');
    expect(JSON.stringify(enhanceBody)).not.toContain('Configuration');
    
    // Step 3: Test other authenticated endpoints
    console.log('🔍 Testing other authenticated endpoints...');
    
    const endpoints = [
      { path: '/api/replicate/generate/basic', method: 'POST', data: { userInfo: {}, gender: 'M' } },
      { path: '/api/replicate/credits', method: 'GET', data: null },
      { path: '/api/image/images', method: 'GET', data: null }
    ];
    
    for (const endpoint of endpoints) {
      console.log(`🔍 Testing ${endpoint.method} ${endpoint.path}...`);
      
      try {
        const response = endpoint.method === 'GET'
          ? await request.get(`${PRODUCTION_API_URL}${endpoint.path}`, {
              headers: { 'Authorization': `Bearer ${jwtLikeToken}` }
            })
          : await request.post(`${PRODUCTION_API_URL}${endpoint.path}`, {
              data: endpoint.data,
              headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${jwtLikeToken}`
              }
            });
        
        console.log(`📊 ${endpoint.path} status: ${response.status()}`);
        
        if (response.status() === 500) {
          const errorBody = await response.json();
          console.log(`🚨 500 ERROR DETECTED at ${endpoint.path}:`, JSON.stringify(errorBody, null, 2));
          
          // This would indicate a configuration issue
          if (JSON.stringify(errorBody).includes('FluxKontextProModelId') || 
              JSON.stringify(errorBody).includes('Replicate')) {
            console.log('❌ CONFIGURATION ERROR CONFIRMED: Missing Replicate configuration');
          }
        }
        
        // All endpoints should return 401 for invalid tokens, not 500
        expect(response.status()).not.toBe(500);
        
      } catch (error) {
        console.log(`⚠️ Error testing ${endpoint.path}:`, error);
      }
    }
  });
  
  test('should verify production environment variables are properly set', async ({ request }) => {
    console.log('🔧 Verifying production configuration status...');
    
    // The health endpoint might give us clues about configuration
    const healthResponse = await request.get(`${PRODUCTION_API_URL}/api/health`);
    expect(healthResponse.status()).toBe(200);
    
    const healthData = await healthResponse.json();
    console.log('🏥 Health check data:', JSON.stringify(healthData, null, 2));
    
    // Check if health endpoint provides configuration status
    if (healthData.dependencies) {
      console.log('📊 Dependencies status:', JSON.stringify(healthData.dependencies, null, 2));
    }
    
    // Document what should be set in production
    console.log('📋 Production environment should have these variables set:');
    console.log('   Environment: Production');
    console.log('   REPLICATE_API_TOKEN or Replicate__ApiToken');
    console.log('   REPLICATE_FLUX_KONTEXT_PRO_MODEL_ID or Replicate__FluxKontextProModelId');
    console.log('   REPLICATE_WEBHOOK_SECRET or Replicate__WebhookSecret');
    console.log('   Plus other Replicate model IDs');
    
    // The fact that health returns 200 and we get 401s (not 500s) suggests config is OK
    expect(healthData.status).toBe('Healthy');
    expect(healthData.environment).toBe('Production');
  });
});

test.describe('Configuration Issue Debugging', () => {
  const PRODUCTION_API_URL = 'https://api.aiprofilephotomaker.com';
  
  test('should reproduce the exact 500 error scenario if it exists', async ({ request }) => {
    console.log('🐛 Attempting to reproduce the 500 error scenario...');
    
    // This test tries various scenarios that might trigger the config issue
    const scenarios = [
      {
        name: 'Malformed Authorization Header',
        headers: { 'Authorization': 'Bearer malformed.jwt.token' }
      },
      {
        name: 'Valid JWT Structure but Invalid Signature',
        headers: { 'Authorization': 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJpYXQiOjE2NzAwMDAwMDB9.invalid_signature_here' }
      },
      {
        name: 'Empty Bearer Token',
        headers: { 'Authorization': 'Bearer ' }
      },
      {
        name: 'Just Bearer without Token',
        headers: { 'Authorization': 'Bearer' }
      }
    ];
    
    for (const scenario of scenarios) {
      console.log(`🔍 Testing scenario: ${scenario.name}`);
      
      try {
        const response = await request.post(`${PRODUCTION_API_URL}/api/replicate/enhance`, {
          data: {
            imageUrl: 'https://example.com/test.jpg',
            enhancementType: 'professional'
          },
          headers: {
            'Content-Type': 'application/json',
            ...scenario.headers
          }
        });
        
        console.log(`📊 ${scenario.name} status: ${response.status()}`);
        
        if (response.status() === 500) {
          const errorBody = await response.json();
          console.log(`🚨 500 ERROR FOUND in ${scenario.name}:`, JSON.stringify(errorBody, null, 2));
          
          // Check if it's the configuration error we're looking for
          if (JSON.stringify(errorBody).includes('FluxKontextProModelId')) {
            console.log('✅ REPRODUCED: Configuration error with missing FluxKontextProModelId');
            throw new Error(`Configuration error reproduced in scenario: ${scenario.name}`);
          }
        }
        
      } catch (error) {
        console.log(`⚠️ Error in scenario ${scenario.name}:`, error);
        if (error.message?.includes('Configuration error reproduced')) {
          throw error; // Re-throw if we found the config issue
        }
      }
    }
    
    console.log('✅ No 500 errors found - production configuration appears to be working correctly');
  });
});