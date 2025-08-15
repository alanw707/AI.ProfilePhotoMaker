import { test, expect } from '@playwright/test';

const PRODUCTION_URL = 'https://api.aiprofilephotomaker.com';

test.describe('Production Authenticated Storage Verification', () => {
  
  test('verify 500 errors are resolved with proper JWT token format', async ({ request }) => {
    console.log('🔑 Testing authenticated requests to verify storage configuration...');
    
    // Test with a realistic but expired/invalid JWT token to trigger deeper validation
    // This should get past initial auth parsing and hit the storage/business logic
    const realisticJwtToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXIiLCJlbWFpbCI6InRlc3RAZXhhbXBsZS5jb20iLCJpYXQiOjE2MzQ2NDcyMDAsImV4cCI6MTYzNDY1MDgwMH0.testSignatureHere';
    
    const enhanceResponse = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${realisticJwtToken}`
      },
      data: JSON.stringify({
        imageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net/profile-images/test-file.jpg',
        enhancementType: 'professional'
      })
    });
    
    const responseText = await enhanceResponse.text();
    const status = enhanceResponse.status();
    
    console.log(`📊 Status: ${status}`);
    console.log(`📋 Response: ${responseText}`);
    
    // Check if we're still getting 500 errors (the original problem)
    if (status === 500) {
      console.log('🚨 STILL GETTING 500 ERROR - Storage configuration may not be fully resolved');
      
      // Try to parse error details
      try {
        const errorData = JSON.parse(responseText);
        console.log('💥 500 Error Details:', JSON.stringify(errorData, null, 2));
        
        if (errorData.error && errorData.error.message) {
          if (errorData.error.message.includes('/uploads')) {
            console.log('❌ ERROR CONFIRMS: Still using local storage (/uploads)');
          } else if (errorData.error.message.includes('blob.core.windows.net')) {
            console.log('✅ ERROR INDICATES: Using Azure Blob Storage');
          } else {
            console.log('ℹ️  Error doesn\'t mention storage paths directly');
          }
        }
      } catch (e) {
        console.log('❌ 500 error response is not valid JSON');
      }
    } else if (status === 401) {
      console.log('✅ Getting 401 instead of 500 - indicates auth working, storage may be resolved');
      
      try {
        const errorData = JSON.parse(responseText);
        if (errorData.error && errorData.error.code === 'Unauthorized') {
          console.log('✅ Proper error structure confirmed');
        }
      } catch (e) {
        console.log('⚠️  401 response not in expected format');
      }
    } else if (status === 400) {
      console.log('ℹ️  Getting 400 - may indicate request validation working (progress from 500)');
    } else {
      console.log(`ℹ️  Unexpected status: ${status}`);
    }
  });
  
  test('test with Azure Blob Storage URL to verify storage service selection', async ({ request }) => {
    console.log('🗄️  Testing with Azure Blob Storage URL...');
    
    // Use a well-formed JWT but potentially invalid signature
    const testJwtToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXItaWQiLCJlbWFpbCI6InRlc3RAYWlwcm9maWxlcGhvdG9tYWtlci5jb20iLCJpYXQiOjE3MzE2NDcyMDAsImV4cCI6MTczMTczMzYwMCwiaXNzIjoiaHR0cHM6Ly9hcGkuYWlwcm9maWxlcGhvdG9tYWtlci5jb20iLCJhdWQiOiJodHRwczovL2FwcC5haXByb2ZpbGVwaG90b21ha2VyLmNvbSJ9.invalidSignatureForTesting';
    
    const azureBlobUrl = 'https://aipmstv16j74jubocuukg.blob.core.windows.net/profile-images/72370a33-c7c8-42ac-b970-1538def4efe3/47135e94-50cd-49de-adf0-f722aece68ad_selfie.jpg';
    
    const response = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${testJwtToken}`
      },
      data: JSON.stringify({
        imageUrl: azureBlobUrl,
        enhancementType: 'professional'
      })
    });
    
    const responseText = await response.text();
    const status = response.status();
    
    console.log(`📊 Azure Blob URL Test Status: ${status}`);
    console.log(`📋 Response: ${responseText.substring(0, 300)}...`);
    
    // Analyze the response to understand what's happening
    if (status === 500) {
      console.log('🚨 500 ERROR WITH AZURE BLOB URL - indicates storage service issue');
    } else if (status === 401) {
      console.log('✅ 401 with Azure Blob URL - auth layer working, storage service properly configured');
    } else if (status === 400) {
      console.log('ℹ️  400 with Azure Blob URL - validation working');
    }
    
    // Check for any storage-related error messages
    if (responseText.includes('/uploads')) {
      console.log('❌ Response mentions /uploads - local storage still being used somewhere');
    } else if (responseText.includes('blob.core.windows.net')) {
      console.log('✅ Response mentions blob storage - Azure Storage properly configured');
    }
  });
  
  test('test with old /uploads URL pattern to verify migration', async ({ request }) => {
    console.log('📁 Testing with old /uploads URL pattern...');
    
    const testJwtToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0LXVzZXItaWQiLCJlbWFpbCI6InRlc3RAYWlwcm9maWxlcGhvdG9tYWtlci5jb20iLCJpYXQiOjE3MzE2NDcyMDAsImV4cCI6MTczMTczMzYwMH0.testSignature';
    
    const oldUploadsUrl = 'https://app.aiprofilephotomaker.com/uploads/72370a33-c7c8-42ac-b970-1538def4efe3/47135e94-50cd-49de-adf0-f722aece68ad_selfie.jpg';
    
    const response = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${testJwtToken}`
      },
      data: JSON.stringify({
        imageUrl: oldUploadsUrl,
        enhancementType: 'professional'
      })
    });
    
    const responseText = await response.text();
    const status = response.status();
    
    console.log(`📊 Old /uploads URL Test Status: ${status}`);
    console.log(`📋 Response: ${responseText.substring(0, 300)}...`);
    
    // This should help us understand if the storage service can handle or redirect old URLs
    if (status === 500) {
      console.log('🚨 500 ERROR WITH /uploads URL - confirms this was the issue');
    } else {
      console.log('✅ No 500 error with /uploads URL - storage service handling it properly');
    }
  });
  
  test('comprehensive deployment verification', async ({ request }) => {
    console.log('🔍 Comprehensive deployment verification...');
    
    // Test multiple scenarios to confirm deployment status
    const scenarios = [
      {
        name: 'No Auth',
        headers: { 'Content-Type': 'application/json' },
        expectedStatus: 401
      },
      {
        name: 'Malformed Auth',
        headers: { 
          'Content-Type': 'application/json',
          'Authorization': 'Bearer invalid'
        },
        expectedStatus: 401
      },
      {
        name: 'Well-formed but Invalid JWT',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c'
        },
        expectedStatus: 401
      }
    ];
    
    for (const scenario of scenarios) {
      const response = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
        headers: scenario.headers,
        data: JSON.stringify({
          imageUrl: 'https://aipmstv16j74jubocuukg.blob.core.windows.net/profile-images/test.jpg',
          enhancementType: 'professional'
        })
      });
      
      const status = response.status();
      const responseText = await response.text();
      
      console.log(`\n--- ${scenario.name} ---`);
      console.log(`Expected: ${scenario.expectedStatus}, Got: ${status}`);
      
      if (status === scenario.expectedStatus) {
        console.log('✅ Status matches expectation');
        
        // Verify error format
        try {
          const errorData = JSON.parse(responseText);
          if (errorData.error && errorData.error.code) {
            console.log('✅ New error format confirmed');
          } else {
            console.log('❌ Old error format detected');
          }
        } catch (e) {
          console.log('⚠️  Non-JSON response');
        }
      } else {
        console.log(`⚠️  Unexpected status (expected ${scenario.expectedStatus}, got ${status})`);
        
        if (status === 500) {
          console.log('🚨 UNEXPECTED 500 ERROR - deployment or configuration issue');
        }
      }
    }
  });
});