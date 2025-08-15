import { test, expect } from '@playwright/test';

test.describe('Enhanced Endpoint Validation', () => {
  const API_BASE_URL = 'https://api.aiprofilephotomaker.com';

  test('should return improved error handling instead of 500', async ({ request }) => {
    // Test without authentication - should get proper 401 error
    const response = await request.post(`${API_BASE_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
      },
      data: {
        imageUrl: 'https://example.com/test.jpg',
        enhancementType: 'professional'
      }
    });

    expect(response.status()).toBe(401);
    
    const responseBody = await response.json();
    
    // Verify the new error format
    expect(responseBody).toHaveProperty('success', false);
    expect(responseBody).toHaveProperty('error');
    expect(responseBody.error).toHaveProperty('code', 'Unauthorized');
    expect(responseBody.error).toHaveProperty('message', 'Authentication required. Please provide a valid JWT token.');
    
    console.log('✅ Enhanced error handling is working properly');
    console.log('Response:', JSON.stringify(responseBody, null, 2));
  });

  test('should return proper error for invalid authentication', async ({ request }) => {
    const response = await request.post(`${API_BASE_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer invalid_token'
      },
      data: {
        imageUrl: 'https://example.com/test.jpg',
        enhancementType: 'professional'
      }
    });

    // Should still get proper error handling, not 500
    expect(response.status()).toBe(401);
    
    const responseBody = await response.json();
    expect(responseBody).toHaveProperty('success', false);
    expect(responseBody).toHaveProperty('error');
    expect(responseBody.error).toHaveProperty('code', 'Unauthorized');
    
    console.log('✅ Invalid token properly handled');
  });

  test('should test endpoint availability and not return old error message', async ({ request }) => {
    // This should NOT return the old error: "Failed to enhance photo. Please try again later"
    const response = await request.post(`${API_BASE_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
      },
      data: {
        imageUrl: 'https://example.com/test.jpg',
        enhancementType: 'professional'
      }
    });

    const responseBody = await response.json();
    
    // Make sure we don't get the old generic error message
    const responseText = JSON.stringify(responseBody);
    expect(responseText).not.toContain('Failed to enhance photo. Please try again later');
    expect(responseText).not.toContain('500');
    expect(responseText).not.toContain('Internal Server Error');
    
    console.log('✅ Old 500 error messages are no longer present');
  });

  test('should validate that configuration validation is working', async ({ request }) => {
    // The endpoint should be accessible (not returning 502/503 due to configuration issues)
    const response = await request.post(`${API_BASE_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
      },
      data: {}
    });

    // Should get 401 (auth error) or 400 (validation error), but NOT 500/502/503 (configuration error)
    expect([400, 401]).toContain(response.status());
    
    const responseBody = await response.json();
    
    // Verify we don't get configuration errors
    const responseText = JSON.stringify(responseBody);
    expect(responseText).not.toContain('temporarily unavailable');
    expect(responseText).not.toContain('ConfigurationError');
    
    console.log('✅ Configuration validation is working - no config errors detected');
  });
});