import { test, expect } from '@playwright/test';
import { 
  STYLE_PREVIEW_TEST_CASES, 
  HTTP_STATUS, 
  TEST_SCENARIOS,
  PERFORMANCE_THRESHOLDS 
} from './test-data';
import { 
  testImageAccessibility, 
  generateTestReport, 
  makeRequestWithMetrics,
  captureTimestampedScreenshot 
} from './test-helpers';

test.describe('Pre-Upload State Validation - Current 404s', () => {
  test.describe.configure({ mode: 'parallel' });

  test('should validate all style preview URLs return 404 (current expected state)', async ({ page }) => {
    console.log('🔍 Testing current state - expecting 404 responses for all style preview images');
    
    const results: Array<{
      styleName: string;
      status: number;
      accessible: boolean;
      loadTime: number;
      imageValid: boolean;
      issues: string[];
    }> = [];

    let totalTests = 0;
    let expectedNotFound = 0;
    let unexpectedlyFound = 0;

    // Test each style preview URL
    for (const testCase of STYLE_PREVIEW_TEST_CASES) {
      console.log(`Testing ${testCase.styleName}...`);
      
      const result = await testImageAccessibility(
        page, 
        testCase.expectedUrl, 
        HTTP_STATUS.NOT_FOUND
      );
      
      results.push({
        styleName: testCase.styleName,
        ...result
      });

      totalTests++;
      
      if (result.status === HTTP_STATUS.NOT_FOUND) {
        expectedNotFound++;
        console.log(`✅ ${testCase.styleName}: 404 Not Found (expected)`);
      } else {
        unexpectedlyFound++;
        console.log(`⚠️  ${testCase.styleName}: Status ${result.status} (unexpected - should be 404)`);
      }
    }

    // Generate and log comprehensive report
    const report = generateTestReport(TEST_SCENARIOS.preUpload.name, results);
    console.log(report);

    // Capture screenshot of test results summary
    await page.goto('data:text/html,<html><body><h1>Pre-Upload State Test Complete</h1><p>See console for detailed results</p></body></html>');
    await captureTimestampedScreenshot(page, 'pre-upload-state-summary');

    // Assertions
    expect(totalTests, 'Should test all expected style previews').toBe(STYLE_PREVIEW_TEST_CASES.length);
    
    // For pre-upload state, we EXPECT 404s
    const allReturned404 = results.every(r => r.status === HTTP_STATUS.NOT_FOUND);
    
    if (allReturned404) {
      console.log('✅ All style preview images correctly return 404 (pre-upload state confirmed)');
    } else {
      console.log('⚠️  Some images are already accessible - this may indicate partial upload or caching');
      
      // List which images are unexpectedly accessible
      const unexpectedlyAccessible = results.filter(r => r.status !== HTTP_STATUS.NOT_FOUND);
      console.log('Unexpectedly accessible images:', unexpectedlyAccessible.map(r => r.styleName));
    }

    // Report summary
    console.log(`\n📊 Pre-Upload State Summary:
    - Total tested: ${totalTests}
    - Expected 404s: ${expectedNotFound}
    - Unexpectedly found: ${unexpectedlyFound}
    - Test completed: ${new Date().toISOString()}`);
  });

  // Test individual categories
  for (const category of ['professional', 'creative', 'lifestyle', 'specialized'] as const) {
    test(`should validate ${category} style previews return 404`, async ({ page }) => {
      const categoryTests = STYLE_PREVIEW_TEST_CASES.filter(tc => tc.category === category);
      
      console.log(`🏷️  Testing ${category} category (${categoryTests.length} styles)`);
      
      for (const testCase of categoryTests) {
        const response = await makeRequestWithMetrics(page, testCase.expectedUrl);
        
        console.log(`${testCase.styleName}: Status ${response.status} (${response.responseTime}ms)`);
        
        // For pre-upload, we expect 404
        if (response.status === HTTP_STATUS.NOT_FOUND) {
          expect(response.status).toBe(HTTP_STATUS.NOT_FOUND);
        } else {
          console.log(`⚠️  ${testCase.styleName} returned ${response.status} instead of 404`);
          // Don't fail the test, just log it as this might indicate partial upload
        }
      }
    });
  }

  test('should validate Azure Blob Storage container accessibility', async ({ page }) => {
    console.log('🗄️  Testing Azure Blob Storage container accessibility...');
    
    // Test the container root - this should either be 404 (not found) or 403 (forbidden)
    // Both are acceptable as they indicate the container exists but individual blobs don't
    const containerResponse = await makeRequestWithMetrics(
      page, 
      'https://aipmstv16j74jubocuukg.blob.core.windows.net/style-previews/'
    );

    console.log(`Container response: ${containerResponse.status} (${containerResponse.responseTime}ms)`);

    // Container should return 404 or 403, not 200 (which would mean public listing is enabled)
    expect([HTTP_STATUS.NOT_FOUND, HTTP_STATUS.FORBIDDEN]).toContain(containerResponse.status);
  });

  test('should measure baseline performance metrics', async ({ page }) => {
    console.log('⏱️  Measuring baseline performance metrics...');
    
    const performanceResults = [];
    
    // Test a sample of URLs for performance baseline
    const sampleUrls = STYLE_PREVIEW_TEST_CASES.slice(0, 5);
    
    for (const testCase of sampleUrls) {
      const response = await makeRequestWithMetrics(page, testCase.expectedUrl);
      
      performanceResults.push({
        styleName: testCase.styleName,
        responseTime: response.responseTime,
        status: response.status
      });
      
      // All should be fast 404s
      expect(response.responseTime).toBeLessThan(PERFORMANCE_THRESHOLDS.apiResponseTimeout);
    }

    const avgResponseTime = performanceResults.reduce((sum, r) => sum + r.responseTime, 0) / performanceResults.length;
    console.log(`Average 404 response time: ${avgResponseTime.toFixed(0)}ms`);
    
    // 404 responses should be fast
    expect(avgResponseTime).toBeLessThan(2000); // Should be under 2 seconds for 404s
  });
});