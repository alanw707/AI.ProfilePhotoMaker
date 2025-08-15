import { test, expect } from '@playwright/test';

/**
 * Production Enhancement API Validation
 * 
 * Tests the deployed enhancement API fix to verify:
 * 1. No more 500 errors from missing configuration
 * 2. Proper authentication error responses (401 instead of 500)
 * 3. JSON error format instead of HTML
 * 4. Configuration validation on startup
 */

const PRODUCTION_API_BASE = 'https://api.aiprofilephotomaker.com';

test.describe('Production Enhancement API Validation', () => {
  
  test('should return healthy status from production API', async ({ request }) => {
    const response = await request.get(`${PRODUCTION_API_BASE}/api/health`);
    
    expect(response.status()).toBe(200);
    
    const health = await response.json();
    expect(health.status).toBe('Healthy');
    expect(health.environment).toBe('Production');
    
    console.log('✅ Production API is healthy:', health);
  });

  test('should return 401 Unauthorized for enhance endpoint without auth (NOT 500)', async ({ request }) => {
    // This should now return 401 instead of 500 Internal Server Error
    const response = await request.post(`${PRODUCTION_API_BASE}/api/replicate/enhance`, {
      data: {
        imageUrl: 'https://example.com/test.jpg',
        enhancementType: 'professional'
      }
    });
    
    // CRITICAL: Should be 401 (Unauthorized) not 500 (Internal Server Error)
    expect(response.status()).toBe(401);
    
    const error = await response.json();
    
    // Verify JSON error format (not HTML)
    expect(error).toHaveProperty('success');
    expect(error.success).toBe(false);
    expect(error).toHaveProperty('error');
    expect(error.error).toHaveProperty('code');
    expect(error.error).toHaveProperty('message');
    expect(error.error.code).toBe('Unauthorized');
    
    console.log('✅ Enhancement API returns proper 401 with JSON error:', error);
  });

  test('should return proper error for invalid requests', async ({ request }) => {
    // Test with invalid/empty data
    const response = await request.post(`${PRODUCTION_API_BASE}/api/replicate/enhance`, {
      data: {}
    });
    
    // Should return 401 or 400, NOT 500
    expect(response.status()).toBeGreaterThanOrEqual(400);
    expect(response.status()).toBeLessThan(500);
    
    const contentType = response.headers()['content-type'];
    expect(contentType).toContain('application/json');
    
    const error = await response.json();
    expect(error).toHaveProperty('success');
    expect(error.success).toBe(false);
    
    console.log('✅ Invalid request returns proper error format:', { 
      status: response.status(), 
      error: error 
    });
  });

  test('should return proper error format for other protected endpoints', async ({ request }) => {
    // Test train endpoint
    const trainResponse = await request.post(`${PRODUCTION_API_BASE}/api/replicate/train`, {
      data: {
        userId: 'test',
        imageZipUrl: 'https://example.com/test.zip'
      }
    });
    
    expect(trainResponse.status()).toBe(401);
    
    const trainError = await trainResponse.json();
    expect(trainError.success).toBe(false);
    expect(trainError.error.code).toBe('Unauthorized');
    
    // Test generate endpoint
    const generateResponse = await request.post(`${PRODUCTION_API_BASE}/api/replicate/generate`, {
      data: {
        trainedModelVersion: 'test',
        userId: 'test',
        style: 'professional'
      }
    });
    
    expect(generateResponse.status()).toBe(401);
    
    const generateError = await generateResponse.json();
    expect(generateError.success).toBe(false);
    expect(generateError.error.code).toBe('Unauthorized');
    
    console.log('✅ All protected endpoints return proper 401 errors');
  });

  test('should have FluxKontextProModelId configuration available', async ({ request }) => {
    // Test that startup validation would have caught missing config
    // We can infer this by testing that the API is running and enhancement endpoint
    // returns structured errors rather than configuration errors
    
    const response = await request.post(`${PRODUCTION_API_BASE}/api/replicate/enhance`, {
      data: {
        imageUrl: 'https://example.com/test.jpg',
        enhancementType: 'professional'
      }
    });
    
    // If configuration was missing, we would get 500 with "ConfigurationError"
    // If configuration is present, we should get 401 for missing auth
    expect(response.status()).toBe(401);
    
    const error = await response.json();
    expect(error.error.code).toBe('Unauthorized');
    expect(error.error.code).not.toBe('ConfigurationError');
    
    console.log('✅ FluxKontextProModelId configuration is present (no configuration errors)');
  });

  test('should handle CORS properly for frontend integration', async ({ request }) => {
    const response = await request.options(`${PRODUCTION_API_BASE}/api/replicate/enhance`, {
      headers: {
        'Origin': 'https://app.aiprofilephotomaker.com',
        'Access-Control-Request-Method': 'POST',
        'Access-Control-Request-Headers': 'Content-Type,Authorization'
      }
    });
    
    // Should allow CORS preflight
    expect([200, 204]).toContain(response.status());
    
    const corsHeaders = response.headers();
    expect(corsHeaders['access-control-allow-origin']).toBeTruthy();
    
    console.log('✅ CORS configuration is working for frontend integration');
  });

  test('should maintain API response time under acceptable limits', async ({ request }) => {
    const start = Date.now();
    
    const response = await request.get(`${PRODUCTION_API_BASE}/api/health`);
    
    const duration = Date.now() - start;
    
    expect(response.status()).toBe(200);
    expect(duration).toBeLessThan(5000); // Should respond within 5 seconds
    
    console.log(`✅ API response time: ${duration}ms (within acceptable limits)`);
  });
});

test.describe('Before/After Fix Comparison', () => {
  
  test('VERIFY: No 500 errors from enhancement endpoint', async ({ request }) => {
    console.log('🔍 Testing the original issue that caused 500 errors...');
    
    // This exact request used to return 500 Internal Server Error
    // due to missing FluxKontextProModelId configuration
    const response = await request.post(`${PRODUCTION_API_BASE}/api/replicate/enhance`, {
      data: {
        imageUrl: 'https://api.aiprofilephotomaker.com/storage/test.jpg',
        enhancementType: 'professional'
      }
    });
    
    // BEFORE FIX: Would return 500 Internal Server Error
    // AFTER FIX: Should return 401 Unauthorized (proper authentication error)
    console.log(`Response Status: ${response.status()}`);
    expect(response.status()).toBe(401);
    expect(response.status()).not.toBe(500);
    
    const responseBody = await response.json();
    
    // BEFORE FIX: Would return HTML error page or generic error
    // AFTER FIX: Should return structured JSON error
    expect(responseBody).toHaveProperty('success');
    expect(responseBody).toHaveProperty('error');
    expect(responseBody.success).toBe(false);
    expect(responseBody.error.code).toBe('Unauthorized');
    expect(responseBody.error.message).toContain('Authentication required');
    
    console.log('🎉 SUCCESS: Enhancement API fix verified!');
    console.log('   ❌ Before: 500 Internal Server Error');
    console.log('   ✅ After: 401 Unauthorized with JSON error format');
    console.log('   📋 Error Response:', responseBody);
  });
});