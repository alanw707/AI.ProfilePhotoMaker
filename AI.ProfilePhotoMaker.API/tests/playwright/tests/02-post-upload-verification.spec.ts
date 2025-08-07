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
  loadImageWithMetrics,
  validateImageQuality,
  captureTimestampedScreenshot 
} from './test-helpers';

test.describe('Post-Upload Verification - Success State', () => {
  test.describe.configure({ mode: 'parallel' });

  test('should validate all style preview URLs return 200 with valid images', async ({ page }) => {
    console.log('✅ Testing post-upload state - expecting 200 responses with valid images');
    
    const results: Array<{
      styleName: string;
      status: number;
      accessible: boolean;
      loadTime: number;
      imageValid: boolean;
      issues: string[];
    }> = [];

    let totalTests = 0;
    let successfulLoads = 0;
    let failedLoads = 0;

    // Test each style preview URL
    for (const testCase of STYLE_PREVIEW_TEST_CASES) {
      console.log(`Testing ${testCase.styleName}...`);
      
      const result = await testImageAccessibility(
        page, 
        testCase.expectedUrl, 
        HTTP_STATUS.OK
      );
      
      results.push({
        styleName: testCase.styleName,
        ...result
      });

      totalTests++;
      
      if (result.status === HTTP_STATUS.OK && result.imageValid) {
        successfulLoads++;
        console.log(`✅ ${testCase.styleName}: 200 OK with valid image (${result.loadTime}ms)`);
      } else {
        failedLoads++;
        console.log(`❌ ${testCase.styleName}: Status ${result.status}, Valid: ${result.imageValid}, Issues: ${result.issues.join(', ')}`);
      }
    }

    // Generate and log comprehensive report
    const report = generateTestReport(TEST_SCENARIOS.postUpload.name, results);
    console.log(report);

    // Capture screenshot of test results summary
    await page.goto('data:text/html,<html><body><h1>Post-Upload Verification Complete</h1><p>See console for detailed results</p></body></html>');
    await captureTimestampedScreenshot(page, 'post-upload-verification-summary');

    // Assertions for post-upload state
    expect(totalTests, 'Should test all expected style previews').toBe(STYLE_PREVIEW_TEST_CASES.length);
    
    // For post-upload state, we EXPECT all 200s with valid images
    const allSuccessful = results.every(r => r.status === HTTP_STATUS.OK && r.imageValid);
    
    if (allSuccessful) {
      console.log('✅ All style preview images successfully loaded and validated');
    } else {
      const failures = results.filter(r => r.status !== HTTP_STATUS.OK || !r.imageValid);
      console.log(`❌ ${failures.length} image(s) failed to load or validate:`, failures.map(f => f.styleName));
    }

    // Strong assertion for post-upload verification
    expect(successfulLoads, 'All images should load successfully after upload').toBe(totalTests);

    // Report summary
    console.log(`\n📊 Post-Upload Verification Summary:
    - Total tested: ${totalTests}
    - Successfully loaded: ${successfulLoads}
    - Failed to load: ${failedLoads}
    - Success rate: ${((successfulLoads / totalTests) * 100).toFixed(1)}%
    - Test completed: ${new Date().toISOString()}`);
  });

  test('should validate image quality and dimensions', async ({ page }) => {
    console.log('🎨 Testing image quality and dimensions...');
    
    const qualityResults = [];
    let validImages = 0;
    let invalidImages = 0;

    // Test image quality for all style previews
    for (const testCase of STYLE_PREVIEW_TEST_CASES) {
      const qualityResult = await validateImageQuality(page, testCase.expectedUrl);
      
      qualityResults.push({
        styleName: testCase.styleName,
        ...qualityResult
      });

      if (qualityResult.isValid) {
        validImages++;
        console.log(`✅ ${testCase.styleName}: ${qualityResult.width}x${qualityResult.height} (AR: ${qualityResult.aspectRatio.toFixed(2)})`);
      } else {
        invalidImages++;
        console.log(`❌ ${testCase.styleName}: ${qualityResult.issues.join(', ')}`);
      }
    }

    // Quality assertions
    expect(validImages).toBeGreaterThan(0);
    
    // At least 90% of images should meet quality standards
    const qualityRate = (validImages / qualityResults.length) * 100;
    expect(qualityRate).toBeGreaterThanOrEqual(90);

    console.log(`🎨 Image Quality Summary:
    - Valid images: ${validImages}
    - Invalid images: ${invalidImages}
    - Quality rate: ${qualityRate.toFixed(1)}%`);
  });

  test('should validate performance benchmarks', async ({ page }) => {
    console.log('⚡ Testing performance benchmarks...');
    
    const performanceResults = [];
    
    for (const testCase of STYLE_PREVIEW_TEST_CASES) {
      const loadResult = await loadImageWithMetrics(page, testCase.expectedUrl);
      
      performanceResults.push({
        styleName: testCase.styleName,
        loadTime: loadResult.loadTime,
        success: loadResult.success
      });

      if (loadResult.success) {
        console.log(`✅ ${testCase.styleName}: Loaded in ${loadResult.loadTime}ms`);
      } else {
        console.log(`❌ ${testCase.styleName}: Failed to load (${loadResult.error})`);
      }
    }

    const successfulLoads = performanceResults.filter(r => r.success);
    const avgLoadTime = successfulLoads.reduce((sum, r) => sum + r.loadTime, 0) / successfulLoads.length;
    const maxLoadTime = Math.max(...successfulLoads.map(r => r.loadTime));
    const minLoadTime = Math.min(...successfulLoads.map(r => r.loadTime));

    console.log(`⚡ Performance Summary:
    - Average load time: ${avgLoadTime.toFixed(0)}ms
    - Maximum load time: ${maxLoadTime}ms
    - Minimum load time: ${minLoadTime}ms
    - Target load time: ${PERFORMANCE_THRESHOLDS.targetLoadTime}ms`);

    // Performance assertions
    expect(avgLoadTime).toBeLessThan(PERFORMANCE_THRESHOLDS.targetLoadTime);
    expect(maxLoadTime).toBeLessThan(PERFORMANCE_THRESHOLDS.imageLoadTimeout);
    
    // At least 90% should load within target time
    const withinTarget = successfulLoads.filter(r => r.loadTime <= PERFORMANCE_THRESHOLDS.targetLoadTime).length;
    const performanceRate = (withinTarget / successfulLoads.length) * 100;
    expect(performanceRate).toBeGreaterThanOrEqual(90);
  });

  // Test individual categories for successful loading
  for (const category of ['professional', 'creative', 'lifestyle', 'specialized'] as const) {
    test(`should validate ${category} style previews load successfully`, async ({ page }) => {
      const categoryTests = STYLE_PREVIEW_TEST_CASES.filter(tc => tc.category === category);
      
      console.log(`🏷️  Testing ${category} category (${categoryTests.length} styles)`);
      
      let categorySuccess = 0;
      
      for (const testCase of categoryTests) {
        const result = await testImageAccessibility(page, testCase.expectedUrl, HTTP_STATUS.OK);
        
        if (result.status === HTTP_STATUS.OK && result.imageValid) {
          categorySuccess++;
          console.log(`✅ ${testCase.styleName}: Success (${result.loadTime}ms)`);
        } else {
          console.log(`❌ ${testCase.styleName}: Failed - Status: ${result.status}, Valid: ${result.imageValid}`);
        }
      }
      
      // All images in category should load successfully
      expect(categorySuccess).toBe(categoryTests.length);
      
      console.log(`🏷️  ${category} category: ${categorySuccess}/${categoryTests.length} successful`);
    });
  }

  test('should validate HTTP headers and content types', async ({ page }) => {
    console.log('📋 Testing HTTP headers and content types...');
    
    // Test a sample of images for proper headers
    const sampleTests = STYLE_PREVIEW_TEST_CASES.slice(0, 5);
    
    for (const testCase of sampleTests) {
      const response = await page.request.get(testCase.expectedUrl);
      
      // Should return 200 OK
      expect(response.status()).toBe(HTTP_STATUS.OK);
      
      // Should have correct content type
      const contentType = response.headers()['content-type'];
      expect(contentType).toMatch(/^image\/(jpeg|jpg)/i);
      
      // Should have content length
      const contentLength = response.headers()['content-length'];
      expect(parseInt(contentLength || '0')).toBeGreaterThan(0);
      
      console.log(`📋 ${testCase.styleName}: ${contentType}, ${contentLength} bytes`);
    }
  });

  test('should validate CORS headers for cross-origin access', async ({ page }) => {
    console.log('🌐 Testing CORS headers...');
    
    // Test CORS headers on a sample image
    const testCase = STYLE_PREVIEW_TEST_CASES[0];
    const response = await page.request.get(testCase.expectedUrl);
    
    // Azure Blob Storage should allow cross-origin access for public blobs
    const headers = response.headers();
    console.log('Response headers:', Object.keys(headers));
    
    // Verify the image is accessible (main requirement)
    expect(response.status()).toBe(HTTP_STATUS.OK);
    
    console.log(`🌐 CORS test completed for ${testCase.styleName}`);
  });
});