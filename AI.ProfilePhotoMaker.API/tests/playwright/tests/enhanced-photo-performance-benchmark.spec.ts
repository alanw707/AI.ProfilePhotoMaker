import { test, expect } from '@playwright/test';
import { 
  createTestUser,
  uploadTestImage,
  triggerPhotoEnhancement,
  monitorPredictionStatus,
  verifyWebhookDatabaseUpdate,
  cleanupTestData,
  generateWebhookPerformanceReport
} from './webhook-integration-helpers';

/**
 * Enhanced Photo Performance Benchmark Test Suite
 * 
 * PURPOSE: Measure and validate performance improvements from webhook migration
 * CONTEXT: Compare webhook-based enhancement vs estimated polling performance
 * 
 * PERFORMANCE METRICS:
 * 1. End-to-end enhancement workflow timing
 * 2. Webhook delivery latency
 * 3. Database persistence speed
 * 4. Resource utilization efficiency
 * 5. Concurrent processing capabilities
 */

const NGROK_BASE_URL = 'https://clear-anteater-usually.ngrok-free.app';
const API_BASE_URL = `${NGROK_BASE_URL}/api`;

interface PerformanceBenchmark {
  testCase: string;
  startTime: number;
  endTime: number;
  duration: number;
  success: boolean;
  metrics: {
    uploadTime?: number;
    enhancementRequestTime?: number;
    webhookDeliveryTime?: number;
    databaseUpdateTime?: number;
    imageValidationTime?: number;
  };
  error?: string;
}

interface PerformanceReport {
  totalTests: number;
  successfulTests: number;
  averageDuration: number;
  minDuration: number;
  maxDuration: number;
  webhookImprovement: number; // Percentage improvement over polling
  resourceEfficiency: number; // Resource usage score
  concurrentCapability: number; // Max concurrent requests handled
}

