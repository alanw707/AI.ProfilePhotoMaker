import { test, expect } from '@playwright/test';

const PRODUCTION_URL = 'https://api.aiprofilephotomaker.com';

test.describe('Production 500 Error Investigation', () => {
  
  test('comprehensive 500 error check - all scenarios', async ({ request }) => {
    console.log('🔍 Comprehensive test for any remaining 500 errors');
    
    const testScenarios = [
      {
        name: 'No authentication',
        headers: { 'Content-Type': 'application/json' },
        data: { imageUrl: 'test.jpg', enhancementType: 'professional' }
      },
      {
        name: 'Invalid Bearer token',
        headers: { 
          'Content-Type': 'application/json',
          'Authorization': 'Bearer invalid-token-12345'
        },
        data: { imageUrl: 'test.jpg', enhancementType: 'professional' }
      },
      {
        name: 'Malformed JSON in body',
        headers: { 'Content-Type': 'application/json' },
        data: '{"imageUrl": "test.jpg", "enhancementType": "professional"' // Missing closing brace
      },
      {
        name: 'Missing required imageUrl field',
        headers: { 'Content-Type': 'application/json' },
        data: { enhancementType: 'professional' }
      },
      {
        name: 'Invalid enhancement type',
        headers: { 'Content-Type': 'application/json' },
        data: { imageUrl: 'test.jpg', enhancementType: 'invalid-type' }
      },
      {
        name: 'Empty request body',
        headers: { 'Content-Type': 'application/json' },
        data: ''
      },
      {
        name: 'Very large image URL',
        headers: { 'Content-Type': 'application/json' },
        data: { 
          imageUrl: 'data:image/png;base64,' + 'A'.repeat(10000), 
          enhancementType: 'professional' 
        }
      },
      {
        name: 'Invalid content-type header',
        headers: { 'Content-Type': 'text/plain' },
        data: { imageUrl: 'test.jpg', enhancementType: 'professional' }
      },
      {
        name: 'Multipart form data (wrong format)',
        headers: { 'Content-Type': 'multipart/form-data' },
        data: { imageUrl: 'test.jpg', enhancementType: 'professional' }
      },
      {
        name: 'SQL injection attempt in imageUrl',
        headers: { 'Content-Type': 'application/json' },
        data: { imageUrl: "'; DROP TABLE Users; --", enhancementType: 'professional' }
      }
    ];
    
    let found500Error = false;
    const results = [];
    
    for (const scenario of testScenarios) {
      console.log(`\n--- Testing: ${scenario.name} ---`);
      
      try {
        const response = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
          headers: scenario.headers,
          data: typeof scenario.data === 'string' ? scenario.data : JSON.stringify(scenario.data)
        });
        
        const status = response.status();
        const responseText = await response.text();
        const contentType = response.headers()['content-type'] || '';
        
        console.log(`Status: ${status}`);
        console.log(`Content-Type: ${contentType}`);
        console.log(`Response: ${responseText.substring(0, 100)}...`);
        
        results.push({
          scenario: scenario.name,
          status: status,
          contentType: contentType,
          response: responseText.substring(0, 200)
        });
        
        if (status === 500) {
          console.log(`🚨 FOUND 500 ERROR: ${scenario.name}`);
          console.log(`Full response: ${responseText}`);
          found500Error = true;
        }
        
        // Verify we get proper JSON responses for errors
        if (status >= 400) {
          expect(contentType).toContain('application/json');
          
          try {
            const errorJson = JSON.parse(responseText);
            expect(errorJson).toHaveProperty('success', false);
            expect(errorJson).toHaveProperty('error');
            expect(errorJson.error).toHaveProperty('code');
            expect(errorJson.error).toHaveProperty('message');
          } catch (parseError) {
            console.log(`❌ Invalid JSON response for ${scenario.name}: ${responseText}`);
          }
        }
        
      } catch (error) {
        console.log(`Request failed for ${scenario.name}: ${error.message}`);
        results.push({
          scenario: scenario.name,
          status: 'ERROR',
          error: error.message
        });
      }
      
      // Small delay between requests
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    
    // Save results
    console.log('\n📊 Test Results Summary:');
    console.log(JSON.stringify(results, null, 2));
    
    // Verify no 500 errors found
    expect(found500Error).toBe(false);
    
    console.log('\n✅ No 500 errors found in any test scenario!');
  });
  
  test('browser-like request with CORS preflight', async ({ request }) => {
    console.log('🌐 Testing browser-like request with CORS headers');
    
    // Simulate preflight OPTIONS request
    const optionsResponse = await request.fetch(`${PRODUCTION_URL}/api/replicate/enhance`, {
      method: 'OPTIONS',
      headers: {
        'Origin': 'https://app.aiprofilephotomaker.com',
        'Access-Control-Request-Method': 'POST',
        'Access-Control-Request-Headers': 'content-type,authorization'
      }
    });
    
    console.log(`OPTIONS status: ${optionsResponse.status()}`);
    
    // Now the actual POST request
    const postResponse = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
        'Origin': 'https://app.aiprofilephotomaker.com',
        'Referer': 'https://app.aiprofilephotomaker.com/'
      },
      data: JSON.stringify({
        imageUrl: 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChAI9jU77IQAAAABJRU5ErkJggg==',
        enhancementType: 'professional'
      })
    });
    
    const status = postResponse.status();
    const responseText = await postResponse.text();
    
    console.log(`POST status: ${status}`);
    console.log(`Response: ${responseText}`);
    
    // Should be 401 (auth required), not 500
    expect(status).not.toBe(500);
    expect(status).toBe(401);
    
    const responseJson = JSON.parse(responseText);
    expect(responseJson.success).toBe(false);
    expect(responseJson.error.code).toBe('Unauthorized');
  });
  
  test('stress test - multiple concurrent requests', async ({ request }) => {
    console.log('⚡ Stress testing with concurrent requests');
    
    const concurrentRequests = 5;
    const promises = [];
    
    for (let i = 0; i < concurrentRequests; i++) {
      const promise = request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
        headers: { 'Content-Type': 'application/json' },
        data: JSON.stringify({
          imageUrl: `test-image-${i}.jpg`,
          enhancementType: 'professional'
        })
      });
      promises.push(promise);
    }
    
    const responses = await Promise.all(promises);
    
    for (let i = 0; i < responses.length; i++) {
      const status = responses[i].status();
      console.log(`Request ${i + 1} status: ${status}`);
      
      // None should be 500
      expect(status).not.toBe(500);
      expect(status).toBe(401); // Should be auth error
    }
    
    console.log('✅ All concurrent requests handled correctly (no 500 errors)');
  });
});