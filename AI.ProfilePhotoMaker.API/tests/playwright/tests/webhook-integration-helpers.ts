import { Page, APIResponse, expect } from '@playwright/test';
import { PERFORMANCE_THRESHOLDS } from './test-data';

/**
 * Webhook Integration Test Helpers
 * 
 * Helper functions specifically designed for testing webhook integration
 * in the enhanced photo workflow after webhook migration.
 */

export interface WebhookValidationResult {
  success: boolean;
  webhookUrl?: string;
  responseTime: number;
  statusCode?: number;
  signature?: string;
  payload?: any;
  error?: string;
}

export interface PredictionStatus {
  id: string;
  status: 'starting' | 'processing' | 'succeeded' | 'failed' | 'canceled';
  completed: boolean;
  webhook_completed: boolean;
  output?: string[];
  error?: string;
  logs?: string;
  created_at: string;
  started_at?: string;
  completed_at?: string;
}

export interface DatabaseImageRecord {
  id: number;
  userProfileId: number;
  originalImageUrl: string;
  processedImageUrl: string;
  style: string;
  isGenerated: boolean;
  createdAt: string;
  scheduledDeletionDate?: string;
}

/**
 * Generate HMAC signature for webhook testing
 */
export function generateWebhookSignature(payload: string, secret: string): string {
  // This would implement HMAC-SHA256 signature generation
  // For testing purposes, return a mock signature
  return `sha256=mock_signature_${Buffer.from(payload).toString('base64').slice(0, 10)}`;
}

/**
 * Create a test webhook payload for prediction completion
 */
export function createPredictionWebhookPayload(
  predictionId: string,
  userId: string,
  style: string = 'professional',
  outputUrls: string[] = ['https://example.com/output1.jpg']
): any {
  return {
    id: predictionId,
    version: 'test-version',
    created_at: new Date().toISOString(),
    started_at: new Date().toISOString(),
    completed_at: new Date().toISOString(),
    status: 'succeeded',
    input: {
      user_id: userId,
      style: style,
      input_image: 'https://example.com/input.jpg',
      prompt: 'Professional headshot enhancement'
    },
    output: outputUrls,
    error: null,
    logs: 'Processing completed successfully',
    metrics: {
      predict_time: 15.234567
    }
  };
}

/**
 * Send webhook request with proper signature
 */
