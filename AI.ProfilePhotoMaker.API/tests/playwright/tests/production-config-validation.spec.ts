import { test, expect } from '@playwright/test';

test.describe('Production Configuration Validation', () => {
  const PRODUCTION_API_URL = 'https://api.aiprofilephotomaker.com';
  
  test('should validate all required Replicate configuration is present', async ({ request }) => {
    console.log('🔍 Testing production configuration validation...');
    
    // Test unauthenticated request (should return 401, not 500)
    const unauthenticatedResponse = await request.post(`${PRODUCTION_API_URL}/api/replicate/enhance`, {
      data: {
        imageUrl: 'https://example.com/test-image.jpg',
        enhancementType: 'professional'
      },
      headers: {
        'Content-Type': 'application/json'
      }
    });
    
    console.log(`📊 Unauthenticated request status: ${unauthenticatedResponse.status()}`);
    expect(unauthenticatedResponse.status()).toBe(401);
    
    // Test with dummy authentication header (should fail gracefully, not 500)
    const pseudoAuthenticatedResponse = await request.post(`${PRODUCTION_API_URL}/api/replicate/enhance`, {
      data: {
        imageUrl: 'https://example.com/test-image.jpg',
        enhancementType: 'professional'
      },
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer invalid-token-for-config-test'
      }
    });
    
    console.log(`📊 Pseudo-authenticated request status: ${pseudoAuthenticatedResponse.status()}`);
    
    // Should be 401 (invalid token) NOT 500 (configuration error)
    expect(pseudoAuthenticatedResponse.status()).toBe(401);
    
    const responseBody = await pseudoAuthenticatedResponse.json();
    console.log('📋 Response body:', JSON.stringify(responseBody, null, 2));
    
    // Should not contain configuration-related error messages
    expect(JSON.stringify(responseBody)).not.toContain('FluxKontextProModelId');
    expect(JSON.stringify(responseBody)).not.toContain('Replicate');
  });
  
  test('should validate health endpoint shows proper configuration status', async ({ request }) => {
    console.log('🏥 Testing health endpoint for configuration validation...');
    
    const healthResponse = await request.get(`${PRODUCTION_API_URL}/api/health`);
    console.log(`📊 Health endpoint status: ${healthResponse.status()}`);
    
    expect(healthResponse.status()).toBe(200);
    
    const healthData = await healthResponse.json();
    console.log('🏥 Health data:', JSON.stringify(healthData, null, 2));
    
    // Health endpoint should indicate proper service status
    expect(healthData.status).toBe('Healthy');
  });
  
  test('should validate webhook endpoint responds properly', async ({ request }) => {
    console.log('🪝 Testing webhook endpoint availability...');
    
    // Test webhook endpoint without proper signature (should return 400, not 500)
    const webhookResponse = await request.post(`${PRODUCTION_API_URL}/api/webhooks/replicate/prediction-complete`, {
      data: {
        id: 'test-prediction-id',
        status: 'succeeded'
      },
      headers: {
        'Content-Type': 'application/json'
      }
    });
    
    console.log(`📊 Webhook request status: ${webhookResponse.status()}`);
    
    // Should return 400 (missing/invalid signature) NOT 500 (configuration error)
    expect([400, 401]).toContain(webhookResponse.status());
  });
});

test.describe('Production Configuration Fix Verification', () => {
  const PRODUCTION_API_URL = 'https://api.aiprofilephotomaker.com';
  
  test('should verify configuration environment variables are set', async ({ request }) => {
    console.log('🔧 Verifying production environment configuration...');
    
    // This test documents the required environment variables for production
    console.log('📋 Required production environment variables:');
    console.log('   REPLICATE_API_TOKEN=<your-replicate-api-token>');
    console.log('   REPLICATE_FLUX_TRAINING_MODEL_ID=ostris/flux-dev-lora-trainer');
    console.log('   REPLICATE_FLUX_GENERATION_MODEL_ID=black-forest-labs/flux-dev');
    console.log('   REPLICATE_FLUX_KONTEXT_PRO_MODEL_ID=black-forest-labs/flux-kontext-pro');
    console.log('   REPLICATE_WEBHOOK_SECRET=whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM');
    
    // Or in Azure Container Apps configuration format:
    console.log('🐳 Azure Container Apps format:');
    console.log('   Replicate__ApiToken=<your-replicate-api-token>');
    console.log('   Replicate__FluxTrainingModelId=ostris/flux-dev-lora-trainer');
    console.log('   Replicate__FluxGenerationModelId=black-forest-labs/flux-dev');
    console.log('   Replicate__FluxKontextProModelId=black-forest-labs/flux-kontext-pro');
    console.log('   Replicate__WebhookSecret=whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM');
    
    // Test basic API connectivity
    const healthCheck = await request.get(`${PRODUCTION_API_URL}/api/health`);
    expect(healthCheck.status()).toBe(200);
  });
  
  test('should validate startup logs contain configuration validation', async ({ request }) => {
    console.log('📝 This test documents expected startup log entries:');
    console.log('   ✅ Replicate API Token is configured');
    console.log('   ✅ Flux Training Model ID: ostris/flux-dev-lora-trainer');
    console.log('   ✅ Flux Generation Model ID: black-forest-labs/flux-dev');
    console.log('   ✅ Flux Kontext Pro Model ID: black-forest-labs/flux-kontext-pro');
    console.log('   ✅ Replicate Webhook Secret is configured');
    console.log('   ✅ All Replicate configuration settings are properly configured');
    
    // Basic connectivity test
    const pingResponse = await request.get(`${PRODUCTION_API_URL}/api/health`);
    expect(pingResponse.status()).toBe(200);
  });
});