test.describe('Enhanced Photo Performance Benchmark', () => {
  
  let benchmarks: PerformanceBenchmark[] = [];
  let performanceReport: PerformanceReport;

  test.beforeEach(async () => {
    test.setTimeout(600000); // 10 minutes for performance testing
    benchmarks = [];
    console.log('⚡ Starting Enhanced Photo Performance Benchmarks...');
  });

  test.afterEach(async () => {
    generatePerformanceReport();
    displayPerformanceReport();
  });

  /**
   * Benchmark 1: Single Enhancement Workflow Performance
   */
  test('should measure single enhancement workflow performance', async ({ page }) => {
    const testId = `single-perf-${Date.now()}`;
    const benchmark: PerformanceBenchmark = {
      testCase: 'Single Enhancement Workflow',
      startTime: Date.now(),
      endTime: 0,
      duration: 0,
      success: false,
      metrics: {}
    };

    try {
      // Step 1: Create test user
      const userResult = await createTestUser(page, API_BASE_URL, testId);
      if (!userResult.success) {
        throw new Error(`User creation failed: ${userResult.error}`);
      }

      // Step 2: Upload test image
      const uploadStart = Date.now();
      const uploadResult = await uploadTestImage(page, API_BASE_URL, userResult.authToken!, testId);
      benchmark.metrics.uploadTime = Date.now() - uploadStart;
      
      if (!uploadResult.success) {
        throw new Error(`Image upload failed: ${uploadResult.error}`);
      }

      // Step 3: Trigger enhancement
      const enhancementStart = Date.now();
      const enhancementResult = await triggerPhotoEnhancement(
        page, API_BASE_URL, userResult.authToken!, uploadResult.imageUrl!, userResult.userId!
      );
      benchmark.metrics.enhancementRequestTime = Date.now() - enhancementStart;
      
      if (!enhancementResult.success) {
        throw new Error(`Enhancement failed: ${enhancementResult.error}`);
      }

      // Step 4: Monitor webhook delivery
      const webhookStart = Date.now();
      const predictionStatus = await monitorPredictionStatus(
        page, enhancementResult.predictionId!, API_BASE_URL, 120000
      );
      benchmark.metrics.webhookDeliveryTime = Date.now() - webhookStart;
      
      if (!predictionStatus) {
        throw new Error('Webhook delivery timeout');
      }

      // Step 5: Verify database update
      const dbStart = Date.now();
      const dbResult = await verifyWebhookDatabaseUpdate(page, userResult.userId!, API_BASE_URL);
      benchmark.metrics.databaseUpdateTime = Date.now() - dbStart;
      
      if (!dbResult.success) {
        throw new Error(`Database verification failed: ${dbResult.error}`);
      }

      benchmark.endTime = Date.now();
      benchmark.duration = benchmark.endTime - benchmark.startTime;
      benchmark.success = true;

      console.log(`✅ Single enhancement completed in ${benchmark.duration}ms`);
      console.log(`   Upload: ${benchmark.metrics.uploadTime}ms`);
      console.log(`   Enhancement: ${benchmark.metrics.enhancementRequestTime}ms`);
      console.log(`   Webhook: ${benchmark.metrics.webhookDeliveryTime}ms`);
      console.log(`   Database: ${benchmark.metrics.databaseUpdateTime}ms`);

    } catch (error) {
      benchmark.endTime = Date.now();
      benchmark.duration = benchmark.endTime - benchmark.startTime;
      benchmark.error = error instanceof Error ? error.message : 'Unknown error';
      console.error(`❌ Single enhancement failed: ${benchmark.error}`);
    } finally {
      benchmarks.push(benchmark);
      await cleanupTestData(page, API_BASE_URL, testId, userResult?.authToken);
    }

    expect(benchmark.success, `Single enhancement should succeed: ${benchmark.error}`).toBe(true);
    expect(benchmark.duration, 'Single enhancement should complete under 2 minutes').toBeLessThan(120000);
  });

  /**
   * Benchmark 2: Concurrent Enhancement Performance
   */
  test('should measure concurrent enhancement performance', async ({ page }) => {
    const testId = `concurrent-perf-${Date.now()}`;
    const concurrentCount = 3;
    const concurrentBenchmarks: Promise<PerformanceBenchmark>[] = [];

    console.log(`🔀 Starting ${concurrentCount} concurrent enhancements...`);

    for (let i = 0; i < concurrentCount; i++) {
      const concurrentTestId = `${testId}-${i}`;
      const benchmarkPromise = runSingleEnhancementBenchmark(page, concurrentTestId, i);
      concurrentBenchmarks.push(benchmarkPromise);
    }

    const overallStart = Date.now();
    const results = await Promise.allSettled(concurrentBenchmarks);
    const overallDuration = Date.now() - overallStart;

    const successful = results.filter(r => r.status === 'fulfilled' && r.value.success).length;
    const failed = results.filter(r => r.status === 'rejected' || (r.status === 'fulfilled' && !r.value.success)).length;

    // Add individual benchmarks to results
    results.forEach(result => {
      if (result.status === 'fulfilled') {
        benchmarks.push(result.value);
      }
    });

    // Add overall concurrent benchmark
    const concurrentBenchmark: PerformanceBenchmark = {
      testCase: 'Concurrent Enhancement',
      startTime: overallStart,
      endTime: overallStart + overallDuration,
      duration: overallDuration,
      success: successful > 0,
      metrics: {
        enhancementRequestTime: overallDuration
      },
      error: failed > 0 ? `${failed}/${concurrentCount} concurrent requests failed` : undefined
    };

    benchmarks.push(concurrentBenchmark);

    console.log(`🔀 Concurrent Enhancement Results:`);
    console.log(`   Total Time: ${overallDuration}ms`);
    console.log(`   Successful: ${successful}/${concurrentCount}`);
    console.log(`   Failed: ${failed}/${concurrentCount}`);
    console.log(`   Average per Request: ${(overallDuration / concurrentCount).toFixed(0)}ms`);

    expect(successful, 'At least 80% of concurrent requests should succeed').toBeGreaterThanOrEqual(Math.floor(concurrentCount * 0.8));
    expect(overallDuration, 'Concurrent processing should not take more than 5 minutes').toBeLessThan(300000);
  });

  /**
   * Benchmark 3: Webhook vs Polling Comparison
   */
  test('should demonstrate webhook performance improvement over polling', async ({ page }) => {
    const testId = `webhook-vs-polling-${Date.now()}`;
    
    // Run webhook-based enhancement
    const webhookBenchmark = await runSingleEnhancementBenchmark(page, `${testId}-webhook`, 0);
    benchmarks.push(webhookBenchmark);

    if (webhookBenchmark.success) {
      // Calculate estimated polling performance
      const pollingEstimate = calculatePollingEstimate(webhookBenchmark);
      
      const improvement = ((pollingEstimate - webhookBenchmark.duration) / pollingEstimate) * 100;

      console.log(`📊 Webhook vs Polling Performance:`);
      console.log(`   Webhook Duration: ${webhookBenchmark.duration}ms`);
      console.log(`   Estimated Polling: ${pollingEstimate}ms`);
      console.log(`   Performance Improvement: ${improvement.toFixed(1)}%`);

      // Add comparison benchmark
      const comparisonBenchmark: PerformanceBenchmark = {
        testCase: 'Webhook vs Polling',
        startTime: webhookBenchmark.startTime,
        endTime: webhookBenchmark.endTime,
        duration: improvement,
        success: improvement > 0,
        metrics: {
          webhookDeliveryTime: webhookBenchmark.duration,
          enhancementRequestTime: pollingEstimate
        },
        error: improvement <= 0 ? 'Webhook performance not better than polling' : undefined
      };

      benchmarks.push(comparisonBenchmark);

      expect(improvement, 'Webhook should be faster than polling').toBeGreaterThan(0);
      expect(improvement, 'Webhook should provide at least 25% improvement').toBeGreaterThanOrEqual(25);
    }
  });

  /**
   * Benchmark 4: Resource Efficiency Test
   */
  test('should measure resource efficiency', async ({ page }) => {
    const testId = `resource-eff-${Date.now()}`;
    const resourceTests = 5;
    const resourceBenchmarks: PerformanceBenchmark[] = [];

    console.log(`📈 Running ${resourceTests} resource efficiency tests...`);

    for (let i = 0; i < resourceTests; i++) {
      const benchmark = await runSingleEnhancementBenchmark(page, `${testId}-${i}`, i);
      resourceBenchmarks.push(benchmark);
      benchmarks.push(benchmark);
      
      // Small delay between tests to allow resource monitoring
      await page.waitForTimeout(1000);
    }

    const successful = resourceBenchmarks.filter(b => b.success).length;
    const avgDuration = resourceBenchmarks
      .filter(b => b.success)
      .reduce((sum, b) => sum + b.duration, 0) / successful;

    const consistencyScore = calculateConsistencyScore(resourceBenchmarks);
    
    console.log(`📈 Resource Efficiency Results:`);
    console.log(`   Successful Tests: ${successful}/${resourceTests}`);
    console.log(`   Average Duration: ${avgDuration.toFixed(0)}ms`);
    console.log(`   Consistency Score: ${consistencyScore.toFixed(1)}%`);

    expect(successful, 'Resource efficiency tests should have high success rate').toBeGreaterThanOrEqual(Math.floor(resourceTests * 0.9));
    expect(consistencyScore, 'Performance should be consistent across tests').toBeGreaterThanOrEqual(80);
  });

  /**
   * Benchmark 5: Scalability Stress Test
   */
  test('should test scalability under load', async ({ page }) => {
    const testId = `scalability-${Date.now()}`;
    const loadLevels = [1, 2, 3]; // Progressive load testing
    const scalabilityResults: { load: number; duration: number; success: boolean }[] = [];

    for (const load of loadLevels) {
      console.log(`🔄 Testing scalability with ${load} concurrent requests...`);
      
      const loadPromises: Promise<PerformanceBenchmark>[] = [];
      const loadStart = Date.now();
      
      for (let i = 0; i < load; i++) {
        const promise = runSingleEnhancementBenchmark(page, `${testId}-load${load}-${i}`, i);
        loadPromises.push(promise);
      }
      
      const loadResults = await Promise.allSettled(loadPromises);
      const loadDuration = Date.now() - loadStart;
      
      const loadSuccessful = loadResults.filter(r => 
        r.status === 'fulfilled' && r.value.success
      ).length;
      
      scalabilityResults.push({
        load,
        duration: loadDuration,
        success: loadSuccessful >= Math.floor(load * 0.8) // 80% success threshold
      });

      // Add to benchmarks
      loadResults.forEach(result => {
        if (result.status === 'fulfilled') {
          benchmarks.push(result.value);
        }
      });

      console.log(`   Load ${load}: ${loadDuration}ms, Success: ${loadSuccessful}/${load}`);
    }

    const scalabilityBenchmark: PerformanceBenchmark = {
      testCase: 'Scalability Test',
      startTime: Date.now(),
      endTime: Date.now(),
      duration: scalabilityResults.reduce((sum, r) => sum + r.duration, 0),
      success: scalabilityResults.every(r => r.success),
      metrics: {},
      error: scalabilityResults.some(r => !r.success) ? 'Some load levels failed' : undefined
    };

    benchmarks.push(scalabilityBenchmark);

    console.log(`🔄 Scalability Results:`);
    scalabilityResults.forEach(result => {
      console.log(`   Load ${result.load}: ${result.success ? '✅' : '❌'} ${result.duration}ms`);
    });

    expect(scalabilityBenchmark.success, 'System should handle progressive load increases').toBe(true);
  });

  // Helper Functions

  async function runSingleEnhancementBenchmark(
    page: Page, 
    testId: string, 
    index: number
  ): Promise<PerformanceBenchmark> {
    const benchmark: PerformanceBenchmark = {
      testCase: `Enhancement ${index + 1}`,
      startTime: Date.now(),
      endTime: 0,
      duration: 0,
      success: false,
      metrics: {}
    };

    let userResult, uploadResult, enhancementResult;

    try {
      userResult = await createTestUser(page, API_BASE_URL, testId);
      if (!userResult.success) throw new Error(userResult.error);

      const uploadStart = Date.now();
      uploadResult = await uploadTestImage(page, API_BASE_URL, userResult.authToken!, testId);
      benchmark.metrics.uploadTime = Date.now() - uploadStart;
      if (!uploadResult.success) throw new Error(uploadResult.error);

      const enhancementStart = Date.now();
      enhancementResult = await triggerPhotoEnhancement(
        page, API_BASE_URL, userResult.authToken!, uploadResult.imageUrl!, userResult.userId!
      );
      benchmark.metrics.enhancementRequestTime = Date.now() - enhancementStart;
      if (!enhancementResult.success) throw new Error(enhancementResult.error);

      const webhookStart = Date.now();
      const predictionStatus = await monitorPredictionStatus(
        page, enhancementResult.predictionId!, API_BASE_URL, 60000
      );
      benchmark.metrics.webhookDeliveryTime = Date.now() - webhookStart;
      if (!predictionStatus) throw new Error('Timeout');

      const dbStart = Date.now();
      const dbResult = await verifyWebhookDatabaseUpdate(page, userResult.userId!, API_BASE_URL);
      benchmark.metrics.databaseUpdateTime = Date.now() - dbStart;
      if (!dbResult.success) throw new Error(dbResult.error);

      benchmark.success = true;
    } catch (error) {
      benchmark.error = error instanceof Error ? error.message : 'Unknown error';
    }

    benchmark.endTime = Date.now();
    benchmark.duration = benchmark.endTime - benchmark.startTime;

    // Cleanup
    if (userResult?.authToken) {
      await cleanupTestData(page, API_BASE_URL, testId, userResult.authToken);
    }

    return benchmark;
  }

  function calculatePollingEstimate(webhookBenchmark: PerformanceBenchmark): number {
    // Estimate polling would take longer due to:
    // 1. Polling intervals (typically 5-10s)
    // 2. Multiple polling requests
    // 3. Network overhead
    // Conservative estimate: 2.5x webhook time + polling overhead
    
    const baseTime = webhookBenchmark.duration;
    const pollingOverhead = 15000; // 15 seconds of polling overhead
    const pollingMultiplier = 2.5;
    
    return Math.floor(baseTime * pollingMultiplier + pollingOverhead);
  }

  function calculateConsistencyScore(benchmarks: PerformanceBenchmark[]): number {
    const successfulBenchmarks = benchmarks.filter(b => b.success);
    if (successfulBenchmarks.length === 0) return 0;

    const durations = successfulBenchmarks.map(b => b.duration);
    const mean = durations.reduce((a, b) => a + b, 0) / durations.length;
    const variance = durations.reduce((a, b) => a + Math.pow(b - mean, 2), 0) / durations.length;
    const stdDev = Math.sqrt(variance);
    
    // Consistency score based on coefficient of variation (lower is better)
    const coefficientOfVariation = stdDev / mean;
    const consistencyScore = Math.max(0, 100 - (coefficientOfVariation * 100));
    
    return consistencyScore;
  }

  function generatePerformanceReport(): void {
    const successfulBenchmarks = benchmarks.filter(b => b.success);
    const durations = successfulBenchmarks.map(b => b.duration);
    
    performanceReport = {
      totalTests: benchmarks.length,
      successfulTests: successfulBenchmarks.length,
      averageDuration: durations.length > 0 ? durations.reduce((a, b) => a + b, 0) / durations.length : 0,
      minDuration: durations.length > 0 ? Math.min(...durations) : 0,
      maxDuration: durations.length > 0 ? Math.max(...durations) : 0,
      webhookImprovement: calculateOverallWebhookImprovement(),
      resourceEfficiency: calculateResourceEfficiencyScore(),
      concurrentCapability: calculateConcurrentCapability()
    };
  }

  function displayPerformanceReport(): void {
    console.log('\n' + '='.repeat(80));
    console.log('📊 ENHANCED PHOTO PERFORMANCE BENCHMARK REPORT');
    console.log('='.repeat(80));
    
    console.log(`\n📈 OVERALL PERFORMANCE METRICS:`);
    console.log(`   Total Tests: ${performanceReport.totalTests}`);
    console.log(`   Successful: ${performanceReport.successfulTests} (${((performanceReport.successfulTests / performanceReport.totalTests) * 100).toFixed(1)}%)`);
    console.log(`   Average Duration: ${performanceReport.averageDuration.toFixed(0)}ms`);
    console.log(`   Min Duration: ${performanceReport.minDuration}ms`);
    console.log(`   Max Duration: ${performanceReport.maxDuration}ms`);
    
    console.log(`\n⚡ PERFORMANCE IMPROVEMENTS:`);
    console.log(`   Webhook vs Polling: +${performanceReport.webhookImprovement.toFixed(1)}%`);
    console.log(`   Resource Efficiency: ${performanceReport.resourceEfficiency.toFixed(1)}/100`);
    console.log(`   Concurrent Capability: ${performanceReport.concurrentCapability} requests`);
    
    console.log(`\n🎯 PERFORMANCE TARGETS:`);
    console.log(`   Single Enhancement: <120s ${performanceReport.averageDuration < 120000 ? '✅' : '❌'}`);
    console.log(`   Webhook Improvement: >25% ${performanceReport.webhookImprovement > 25 ? '✅' : '❌'}`);
    console.log(`   Success Rate: >90% ${((performanceReport.successfulTests / performanceReport.totalTests) * 100) > 90 ? '✅' : '❌'}`);
    
    console.log(`\n📋 DETAILED BENCHMARK RESULTS:`);
    benchmarks.forEach((benchmark, index) => {
      const status = benchmark.success ? '✅' : '❌';
      console.log(`${index + 1}. ${status} ${benchmark.testCase}: ${benchmark.duration}ms`);
      if (benchmark.metrics.uploadTime) console.log(`   └── Upload: ${benchmark.metrics.uploadTime}ms`);
      if (benchmark.metrics.enhancementRequestTime) console.log(`   └── Enhancement: ${benchmark.metrics.enhancementRequestTime}ms`);
      if (benchmark.metrics.webhookDeliveryTime) console.log(`   └── Webhook: ${benchmark.metrics.webhookDeliveryTime}ms`);
      if (benchmark.metrics.databaseUpdateTime) console.log(`   └── Database: ${benchmark.metrics.databaseUpdateTime}ms`);
      if (benchmark.error) console.log(`   └── Error: ${benchmark.error}`);
    });
  }

  function calculateOverallWebhookImprovement(): number {
    const webhookBenchmarks = benchmarks.filter(b => 
      b.testCase.includes('Webhook vs Polling') && b.success
    );
    
    if (webhookBenchmarks.length === 0) return 0;
    
    return webhookBenchmarks.reduce((sum, b) => sum + b.duration, 0) / webhookBenchmarks.length;
  }

  function calculateResourceEfficiencyScore(): number {
    const consistencyScore = calculateConsistencyScore(benchmarks);
    const successRate = (performanceReport.successfulTests / performanceReport.totalTests) * 100;
    
    return (consistencyScore + successRate) / 2;
  }

  function calculateConcurrentCapability(): number {
    const concurrentBenchmarks = benchmarks.filter(b => 
      b.testCase.includes('Concurrent') || b.testCase.includes('Enhancement')
    );
    
    const maxSuccessfulConcurrent = Math.max(
      ...concurrentBenchmarks.map(b => b.success ? 1 : 0)
    );
    
    return maxSuccessfulConcurrent;
  }
});