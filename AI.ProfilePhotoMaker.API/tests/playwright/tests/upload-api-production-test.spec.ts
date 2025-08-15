import { test, expect } from '@playwright/test';
import { readFileSync, writeFileSync } from 'fs';
import { join } from 'path';

const PRODUCTION_URL = 'https://api.aiprofilephotomaker.com';
const TEST_IMAGE_SIZE = 1024; // Small test image for safety

// Create minimal test image data (1x1 pixel PNG)
const createMinimalTestImage = (): Buffer => {
  // Minimal 1x1 PNG image (base64 decoded)
  const pngData = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChAI9jU77IQAAAABJRU5ErkJggg==';
  return Buffer.from(pngData, 'base64');
};

test.describe('Production Upload API Tests', () => {
  let testImageBuffer: Buffer;

  test.beforeAll(() => {
    testImageBuffer = createMinimalTestImage();
    console.log('🔍 Testing on PRODUCTION environment - using minimal safe payloads');
  });

  test('Health check endpoint should respond correctly', async ({ request }) => {
    const response = await request.get(`${PRODUCTION_URL}/api/health`);
    expect(response.status()).toBe(200);
    
    const health = await response.json();
    expect(health.status).toBe('Healthy');
    console.log('✅ Health check passed:', health);
  });

  test('Upload endpoint basic connectivity test', async ({ request }) => {
    console.log('🧪 Testing upload endpoint connectivity...');
    
    // Test OPTIONS request first (preflight for CORS)
    const optionsResponse = await request.fetch(`${PRODUCTION_URL}/api/replicate/enhance`, {
      method: 'OPTIONS'
    });
    
    console.log('OPTIONS response status:', optionsResponse.status());
    console.log('OPTIONS headers:', await optionsResponse.allHeaders());
  });

  test('Upload API with minimal test image', async ({ request }) => {
    console.log('🔬 Testing upload with minimal test payload...');
    
    try {
      const formData = new FormData();
      
      // Create a minimal file blob
      const testFile = new File([testImageBuffer], 'test-minimal.png', { 
        type: 'image/png' 
      });
      
      formData.append('images', testFile);
      formData.append('style', 'professional');
      
      console.log('📤 Sending request to:', `${PRODUCTION_URL}/api/replicate/enhance`);
      
      const response = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
        multipart: {
          images: {
            name: 'test-minimal.png',
            mimeType: 'image/png',
            buffer: testImageBuffer
          },
          style: 'professional'
        },
        timeout: 30000 // 30 second timeout for production safety
      });

      console.log('📥 Response status:', response.status());
      console.log('📥 Response headers:', await response.allHeaders());
      
      if (response.status() >= 400) {
        const errorText = await response.text();
        console.log('❌ Error response body:', errorText);
        
        // Log the specific error for analysis
        if (response.status() === 500) {
          console.log('🚨 500 ERROR REPRODUCED - Internal Server Error detected');
        }
      }
      
      // Don't assert on status for now, just log for observation
      const responseBody = await response.text();
      console.log('📄 Response body preview:', responseBody.substring(0, 500));
      
    } catch (error) {
      console.log('💥 Request failed with error:', error);
      throw error;
    }
  });

  test('Loop test - controlled minimal requests', async ({ request }) => {
    console.log('🔄 Starting controlled loop test (3 iterations for safety)...');
    
    const results = [];
    
    for (let i = 0; i < 3; i++) {
      console.log(`🔄 Loop iteration ${i + 1}/3`);
      
      try {
        const response = await request.post(`${PRODUCTION_URL}/api/replicate/enhance`, {
          multipart: {
            images: {
              name: `test-loop-${i}.png`,
              mimeType: 'image/png',
              buffer: testImageBuffer
            },
            style: 'professional'
          },
          timeout: 15000
        });
        
        results.push({
          iteration: i + 1,
          status: response.status(),
          timestamp: new Date().toISOString()
        });
        
        console.log(`✅ Loop ${i + 1}: Status ${response.status()}`);
        
        if (response.status() === 500) {
          console.log(`🚨 500 ERROR on iteration ${i + 1}`);
          // Continue to gather more data points
        }
        
        // Small delay between requests for production safety
        await new Promise(resolve => setTimeout(resolve, 2000));
        
      } catch (error) {
        console.log(`💥 Loop ${i + 1} failed:`, error);
        results.push({
          iteration: i + 1,
          status: 'ERROR',
          error: error.message,
          timestamp: new Date().toISOString()
        });
      }
    }
    
    console.log('📊 Loop test results:', JSON.stringify(results, null, 2));
    
    // Save results for analysis
    const resultsPath = join(__dirname, '..', 'results', `upload-test-results-${Date.now()}.json`);
    writeFileSync(resultsPath, JSON.stringify({
      testType: 'production-upload-loop',
      timestamp: new Date().toISOString(),
      results
    }, null, 2));
    
    console.log('💾 Results saved to:', resultsPath);
  });
});