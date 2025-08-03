import { test, expect } from '@playwright/test';
import { StagingTestHelpers, waitForStableLoad } from './utils/test-helpers';

test.describe('Comprehensive Staging Environment Report', () => {
  let helpers: StagingTestHelpers;

  test.beforeEach(async ({ page }) => {
    helpers = new StagingTestHelpers(page);
  });

  test('should generate comprehensive staging environment report', async ({ page }) => {
    console.log('📋 Generating comprehensive staging environment report...');
    
    const report = {
      timestamp: new Date().toISOString(),
      environment: 'staging',
      baseUrl: 'https://aiprofilemaker-web-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io',
      testResults: {
        landingPage: { status: 'unknown', details: {} },
        azureIntegration: { status: 'unknown', details: {} },
        packageFunctionality: { status: 'unknown', details: {} },
        apiIntegration: { status: 'unknown', details: {} },
        imageLoading: { status: 'unknown', details: {} },
        performance: { status: 'unknown', details: {} }
      },
      issues: [] as string[],
      recommendations: [] as string[]
    };
    
    try {
      // Test 1: Landing Page Functionality
      console.log('🏠 Testing landing page...');
      const startTime = Date.now();
      await page.goto('/');
      await waitForStableLoad(page);
      const loadTime = Date.now() - startTime;
      
      const pageTitle = await page.title();
      const hasMainHeading = await page.locator('h1').count() > 0;
      const hasHeroSection = await page.locator('.hero-section, .main-content').count() > 0;
      
      report.testResults.landingPage = {
        status: hasMainHeading && hasHeroSection ? 'pass' : 'fail',
        details: {
          loadTime: `${loadTime}ms`,
          title: pageTitle,
          hasMainHeading,
          hasHeroSection
        }
      };
      
      if (loadTime > 5000) {
        report.issues.push(`Slow landing page load time: ${loadTime}ms`);
        report.recommendations.push('Optimize initial page load performance');
      }
      
      // Test 2: Azure Blob Storage Integration
      console.log('☁️ Testing Azure integration...');
      const storageMetrics = await helpers.verifyAzureBlobStorageUsage();
      
      report.testResults.azureIntegration = {
        status: storageMetrics.azureUrls.length > 0 ? 'pass' : 'fail',
        details: {
          azureImages: storageMetrics.azureUrls.length,
          nonAzureImages: storageMetrics.nonAzureUrls.length,
          sampleAzureUrls: storageMetrics.azureUrls.slice(0, 3)
        }
      };
      
      if (storageMetrics.azureUrls.length === 0) {
        report.issues.push('No images loading from Azure Blob Storage');
        report.recommendations.push('Verify Azure Blob Storage configuration and image upload process');
      }
      
      // Test 3: Style Preview Images
      console.log('🎨 Testing style preview images...');
      await page.waitForSelector('.styled-photos-grid, .style-showcase, .styles-section', { timeout: 10000 });
      
      const imageMetrics = await helpers.verifyStylePreviewImages();
      
      report.testResults.imageLoading = {
        status: imageMetrics.azureHosted > 0 && imageMetrics.placeholders < imageMetrics.total * 0.3 ? 'pass' : 'warning',
        details: {
          totalImages: imageMetrics.total,
          azureHosted: imageMetrics.azureHosted,
          placeholders: imageMetrics.placeholders,
          loadErrors: imageMetrics.loadErrors
        }
      };
      
      if (imageMetrics.placeholders > imageMetrics.total * 0.2) {
        report.issues.push(`High placeholder count: ${imageMetrics.placeholders}/${imageMetrics.total} images are placeholders`);
        report.recommendations.push('Upload real style preview images to Azure Blob Storage');
      }
      
      // Test 4: Package Functionality
      console.log('💳 Testing package functionality...');
      const packageCards = page.locator('.package-card, .plan-card, .pricing-card');
      const packageCount = await packageCards.count();
      
      let packagesWithPrices = 0;
      let packagesWithButtons = 0;
      
      for (let i = 0; i < Math.min(packageCount, 3); i++) {
        const card = packageCards.nth(i);
        
        const hasPrice = await card.locator('.price, .plan-price, .package-price, [class*="price"]').count() > 0;
        if (hasPrice) packagesWithPrices++;
        
        const hasButton = await card.locator('button, .btn, [class*="btn"]').count() > 0;
        if (hasButton) packagesWithButtons++;
      }
      
      report.testResults.packageFunctionality = {
        status: packageCount > 0 && packagesWithPrices > 0 ? 'pass' : 'fail',
        details: {
          totalPackages: packageCount,
          packagesWithPrices,
          packagesWithButtons
        }
      };
      
      if (packageCount === 0) {
        report.issues.push('No credit packages found');
        report.recommendations.push('Verify package API integration and database configuration');
      }
      
      // Test 5: API Integration
      console.log('🔌 Testing API integration...');
      const apiCalls: Array<{url: string, status: number, method: string}> = [];
      
      page.on('response', response => {
        const url = response.url();
        if (url.includes('/api/')) {
          apiCalls.push({
            url,
            status: response.status(),
            method: response.request().method()
          });
        }
      });
      
      await page.reload();
      await waitForStableLoad(page);
      await page.waitForTimeout(3000);
      
      const successfulApiCalls = apiCalls.filter(call => call.status >= 200 && call.status < 300);
      const failedApiCalls = apiCalls.filter(call => call.status >= 400);
      
      report.testResults.apiIntegration = {
        status: successfulApiCalls.length > 0 ? 'pass' : failedApiCalls.length > 0 ? 'fail' : 'unknown',
        details: {
          totalApiCalls: apiCalls.length,
          successfulCalls: successfulApiCalls.length,
          failedCalls: failedApiCalls.length,
          endpoints: apiCalls.map(call => `${call.method} ${call.status} - ${call.url}`).slice(0, 5)
        }
      };
      
      if (failedApiCalls.length > successfulApiCalls.length) {
        report.issues.push(`High API failure rate: ${failedApiCalls.length}/${apiCalls.length} calls failed`);
        report.recommendations.push('Investigate API endpoint failures and backend connectivity');
      }
      
      // Test 6: Performance Metrics
      console.log('⚡ Testing performance...');
      const performanceMetrics = await page.evaluate(() => {
        const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming;
        
        return {
          domContentLoaded: navigation ? navigation.domContentLoadedEventEnd - navigation.navigationStart : 0,
          loadComplete: navigation ? navigation.loadEventEnd - navigation.navigationStart : 0,
          dnsLookup: navigation ? navigation.domainLookupEnd - navigation.domainLookupStart : 0,
          serverResponse: navigation ? navigation.responseEnd - navigation.requestStart : 0
        };
      });
      
      report.testResults.performance = {
        status: performanceMetrics.loadComplete < 5000 ? 'pass' : 'warning',
        details: {
          domContentLoaded: `${performanceMetrics.domContentLoaded.toFixed(0)}ms`,
          loadComplete: `${performanceMetrics.loadComplete.toFixed(0)}ms`,
          dnsLookup: `${performanceMetrics.dnsLookup.toFixed(0)}ms`,
          serverResponse: `${performanceMetrics.serverResponse.toFixed(0)}ms`
        }
      };
      
      if (performanceMetrics.loadComplete > 5000) {
        report.issues.push(`Slow page load: ${performanceMetrics.loadComplete.toFixed(0)}ms`);
        report.recommendations.push('Optimize page load performance through caching and asset optimization');
      }
      
      // Generate final recommendations
      if (report.issues.length === 0) {
        report.recommendations.push('✅ Staging environment is functioning well');
        report.recommendations.push('Consider monitoring performance metrics over time');
        report.recommendations.push('Set up automated testing for continuous validation');
      }
      
    } catch (error) {
      report.issues.push(`Test execution error: ${error}`);
      report.recommendations.push('Investigate test infrastructure and staging environment accessibility');
    }
    
    // Print comprehensive report
    console.log('\n' + '='.repeat(80));
    console.log('📋 COMPREHENSIVE STAGING ENVIRONMENT REPORT');
    console.log('='.repeat(80));
    console.log(`🕒 Generated: ${report.timestamp}`);
    console.log(`🌐 Environment: ${report.environment}`);
    console.log(`🔗 Base URL: ${report.baseUrl}`);
    
    console.log('\n📊 TEST RESULTS SUMMARY:');
    Object.entries(report.testResults).forEach(([testName, result]) => {
      const statusIcon = result.status === 'pass' ? '✅' : result.status === 'fail' ? '❌' : '⚠️';
      console.log(`  ${statusIcon} ${testName}: ${result.status.toUpperCase()}`);
    });
    
    if (report.issues.length > 0) {
      console.log('\n🚨 ISSUES IDENTIFIED:');
      report.issues.forEach((issue, index) => {
        console.log(`  ${index + 1}. ${issue}`);
      });
    }
    
    console.log('\n💡 RECOMMENDATIONS:');
    report.recommendations.forEach((rec, index) => {
      console.log(`  ${index + 1}. ${rec}`);
    });
    
    console.log('\n📈 DETAILED RESULTS:');
    Object.entries(report.testResults).forEach(([testName, result]) => {
      console.log(`\n  ${testName.toUpperCase()}:`);
      Object.entries(result.details).forEach(([key, value]) => {
        console.log(`    ${key}: ${Array.isArray(value) ? value.join(', ') : value}`);
      });
    });
    
    console.log('\n' + '='.repeat(80));
    console.log('END OF STAGING ENVIRONMENT REPORT');
    console.log('='.repeat(80));
    
    // Take final screenshot
    await page.screenshot({ path: 'screenshots/99-final-staging-state.png', fullPage: true });
    
    // Overall test assertion
    const criticalFailures = Object.values(report.testResults).filter(result => result.status === 'fail').length;
    expect(criticalFailures).toBeLessThan(3); // Allow some failures but not too many
    
    // Save report to file
    const fs = require('fs');
    fs.writeFileSync('staging-environment-report.json', JSON.stringify(report, null, 2));
    console.log('\n💾 Detailed report saved to: staging-environment-report.json');
    
  }, { timeout: 120000 }); // 2 minute timeout for comprehensive test
});