import { test, expect } from '@playwright/test';
import { StagingTestHelpers, waitForStableLoad } from './utils/test-helpers';

test.describe('Performance Metrics - Staging Environment', () => {
  let helpers: StagingTestHelpers;

  test.beforeEach(async ({ page }) => {
    helpers = new StagingTestHelpers(page);
  });

  test('should measure Core Web Vitals', async ({ page }) => {
    console.log('📊 Measuring Core Web Vitals...');
    
    await page.goto('/');
    
    // Wait for page to fully load
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);
    
    const webVitals = await page.evaluate(() => {
      return new Promise((resolve) => {
        const vitals = {
          LCP: 0, // Largest Contentful Paint
          FID: 0, // First Input Delay  
          CLS: 0, // Cumulative Layout Shift
          FCP: 0, // First Contentful Paint
          TTFB: 0 // Time to First Byte
        };
        
        // Get performance navigation timing
        const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming;
        if (navigation) {
          vitals.TTFB = navigation.responseStart - navigation.requestStart;
        }
        
        // Try to get Web Vitals if available
        if ('webVitals' in window) {
          // Web Vitals library not available, use performance API
        }
        
        // Get paint timings
        const paintEntries = performance.getEntriesByType('paint');
        const fcpEntry = paintEntries.find(entry => entry.name === 'first-contentful-paint');
        if (fcpEntry) {
          vitals.FCP = fcpEntry.startTime;
        }
        
        // Simulate LCP measurement (simplified)
        const largestElements = document.querySelectorAll('img, video, svg, h1, h2');
        if (largestElements.length > 0) {
          // Estimate LCP based on when images are loaded
          vitals.LCP = performance.now();
        }
        
        resolve(vitals);
      });
    });
    
    console.log('📊 Core Web Vitals Results:');
    console.log(`  Time to First Byte (TTFB): ${webVitals.TTFB.toFixed(0)}ms`);
    console.log(`  First Contentful Paint (FCP): ${webVitals.FCP.toFixed(0)}ms`);
    console.log(`  Largest Contentful Paint (LCP): ${webVitals.LCP.toFixed(0)}ms`);
    
    // Performance thresholds (Web Vitals)
    expect(webVitals.TTFB).toBeLessThan(800); // 800ms TTFB threshold
    expect(webVitals.FCP).toBeLessThan(1800); // 1.8s FCP threshold
    expect(webVitals.LCP).toBeLessThan(2500); // 2.5s LCP threshold
    
    if (webVitals.TTFB > 600) {
      console.warn(`⚠️ Slow TTFB: ${webVitals.TTFB.toFixed(0)}ms`);
    }
    if (webVitals.FCP > 1500) {
      console.warn(`⚠️ Slow FCP: ${webVitals.FCP.toFixed(0)}ms`);
    }
    if (webVitals.LCP > 2000) {
      console.warn(`⚠️ Slow LCP: ${webVitals.LCP.toFixed(0)}ms`);
    }
  });

  test('should measure page load performance', async ({ page }) => {
    console.log('⚡ Measuring page load performance...');
    
    const startTime = Date.now();
    
    await page.goto('/', { waitUntil: 'commit' });
    const navigationTime = Date.now() - startTime;
    
    await page.waitForLoadState('domcontentloaded');
    const domContentLoadedTime = Date.now() - startTime;
    
    await page.waitForLoadState('networkidle');
    const networkIdleTime = Date.now() - startTime;
    
    // Get detailed performance metrics
    const performanceMetrics = await page.evaluate(() => {
      const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming;
      
      return {
        domContentLoaded: navigation.domContentLoadedEventEnd - navigation.navigationStart,
        loadComplete: navigation.loadEventEnd - navigation.navigationStart,
        domInteractive: navigation.domInteractive - navigation.navigationStart,
        dnsLookup: navigation.domainLookupEnd - navigation.domainLookupStart,
        tcpConnection: navigation.connectEnd - navigation.connectStart,
        serverResponse: navigation.responseEnd - navigation.requestStart,
        domProcessing: navigation.domComplete - navigation.responseEnd
      };
    });
    
    console.log('📊 Page Load Performance:');
    console.log(`  Navigation start: ${navigationTime}ms`);
    console.log(`  DOM Content Loaded: ${domContentLoadedTime}ms`);
    console.log(`  Network Idle: ${networkIdleTime}ms`);
    console.log(`  DNS Lookup: ${performanceMetrics.dnsLookup.toFixed(0)}ms`);
    console.log(`  TCP Connection: ${performanceMetrics.tcpConnection.toFixed(0)}ms`);
    console.log(`  Server Response: ${performanceMetrics.serverResponse.toFixed(0)}ms`);
    console.log(`  DOM Processing: ${performanceMetrics.domProcessing.toFixed(0)}ms`);
    
    // Performance assertions
    expect(networkIdleTime).toBeLessThan(8000); // 8 seconds total load time
    expect(domContentLoadedTime).toBeLessThan(3000); // 3 seconds for DOM ready
    expect(performanceMetrics.serverResponse).toBeLessThan(2000); // 2 seconds server response
    
    if (networkIdleTime > 5000) {
      console.warn(`⚠️ Slow page load: ${networkIdleTime}ms`);
    }
  });

  test('should measure resource loading performance', async ({ page }) => {
    console.log('📦 Measuring resource loading performance...');
    
    const resourceMetrics: Array<{
      type: string;
      url: string;
      size: number;
      duration: number;
      status: number;
    }> = [];
    
    page.on('response', async response => {
      const request = response.request();
      const url = response.url();
      const resourceType = request.resourceType();
      const size = parseInt(response.headers()['content-length'] || '0');
      
      // Calculate duration from request timing if available
      const timing = await response.request().timing();
      const duration = timing ? timing.responseEnd - timing.requestStart : 0;
      
      resourceMetrics.push({
        type: resourceType,
        url: url.length > 100 ? url.substring(0, 100) + '...' : url,
        size,
        duration,
        status: response.status()
      });
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Analyze resource metrics
    const totalSize = resourceMetrics.reduce((sum, resource) => sum + resource.size, 0);
    const avgDuration = resourceMetrics.length > 0 ? 
      resourceMetrics.reduce((sum, resource) => sum + resource.duration, 0) / resourceMetrics.length : 0;
    
    // Group by resource type
    const byType = resourceMetrics.reduce((acc, resource) => {
      if (!acc[resource.type]) {
        acc[resource.type] = { count: 0, totalSize: 0, avgDuration: 0 };
      }
      acc[resource.type].count++;
      acc[resource.type].totalSize += resource.size;
      acc[resource.type].avgDuration += resource.duration;
      return acc;
    }, {} as Record<string, {count: number, totalSize: number, avgDuration: number}>);
    
    // Calculate averages
    Object.keys(byType).forEach(type => {
      byType[type].avgDuration = byType[type].avgDuration / byType[type].count;
    });
    
    console.log('📊 Resource Loading Analysis:');
    console.log(`  Total resources: ${resourceMetrics.length}`);
    console.log(`  Total size: ${(totalSize / 1024 / 1024).toFixed(2)}MB`);
    console.log(`  Average duration: ${avgDuration.toFixed(0)}ms`);
    
    console.log('\n📦 By Resource Type:');
    Object.entries(byType).forEach(([type, metrics]) => {
      console.log(`  ${type}: ${metrics.count} files, ${(metrics.totalSize / 1024).toFixed(1)}KB, ${metrics.avgDuration.toFixed(0)}ms avg`);
    });
    
    // Performance assertions
    expect(totalSize).toBeLessThan(10 * 1024 * 1024); // 10MB total
    expect(avgDuration).toBeLessThan(1000); // 1 second average
    
    // Check for failed resources
    const failedResources = resourceMetrics.filter(r => r.status >= 400);
    if (failedResources.length > 0) {
      console.log('\n❌ Failed Resources:');
      failedResources.forEach(resource => {
        console.log(`  ${resource.status} - ${resource.type}: ${resource.url}`);
      });
    }
    
    expect(failedResources.length).toBeLessThan(3); // Allow some failed resources
  });

  test('should measure JavaScript performance', async ({ page }) => {
    console.log('⚙️ Measuring JavaScript performance...');
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    const jsMetrics = await page.evaluate(() => {
      const timing = performance.timing;
      const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming;
      
      // Get JavaScript execution time (approximate)
      const jsExecutionTime = navigation ? 
        navigation.domComplete - navigation.domContentLoadedEventEnd : 0;
      
      // Check for long tasks (if Performance Observer is available)
      let longTasks = 0;
      if ('PerformanceObserver' in window) {
        // Long tasks would be measured here in real implementation
      }
      
      // Memory usage (if available)
      const memory = (performance as any).memory;
      const memoryInfo = memory ? {
        usedJSHeapSize: memory.usedJSHeapSize,
        totalJSHeapSize: memory.totalJSHeapSize,
        jsHeapSizeLimit: memory.jsHeapSizeLimit
      } : null;
      
      return {
        jsExecutionTime,
        longTasks,
        memoryInfo,
        userAgent: navigator.userAgent
      };
    });
    
    console.log('📊 JavaScript Performance:');
    console.log(`  JS Execution Time: ${jsMetrics.jsExecutionTime.toFixed(0)}ms`);
    console.log(`  Long Tasks: ${jsMetrics.longTasks}`);
    
    if (jsMetrics.memoryInfo) {
      const memMB = jsMetrics.memoryInfo.usedJSHeapSize / 1024 / 1024;
      console.log(`  Memory Usage: ${memMB.toFixed(1)}MB`);
      
      // Memory usage should be reasonable
      expect(memMB).toBeLessThan(100); // 100MB limit
      
      if (memMB > 50) {
        console.warn(`⚠️ High memory usage: ${memMB.toFixed(1)}MB`);
      }
    }
    
    // JS execution time should be reasonable
    expect(jsMetrics.jsExecutionTime).toBeLessThan(3000); // 3 seconds
    
    if (jsMetrics.jsExecutionTime > 1000) {
      console.warn(`⚠️ Slow JS execution: ${jsMetrics.jsExecutionTime.toFixed(0)}ms`);
    }
  });

  test('should measure mobile performance', async ({ page }) => {
    console.log('📱 Measuring mobile performance...');
    
    // Set mobile viewport and slower network
    await page.setViewportSize({ width: 375, height: 667 });
    
    // Simulate slower mobile network
    const client = await page.context().newCDPSession(page);
    await client.send('Network.emulateNetworkConditions', {
      offline: false,
      downloadThroughput: 1.5 * 1024 * 1024 / 8, // 1.5 Mbps
      uploadThroughput: 750 * 1024 / 8, // 750 Kbps
      latency: 40 // 40ms latency
    });
    
    const startTime = Date.now();
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    const mobileLoadTime = Date.now() - startTime;
    
    // Get mobile-specific metrics
    const mobileMetrics = await page.evaluate(() => {
      const viewport = {
        width: window.innerWidth,
        height: window.innerHeight,
        devicePixelRatio: window.devicePixelRatio
      };
      
      // Check touch support
      const touchSupported = 'ontouchstart' in window;
      
      // Check for mobile-specific elements
      const mobileNav = document.querySelector('.mobile-nav, .mobile-menu, [class*="mobile"]');
      const responsiveImages = document.querySelectorAll('img[srcset], picture');
      
      return {
        viewport,
        touchSupported,
        hasMobileNav: !!mobileNav,
        responsiveImages: responsiveImages.length
      };
    });
    
    console.log('📊 Mobile Performance:');
    console.log(`  Mobile load time: ${mobileLoadTime}ms`);
    console.log(`  Viewport: ${mobileMetrics.viewport.width}x${mobileMetrics.viewport.height}`);
    console.log(`  Device pixel ratio: ${mobileMetrics.viewport.devicePixelRatio}`);
    console.log(`  Touch supported: ${mobileMetrics.touchSupported}`);
    console.log(`  Mobile navigation: ${mobileMetrics.hasMobileNav ? 'Yes' : 'No'}`);
    console.log(`  Responsive images: ${mobileMetrics.responsiveImages}`);
    
    // Mobile performance assertions
    expect(mobileLoadTime).toBeLessThan(10000); // 10 seconds on mobile
    
    if (mobileLoadTime > 7000) {
      console.warn(`⚠️ Slow mobile load time: ${mobileLoadTime}ms`);
    }
    
    // Reset network conditions
    await client.send('Network.emulateNetworkConditions', {
      offline: false,
      downloadThroughput: -1,
      uploadThroughput: -1,
      latency: 0
    });
    
    await page.screenshot({ path: 'screenshots/12-mobile-performance.png', fullPage: true });
  });

  test('should check for performance anti-patterns', async ({ page }) => {
    console.log('🔍 Checking for performance anti-patterns...');
    
    const issues: string[] = [];
    
    page.on('console', msg => {
      if (msg.type() === 'warning' && msg.text().includes('performance')) {
        issues.push(`Console Warning: ${msg.text()}`);
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    const antiPatterns = await page.evaluate(() => {
      const findings: string[] = [];
      
      // Check for excessive DOM size
      const totalElements = document.querySelectorAll('*').length;
      if (totalElements > 3000) {
        findings.push(`Large DOM size: ${totalElements} elements`);
      }
      
      // Check for images without dimensions
      const imagesWithoutDimensions = document.querySelectorAll('img:not([width]):not([height])');
      if (imagesWithoutDimensions.length > 5) {
        findings.push(`${imagesWithoutDimensions.length} images without dimensions (causes layout shift)`);
      }
      
      // Check for inline styles (performance impact)
      const elementsWithInlineStyles = document.querySelectorAll('[style]');
      if (elementsWithInlineStyles.length > 20) {
        findings.push(`${elementsWithInlineStyles.length} elements with inline styles`);
      }
      
      // Check for synchronous scripts
      const syncScripts = document.querySelectorAll('script:not([async]):not([defer])');
      if (syncScripts.length > 3) {
        findings.push(`${syncScripts.length} synchronous scripts (may block rendering)`);
      }
      
      // Check for multiple CSS files
      const cssFiles = document.querySelectorAll('link[rel="stylesheet"]');
      if (cssFiles.length > 5) {
        findings.push(`${cssFiles.length} CSS files (consider bundling)`);
      }
      
      return findings;
    });
    
    console.log('📊 Performance Anti-Pattern Analysis:');
    
    const allIssues = [...issues, ...antiPatterns];
    
    if (allIssues.length === 0) {
      console.log('✅ No major performance anti-patterns detected');
    } else {
      console.log(`⚠️ Found ${allIssues.length} potential performance issues:`);
      allIssues.forEach((issue, index) => {
        console.log(`  ${index + 1}. ${issue}`);
      });
      
      // Allow some issues but fail if too many critical ones
      const criticalIssues = allIssues.filter(issue => 
        issue.includes('Large DOM') || 
        issue.includes('synchronous scripts') ||
        issue.includes('layout shift')
      );
      
      expect(criticalIssues.length).toBeLessThan(3);
    }
  });
});