import { test, expect } from '@playwright/test';

const PRODUCTION_URL = 'https://api.aiprofilephotomaker.com';

test.describe('Production Storage and Error Message Verification', () => {
  
  test('verify storage URLs point to blob storage not /uploads', async ({ request }) => {
    console.log('🔍 Testing if storage URLs now point to Azure Blob Storage...');
    
    // Test the health endpoint to ensure we're hitting the right version
    const healthResponse = await request.get(`${PRODUCTION_URL}/api/health`);
    const healthData = await healthResponse.json();
    console.log('📊 Health check:', healthData);
    
    // Test upload endpoint to see what kind of URLs are generated
    // Since we need auth for uploads, we'll test the enhance endpoint error to see if it mentions /uploads
    const enhanceResponse = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
      headers: { 'Content-Type': 'application/json' },
      data: JSON.stringify({
        imageUrl: 'https://app.aiprofilephotomaker.com/uploads/test-file.jpg', // Simulate old /uploads URL
        enhancementType: 'professional'
      })
    });
    
    const enhanceData = await enhanceResponse.text();
    console.log('🔍 Enhance endpoint response:', enhanceData);
    console.log('📈 Status code:', enhanceResponse.status());
    
    // Check if we're still getting old error messages or new ones
    try {
      const errorJson = JSON.parse(enhanceData);
      console.log('🎯 Parsed error response:', JSON.stringify(errorJson, null, 2));
      
      // Check if we get new error structure
      if (errorJson.error && errorJson.error.code && errorJson.error.message) {
        console.log('✅ NEW ERROR FORMAT DETECTED!');
        console.log(`   Code: ${errorJson.error.code}`);
        console.log(`   Message: ${errorJson.error.message}`);
      } else {
        console.log('❌ OLD ERROR FORMAT - deployment may not have taken effect');
      }
    } catch (parseError) {
      console.log('❌ Non-JSON response (might be HTML error page)');
      console.log('📝 Raw response:', enhanceData.substring(0, 500));
    }
  });
  
  test('comprehensive error message analysis', async ({ request }) => {
    console.log('🧪 Testing various scenarios to check error message formats...');
    
    const testScenarios = [
      {
        name: 'No authentication',
        headers: { 'Content-Type': 'application/json' },
        data: { imageUrl: 'test.jpg', enhancementType: 'professional' },
        expectedNewError: 'Unauthorized'
      },
      {
        name: 'Invalid token format',
        headers: { 
          'Content-Type': 'application/json',
          'Authorization': 'Bearer invalid-jwt-token'
        },
        data: { imageUrl: 'test.jpg', enhancementType: 'professional' },
        expectedNewError: 'Unauthorized'
      },
      {
        name: 'Simulated old /uploads URL',
        headers: { 'Content-Type': 'application/json' },
        data: { 
          imageUrl: 'https://app.aiprofilephotomaker.com/uploads/72370a33-c7c8-42ac-b970-1538def4efe3/47135e94-50cd-49de-adf0-f722aece68ad_selfie.jpg', 
          enhancementType: 'professional' 
        },
        expectedNewError: 'Unauthorized'
      }
    ];
    
    for (const scenario of testScenarios) {
      console.log(`\n--- Testing: ${scenario.name} ---`);
      
      const response = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
        headers: scenario.headers,
        data: JSON.stringify(scenario.data)
      });
      
      const responseText = await response.text();
      const status = response.status();
      
      console.log(`Status: ${status}`);
      console.log(`Response: ${responseText.substring(0, 200)}...`);
      
      // Check for old vs new error messages
      if (responseText.includes('Failed to enhance photo. Please try again later')) {
        console.log(`❌ OLD ERROR MESSAGE DETECTED in ${scenario.name}`);
        console.log('   This suggests deployment didn\'t take effect');
      } else {
        try {
          const errorJson = JSON.parse(responseText);
          if (errorJson.error && errorJson.error.code === scenario.expectedNewError) {
            console.log(`✅ NEW ERROR FORMAT WORKING for ${scenario.name}`);
            console.log(`   Code: ${errorJson.error.code}, Message: ${errorJson.error.message}`);
          } else {
            console.log(`⚠️  UNEXPECTED RESPONSE for ${scenario.name}:`, errorJson);
          }
        } catch (parseError) {
          console.log(`❌ NON-JSON RESPONSE for ${scenario.name}`);
        }
      }
      
      // Small delay between requests
      await new Promise(resolve => setTimeout(resolve, 500));
    }
  });
  
  test('check for blob storage URL patterns in any endpoints', async ({ request }) => {
    console.log('🔎 Checking if any endpoints return Azure Blob Storage URLs...');
    
    // Check health endpoint for any URL patterns
    const healthResponse = await request.get(`${PRODUCTION_URL}/api/health`);
    const healthText = await healthResponse.text();
    
    console.log('Health response:', healthText);
    
    // Look for any Azure blob storage URLs in the response
    const blobStoragePattern = /https:\/\/[a-z0-9]+\.blob\.core\.windows\.net/gi;
    const uploadsPattern = /\/uploads\//gi;
    
    const blobMatches = healthText.match(blobStoragePattern);
    const uploadsMatches = healthText.match(uploadsPattern);
    
    if (blobMatches) {
      console.log('✅ Found blob storage URLs:', blobMatches);
    } else {
      console.log('ℹ️  No blob storage URLs found in health endpoint');
    }
    
    if (uploadsMatches) {
      console.log('⚠️  Still found /uploads references:', uploadsMatches);
    } else {
      console.log('✅ No /uploads references found in health endpoint');
    }
    
    // Test if startup logs indicate storage service being used
    // We can't directly access logs, but we can infer from behavior
    
    console.log('\n📋 Analysis Summary:');
    console.log('- Health endpoint accessible:', healthResponse.status() === 200);
    console.log('- Response format: JSON');
    console.log('- Environment: Production');
  });
  
  test('test authentication and storage flow integration', async ({ request }) => {
    console.log('🔗 Testing if authentication + storage configuration work together...');
    
    // Simulate what happens when we provide a realistic but invalid JWT token
    const tokenResponse = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c'
      },
      data: JSON.stringify({
        imageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net/profile-images/test-file.jpg',
        enhancementType: 'professional'
      })
    });
    
    const tokenResponseText = await tokenResponse.text();
    const tokenStatus = tokenResponse.status();
    
    console.log('🔑 Auth test results:');
    console.log(`   Status: ${tokenStatus}`);
    console.log(`   Response: ${tokenResponseText}`);
    
    // Check if this gives us more detailed error information
    if (tokenStatus === 500) {
      console.log('🚨 STILL GETTING 500 ERROR - Storage or auth issue not fully resolved');
    } else if (tokenStatus === 401) {
      console.log('✅ Getting 401 as expected - auth working, proceeding to check error format');
      
      try {
        const errorData = JSON.parse(tokenResponseText);
        if (errorData.error && errorData.error.code) {
          console.log('✅ NEW ERROR FORMAT CONFIRMED');
          console.log(`   Error Code: ${errorData.error.code}`);
          console.log(`   Error Message: ${errorData.error.message}`);
        }
      } catch (e) {
        console.log('❌ Error response not in expected JSON format');
      }
    }
  });
});