export async function sendWebhookRequest(
  page: Page,
  webhookUrl: string,
  payload: any,
  secret: string = 'test-webhook-secret'
): Promise<WebhookValidationResult> {
  const startTime = Date.now();
  
  try {
    const payloadString = JSON.stringify(payload);
    const signature = generateWebhookSignature(payloadString, secret);
    
    const response = await page.request.post(webhookUrl, {
      data: payload,
      headers: {
        'Content-Type': 'application/json',
        'Replicate-Signature': signature,
        'Replicate-Timestamp': Math.floor(Date.now() / 1000).toString()
      }
    });
    
    const responseTime = Date.now() - startTime;
    const responseData = await response.json().catch(() => null);
    
    return {
      success: response.ok(),
      responseTime,
      statusCode: response.status(),
      signature,
      payload: responseData,
      error: responseData?.error
    };
  } catch (error) {
    return {
      success: false,
      responseTime: Date.now() - startTime,
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Monitor prediction status until completion or timeout
 */
export async function monitorPredictionStatus(
  page: Page,
  predictionId: string,
  apiBaseUrl: string,
  timeoutMs: number = 120000,
  pollIntervalMs: number = 3000
): Promise<PredictionStatus | null> {
  const startTime = Date.now();
  
  while (Date.now() - startTime < timeoutMs) {
    try {
      const response = await page.request.get(`${apiBaseUrl}/predictions/${predictionId}`);
      
      if (response.ok()) {
        const status: PredictionStatus = await response.json();
        
        // Check if prediction is completed
        if (status.status === 'succeeded' || status.status === 'failed' || status.status === 'canceled') {
          return status;
        }
        
        // Check if webhook has been processed (custom field)
        if (status.webhook_completed) {
          return status;
        }
      }
      
      await page.waitForTimeout(pollIntervalMs);
    } catch (error) {
      console.warn(`Error monitoring prediction ${predictionId}:`, error);
      await page.waitForTimeout(pollIntervalMs);
    }
  }
  
  return null; // Timeout
}

/**
 * Verify database records were created by webhook processing
 */
export async function verifyWebhookDatabaseUpdate(
  page: Page,
  userId: string,
  apiBaseUrl: string,
  expectedImageCount: number = 1
): Promise<{
  success: boolean;
  records: DatabaseImageRecord[];
  error?: string;
}> {
  try {
    const response = await page.request.get(`${apiBaseUrl}/users/${userId}/images?source=webhook`);
    
    if (!response.ok()) {
      return {
        success: false,
        records: [],
        error: `API request failed: ${response.status()}`
      };
    }
    
    const data = await response.json();
    const records: DatabaseImageRecord[] = data.images || [];
    
    return {
      success: records.length >= expectedImageCount,
      records,
      error: records.length < expectedImageCount ? 
        `Expected ${expectedImageCount} images, found ${records.length}` : undefined
    };
  } catch (error) {
    return {
      success: false,
      records: [],
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Test webhook URL resolution consistency
 */
export async function validateWebhookUrlResolution(
  page: Page,
  apiBaseUrl: string
): Promise<{
  success: boolean;
  webhookUrl?: string;
  isHttps?: boolean;
  hasNgrok?: boolean;
  error?: string;
}> {
  try {
    const response = await page.request.get(`${apiBaseUrl}/webhook-info`);
    
    if (!response.ok()) {
      return {
        success: false,
        error: `Webhook info request failed: ${response.status()}`
      };
    }
    
    const data = await response.json();
    const webhookUrl = data.webhookUrl;
    
    if (!webhookUrl) {
      return {
        success: false,
        error: 'Webhook URL not found in response'
      };
    }
    
    const isHttps = webhookUrl.startsWith('https://');
    const hasNgrok = webhookUrl.includes('ngrok');
    const hasCorrectEndpoint = webhookUrl.includes('/api/webhooks/replicate/prediction-complete');
    
    return {
      success: hasCorrectEndpoint,
      webhookUrl,
      isHttps,
      hasNgrok,
      error: !hasCorrectEndpoint ? 'Webhook URL does not contain expected endpoint' : undefined
    };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Create test user and return authentication details
 */
export async function createTestUser(
  page: Page,
  apiBaseUrl: string,
  testId: string
): Promise<{
  success: boolean;
  userId?: string;
  authToken?: string;
  error?: string;
}> {
  try {
    const testUserData = {
      userId: `test-user-${testId}`,
      email: `test-${testId}@example.com`,
      name: `Test User ${testId}`
    };
    
    const response = await page.request.post(`${apiBaseUrl}/test/create-user`, {
      data: testUserData
    });
    
    if (response.ok()) {
      const data = await response.json();
      return {
        success: true,
        userId: data.userId,
        authToken: data.authToken
      };
    } else {
      return {
        success: false,
        error: `User creation failed: ${response.status()}`
      };
    }
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Upload test image and return URL
 */
export async function uploadTestImage(
  page: Page,
  apiBaseUrl: string,
  authToken: string,
  testId: string
): Promise<{
  success: boolean;
  imageUrl?: string;
  imageId?: string;
  error?: string;
}> {
  try {
    // Create a test image blob
    const testImageData = 'data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQH/2wBDAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQH/wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAv/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAAAAAAAAAAAAAAAAX/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIRAxEAPwA/8A';
    
    const response = await page.request.post(`${apiBaseUrl}/upload-image`, {
      data: {
        image: testImageData,
        fileName: `test-image-${testId}.jpg`,
        testMode: true
      },
      headers: {
        'Authorization': `Bearer ${authToken}`,
        'Content-Type': 'application/json'
      }
    });
    
    if (response.ok()) {
      const data = await response.json();
      return {
        success: true,
        imageUrl: data.imageUrl,
        imageId: data.imageId
      };
    } else {
      return {
        success: false,
        error: `Image upload failed: ${response.status()}`
      };
    }
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Trigger photo enhancement request
 */
export async function triggerPhotoEnhancement(
  page: Page,
  apiBaseUrl: string,
  authToken: string,
  imageUrl: string,
  userId: string,
  enhancementType: string = 'professional'
): Promise<{
  success: boolean;
  predictionId?: string;
  statusCode?: number;
  error?: string;
}> {
  try {
    const enhancementData = {
      userId,
      imageUrl,
      enhancementType
    };
    
    const response = await page.request.post(`${apiBaseUrl}/enhance-photo`, {
      data: enhancementData,
      headers: {
        'Authorization': `Bearer ${authToken}`,
        'Content-Type': 'application/json'
      }
    });
    
    const responseData = await response.json().catch(() => ({}));
    
    return {
      success: response.ok(),
      predictionId: responseData.predictionId || responseData.id,
      statusCode: response.status(),
      error: responseData.error
    };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Simulate webhook delivery failure scenarios
 */
export async function simulateWebhookFailure(
  page: Page,
  webhookUrl: string,
  failureType: 'timeout' | 'signature' | 'payload' | 'network',
  predictionId: string,
  userId: string
): Promise<WebhookValidationResult> {
  const basePayload = createPredictionWebhookPayload(predictionId, userId);
  
  switch (failureType) {
    case 'timeout':
      // Simulate timeout by using a very short timeout
      return await sendWebhookRequestWithTimeout(page, webhookUrl, basePayload, 1);
      
    case 'signature':
      // Send with invalid signature
      return await sendWebhookWithInvalidSignature(page, webhookUrl, basePayload);
      
    case 'payload':
      // Send malformed payload
      const malformedPayload = { ...basePayload, status: null, output: 'invalid' };
      return await sendWebhookRequest(page, webhookUrl, malformedPayload);
      
    case 'network':
      // Simulate network interruption
      return await simulateNetworkInterruption(page, webhookUrl, basePayload);
      
    default:
      throw new Error(`Unknown failure type: ${failureType}`);
  }
}

/**
 * Send webhook request with custom timeout
 */
async function sendWebhookRequestWithTimeout(
  page: Page,
  webhookUrl: string,
  payload: any,
  timeoutMs: number
): Promise<WebhookValidationResult> {
  const startTime = Date.now();
  
  try {
    const response = await page.request.post(webhookUrl, {
      data: payload,
      headers: { 'Content-Type': 'application/json' },
      timeout: timeoutMs
    });
    
    return {
      success: false,
      responseTime: Date.now() - startTime,
      error: 'Expected timeout but request succeeded'
    };
  } catch (error) {
    return {
      success: true, // Success means we caught the expected timeout
      responseTime: Date.now() - startTime,
      error: error instanceof Error ? error.message : 'Timeout occurred as expected'
    };
  }
}

/**
 * Send webhook with invalid signature
 */
async function sendWebhookWithInvalidSignature(
  page: Page,
  webhookUrl: string,
  payload: any
): Promise<WebhookValidationResult> {
  const startTime = Date.now();
  
  try {
    const response = await page.request.post(webhookUrl, {
      data: payload,
      headers: {
        'Content-Type': 'application/json',
        'Replicate-Signature': 'sha256=invalid_signature',
        'Replicate-Timestamp': Math.floor(Date.now() / 1000).toString()
      }
    });
    
    const responseTime = Date.now() - startTime;
    
    return {
      success: !response.ok() && (response.status() === 401 || response.status() === 403),
      responseTime,
      statusCode: response.status(),
      error: response.ok() ? 'Expected signature validation to fail but request succeeded' : undefined
    };
  } catch (error) {
    return {
      success: false,
      responseTime: Date.now() - startTime,
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Simulate network interruption
 */
async function simulateNetworkInterruption(
  page: Page,
  webhookUrl: string,
  payload: any
): Promise<WebhookValidationResult> {
  // This would require more sophisticated network simulation
  // For now, just test with an invalid URL to simulate network failure
  const invalidUrl = webhookUrl.replace('https://', 'https://invalid-');
  
  const startTime = Date.now();
  
  try {
    const response = await page.request.post(invalidUrl, {
      data: payload,
      headers: { 'Content-Type': 'application/json' },
      timeout: 5000
    });
    
    return {
      success: false,
      responseTime: Date.now() - startTime,
      error: 'Expected network failure but request succeeded'
    };
  } catch (error) {
    return {
      success: true, // Success means we caught the expected network failure
      responseTime: Date.now() - startTime,
      error: 'Network interruption simulated successfully'
    };
  }
}

/**
 * Cleanup test data after webhook tests
 */
export async function cleanupTestData(
  page: Page,
  apiBaseUrl: string,
  testId: string,
  authToken?: string
): Promise<void> {
  try {
    if (authToken) {
      await page.request.delete(`${apiBaseUrl}/test/cleanup/${testId}`, {
        headers: {
          'Authorization': `Bearer ${authToken}`
        }
      });
    } else {
      await page.request.delete(`${apiBaseUrl}/test/cleanup/${testId}`);
    }
  } catch (error) {
    console.warn(`Cleanup failed for test ${testId}:`, error);
  }
}

/**
 * Generate performance report for webhook tests
 */
export function generateWebhookPerformanceReport(
  testResults: any[],
  testName: string
): string {
  const totalTests = testResults.length;
  const successfulTests = testResults.filter(r => r.success).length;
  const avgResponseTime = testResults
    .filter(r => r.responseTime)
    .reduce((sum, r) => sum + r.responseTime, 0) / 
    (testResults.filter(r => r.responseTime).length || 1);
  
  const webhookTests = testResults.filter(r => r.webhookReceived !== undefined);
  const successfulWebhooks = webhookTests.filter(r => r.webhookReceived).length;
  
  return `
📊 WEBHOOK PERFORMANCE REPORT: ${testName}
${'='.repeat(60)}

📈 OVERALL METRICS:
   Total Tests: ${totalTests}
   Successful: ${successfulTests} (${((successfulTests / totalTests) * 100).toFixed(1)}%)
   Failed: ${totalTests - successfulTests}
   Average Response Time: ${avgResponseTime.toFixed(0)}ms

🔗 WEBHOOK METRICS:
   Webhook Tests: ${webhookTests.length}
   Successful Webhooks: ${successfulWebhooks}
   Webhook Success Rate: ${webhookTests.length > 0 ? ((successfulWebhooks / webhookTests.length) * 100).toFixed(1) : 0}%

📋 PERFORMANCE THRESHOLDS:
   Target Response Time: ${PERFORMANCE_THRESHOLDS.apiResponseTimeout}ms
   Actual Avg Response: ${avgResponseTime.toFixed(0)}ms
   Performance Rating: ${avgResponseTime < PERFORMANCE_THRESHOLDS.apiResponseTimeout ? '✅ GOOD' : '⚠️ NEEDS IMPROVEMENT'}

🔍 DETAILED BREAKDOWN:
${testResults.map((result, index) => {
  const status = result.success ? '✅' : '❌';
  const webhook = result.webhookReceived === true ? '🔗' : result.webhookReceived === false ? '⚠️' : '⚪';
  
  return `   ${index + 1}. ${status} ${webhook} ${result.step || 'Test'} (${result.responseTime || 0}ms)`;
}).join('\n')}
`;
}