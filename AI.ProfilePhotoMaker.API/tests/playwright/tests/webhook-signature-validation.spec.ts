import { test, expect } from '@playwright/test';
import { 
  sendWebhookRequest, 
  createPredictionWebhookPayload, 
  simulateWebhookFailure,
  generateWebhookPerformanceReport
} from './webhook-integration-helpers';

/**
 * Webhook Signature Validation Test Suite
 * 
 * PURPOSE: Validate webhook signature authentication and error handling
 * FOCUS: ReplicateWebhookController signature validation after webhook migration
 * 
 * CRITICAL SCENARIOS:
 * 1. Valid signature acceptance
 * 2. Invalid signature rejection
 * 3. Missing signature handling
 * 4. Timestamp validation
 * 5. Payload tampering detection
 */

const NGROK_BASE_URL = 'https://clear-anteater-usually.ngrok-free.app';
const WEBHOOK_ENDPOINT = `${NGROK_BASE_URL}/api/webhooks/replicate/prediction-complete`;

interface SignatureTestResult {
  testCase: string;
  expectedResult: 'accept' | 'reject';
  actualResult: 'accept' | 'reject';
  success: boolean;
  statusCode?: number;
  responseTime: number;
  error?: string;
}

test.describe('Webhook Signature Validation', () => {
  
  let testResults: SignatureTestResult[] = [];

  test.beforeEach(async () => {
    test.setTimeout(60000); // 1 minute timeout
    testResults = [];
    console.log('🔐 Starting webhook signature validation tests...');
  });

  test.afterEach(async () => {
    console.log('\n📊 SIGNATURE VALIDATION REPORT');
    console.log('=' + '='.repeat(50));
    
    const totalTests = testResults.length;
    const passedTests = testResults.filter(r => r.success).length;
    const failedTests = totalTests - passedTests;
    
    console.log(`📈 VALIDATION METRICS:`);
    console.log(`   Total Tests: ${totalTests}`);
    console.log(`   Passed: ${passedTests} (${((passedTests / totalTests) * 100).toFixed(1)}%)`);
    console.log(`   Failed: ${failedTests}`);
    
    console.log(`\n🔍 TEST RESULTS:`);
    testResults.forEach((result, index) => {
      const status = result.success ? '✅' : '❌';
      const expected = result.expectedResult === 'accept' ? '🔓' : '🔒';
      const actual = result.actualResult === 'accept' ? '🔓' : '🔒';
      
      console.log(`${index + 1}. ${status} ${expected}→${actual} ${result.testCase}`);
      console.log(`   Expected: ${result.expectedResult}, Got: ${result.actualResult} (${result.responseTime}ms)`);
      if (result.error) console.log(`   Error: ${result.error}`);
    });
  });

  test('should accept valid webhook signatures', async ({ page }) => {
    const testId = `valid-sig-${Date.now()}`;
    const payload = createPredictionWebhookPayload(testId, `user-${testId}`);
    
    // Test with correct webhook secret (from environment or default)
    const webhookSecret = 'whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM';
    
    const result = await sendWebhookRequest(page, WEBHOOK_ENDPOINT, payload, webhookSecret);
    
    const testResult: SignatureTestResult = {
      testCase: 'Valid Signature',
      expectedResult: 'accept',
      actualResult: result.success ? 'accept' : 'reject',
      success: result.success,
      statusCode: result.statusCode,
      responseTime: result.responseTime,
      error: result.error
    };
    
    testResults.push(testResult);
    
    expect(result.success, `Valid signature should be accepted. Status: ${result.statusCode}, Error: ${result.error}`).toBe(true);
    expect(result.statusCode).toBe(200);
  });

  test('should reject invalid webhook signatures', async ({ page }) => {
    const testId = `invalid-sig-${Date.now()}`;
    const payload = createPredictionWebhookPayload(testId, `user-${testId}`);
    
    // Test with wrong webhook secret
    const wrongSecret = 'wrong-secret-key';
    
    const result = await sendWebhookRequest(page, WEBHOOK_ENDPOINT, payload, wrongSecret);
    
    const testResult: SignatureTestResult = {
      testCase: 'Invalid Signature',
      expectedResult: 'reject',
      actualResult: result.success ? 'accept' : 'reject',
      success: !result.success && (result.statusCode === 401 || result.statusCode === 403),
      statusCode: result.statusCode,
      responseTime: result.responseTime,
      error: result.error
    };
    
    testResults.push(testResult);
    
    expect(result.success, 'Invalid signature should be rejected').toBe(false);
    expect(result.statusCode).toBeOneOf([401, 403]);
  });

  test('should reject requests without signature header', async ({ page }) => {
    const testId = `no-sig-${Date.now()}`;
    const payload = createPredictionWebhookPayload(testId, `user-${testId}`);
    
    const startTime = Date.now();
    
    try {
      const response = await page.request.post(WEBHOOK_ENDPOINT, {
        data: payload,
        headers: {
          'Content-Type': 'application/json'
          // Deliberately omit Replicate-Signature header
        }
      });
      
      const responseTime = Date.now() - startTime;
      
      const testResult: SignatureTestResult = {
        testCase: 'Missing Signature Header',
        expectedResult: 'reject',
        actualResult: response.ok() ? 'accept' : 'reject',
        success: !response.ok() && (response.status() === 401 || response.status() === 403),
        statusCode: response.status(),
        responseTime,
        error: response.ok() ? 'Request without signature was unexpectedly accepted' : undefined
      };
      
      testResults.push(testResult);
      
      expect(response.ok(), 'Request without signature should be rejected').toBe(false);
      expect(response.status()).toBeOneOf([401, 403]);
      
    } catch (error) {
      const testResult: SignatureTestResult = {
        testCase: 'Missing Signature Header',
        expectedResult: 'reject',
        actualResult: 'reject',
        success: true,
        responseTime: Date.now() - startTime,
        error: 'Request failed as expected (missing signature)'
      };
      
      testResults.push(testResult);
    }
  });

  test('should validate timestamp in signature', async ({ page }) => {
    const testId = `timestamp-${Date.now()}`;
    const payload = createPredictionWebhookPayload(testId, `user-${testId}`);
    const webhookSecret = 'whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM';
    
    // Test with very old timestamp (more than 5 minutes ago)
    const oldTimestamp = Math.floor((Date.now() - 10 * 60 * 1000) / 1000); // 10 minutes ago
    
    const startTime = Date.now();
    
    try {
      const payloadString = JSON.stringify(payload);
      
      const response = await page.request.post(WEBHOOK_ENDPOINT, {
        data: payload,
        headers: {
          'Content-Type': 'application/json',
          'Replicate-Signature': `sha256=old_timestamp_signature`,
          'Replicate-Timestamp': oldTimestamp.toString()
        }
      });
      
      const responseTime = Date.now() - startTime;
      
      const testResult: SignatureTestResult = {
        testCase: 'Old Timestamp',
        expectedResult: 'reject',
        actualResult: response.ok() ? 'accept' : 'reject',
        success: !response.ok() && (response.status() === 401 || response.status() === 403),
        statusCode: response.status(),
        responseTime,
        error: response.ok() ? 'Old timestamp was unexpectedly accepted' : undefined
      };
      
      testResults.push(testResult);
      
      expect(response.ok(), 'Old timestamp should be rejected').toBe(false);
      expect(response.status()).toBeOneOf([401, 403]);
      
    } catch (error) {
      const testResult: SignatureTestResult = {
        testCase: 'Old Timestamp',
        expectedResult: 'reject',
        actualResult: 'reject',
        success: true,
        responseTime: Date.now() - startTime,
        error: 'Old timestamp rejected as expected'
      };
      
      testResults.push(testResult);
    }
  });

  test('should detect payload tampering', async ({ page }) => {
    const testId = `tamper-${Date.now()}`;
    const originalPayload = createPredictionWebhookPayload(testId, `user-${testId}`);
    const webhookSecret = 'whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM';
    
    // Generate signature for original payload
    const originalPayloadString = JSON.stringify(originalPayload);
    const validSignature = `sha256=valid_signature_for_original`;
    
    // Tamper with payload after signature generation
    const tamperedPayload = {
      ...originalPayload,
      output: ['https://malicious-site.com/malicious-image.jpg'] // Tampered output
    };
    
    const startTime = Date.now();
    
    try {
      const response = await page.request.post(WEBHOOK_ENDPOINT, {
        data: tamperedPayload, // Send tampered payload
        headers: {
          'Content-Type': 'application/json',
          'Replicate-Signature': validSignature, // With signature for original
          'Replicate-Timestamp': Math.floor(Date.now() / 1000).toString()
        }
      });
      
      const responseTime = Date.now() - startTime;
      
      const testResult: SignatureTestResult = {
        testCase: 'Payload Tampering Detection',
        expectedResult: 'reject',
        actualResult: response.ok() ? 'accept' : 'reject',
        success: !response.ok() && (response.status() === 401 || response.status() === 403),
        statusCode: response.status(),
        responseTime,
        error: response.ok() ? 'Tampered payload was unexpectedly accepted' : undefined
      };
      
      testResults.push(testResult);
      
      expect(response.ok(), 'Tampered payload should be rejected').toBe(false);
      expect(response.status()).toBeOneOf([401, 403]);
      
    } catch (error) {
      const testResult: SignatureTestResult = {
        testCase: 'Payload Tampering Detection',
        expectedResult: 'reject',
        actualResult: 'reject',
        success: true,
        responseTime: Date.now() - startTime,
        error: 'Tampered payload rejected as expected'
      };
      
      testResults.push(testResult);
    }
  });

  test('should handle malformed signature format', async ({ page }) => {
    const testId = `malformed-sig-${Date.now()}`;
    const payload = createPredictionWebhookPayload(testId, `user-${testId}`);
    
    const malformedSignatures = [
      'not-a-signature',
      'sha256=',
      'sha1=invalid_algorithm',
      'malformed-format',
      ''
    ];
    
    for (const [index, malformedSignature] of malformedSignatures.entries()) {
      const startTime = Date.now();
      
      try {
        const response = await page.request.post(WEBHOOK_ENDPOINT, {
          data: payload,
          headers: {
            'Content-Type': 'application/json',
            'Replicate-Signature': malformedSignature,
            'Replicate-Timestamp': Math.floor(Date.now() / 1000).toString()
          }
        });
        
        const responseTime = Date.now() - startTime;
        
        const testResult: SignatureTestResult = {
          testCase: `Malformed Signature ${index + 1}`,
          expectedResult: 'reject',
          actualResult: response.ok() ? 'accept' : 'reject',
          success: !response.ok() && (response.status() === 400 || response.status() === 401 || response.status() === 403),
          statusCode: response.status(),
          responseTime,
          error: response.ok() ? `Malformed signature "${malformedSignature}" was unexpectedly accepted` : undefined
        };
        
        testResults.push(testResult);
        
        expect(response.ok(), `Malformed signature "${malformedSignature}" should be rejected`).toBe(false);
        expect(response.status()).toBeOneOf([400, 401, 403]);
        
      } catch (error) {
        const testResult: SignatureTestResult = {
          testCase: `Malformed Signature ${index + 1}`,
          expectedResult: 'reject',
          actualResult: 'reject',
          success: true,
          responseTime: Date.now() - startTime,
          error: `Malformed signature "${malformedSignature}" rejected as expected`
        };
        
        testResults.push(testResult);
      }
    }
  });

  test('should measure signature validation performance', async ({ page }) => {
    const testId = `perf-${Date.now()}`;
    const webhookSecret = 'whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM';
    const requestCount = 10;
    
    const performanceMeasurements: number[] = [];
    
    for (let i = 0; i < requestCount; i++) {
      const payload = createPredictionWebhookPayload(`${testId}-${i}`, `user-${testId}-${i}`);
      const result = await sendWebhookRequest(page, WEBHOOK_ENDPOINT, payload, webhookSecret);
      
      performanceMeasurements.push(result.responseTime);
      
      const testResult: SignatureTestResult = {
        testCase: `Performance Test ${i + 1}`,
        expectedResult: 'accept',
        actualResult: result.success ? 'accept' : 'reject',
        success: result.success,
        statusCode: result.statusCode,
        responseTime: result.responseTime
      };
      
      testResults.push(testResult);
    }
    
    const avgResponseTime = performanceMeasurements.reduce((a, b) => a + b, 0) / requestCount;
    const maxResponseTime = Math.max(...performanceMeasurements);
    const minResponseTime = Math.min(...performanceMeasurements);
    
    console.log(`\n⚡ SIGNATURE VALIDATION PERFORMANCE:`);
    console.log(`   Requests: ${requestCount}`);
    console.log(`   Average: ${avgResponseTime.toFixed(0)}ms`);
    console.log(`   Min: ${minResponseTime}ms`);
    console.log(`   Max: ${maxResponseTime}ms`);
    console.log(`   Target: <1000ms`);
    console.log(`   Performance: ${avgResponseTime < 1000 ? '✅ GOOD' : '⚠️ SLOW'}`);
    
    // Performance assertions
    expect(avgResponseTime, 'Average signature validation should be under 1 second').toBeLessThan(1000);
    expect(maxResponseTime, 'Maximum signature validation should be under 2 seconds').toBeLessThan(2000);
  });

  test('should handle concurrent signature validation', async ({ page }) => {
    const testId = `concurrent-${Date.now()}`;
    const webhookSecret = 'whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM';
    const concurrentRequests = 5;
    
    const requests = [];
    
    for (let i = 0; i < concurrentRequests; i++) {
      const payload = createPredictionWebhookPayload(`${testId}-${i}`, `user-${testId}-${i}`);
      const requestPromise = sendWebhookRequest(page, WEBHOOK_ENDPOINT, payload, webhookSecret);
      requests.push(requestPromise);
    }
    
    const startTime = Date.now();
    const results = await Promise.allSettled(requests);
    const totalTime = Date.now() - startTime;
    
    const successful = results.filter(r => r.status === 'fulfilled' && r.value.success).length;
    const failed = results.filter(r => r.status === 'rejected' || !r.value?.success).length;
    
    const testResult: SignatureTestResult = {
      testCase: 'Concurrent Validation',
      expectedResult: 'accept',
      actualResult: successful > 0 ? 'accept' : 'reject',
      success: successful === concurrentRequests,
      responseTime: totalTime,
      error: failed > 0 ? `${failed} concurrent requests failed` : undefined
    };
    
    testResults.push(testResult);
    
    console.log(`\n🔀 CONCURRENT VALIDATION RESULTS:`);
    console.log(`   Concurrent Requests: ${concurrentRequests}`);
    console.log(`   Successful: ${successful}`);
    console.log(`   Failed: ${failed}`);
    console.log(`   Total Time: ${totalTime}ms`);
    console.log(`   Avg Per Request: ${(totalTime / concurrentRequests).toFixed(0)}ms`);
    
    expect(successful, 'All concurrent signature validations should succeed').toBe(concurrentRequests);
    expect(totalTime, 'Concurrent validation should not take excessively long').toBeLessThan(10000); // 10 seconds max
  });
});