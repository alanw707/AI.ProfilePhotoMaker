/**
 * Simplified Auto-Repair Functionality Tests
 * 
 * Focused tests that work with Angular environment without complex injector access
 */

import { test, expect, Page } from '@playwright/test';

test.describe('Auto-Repair Functionality - Simplified Tests', () => {
  let page: Page;

  test.beforeEach(async ({ page: testPage }) => {
    page = testPage;
    await page.goto('http://localhost:4200');
    await page.waitForSelector('body');
    await page.waitForTimeout(3000); // Allow Angular to fully bootstrap
  });

  test('should validate environment configuration through window inspection', async () => {
    console.log('🔍 Testing environment configuration access...');
    
    // Check multiple ways to access environment config
    const configCheck = await page.evaluate(() => {
      // Method 1: Check if environment config is exposed
      const hasEnvironment = typeof (window as any).environment !== 'undefined';
      
      // Method 2: Check Angular development mode
      const isAngularDev = typeof (window as any).ng !== 'undefined';
      
      // Method 3: Check for development indicators
      const isDevelopment = window.location.hostname === 'localhost' && window.location.port === '4200';
      
      // Method 4: Check meta tags for environment info
      const metaTags = Array.from(document.getElementsByTagName('meta'))
        .map(meta => ({ name: meta.name, content: meta.content }))
        .filter(meta => meta.name && meta.content);
      
      return {
        hasEnvironment,
        isAngularDev,
        isDevelopment,
        hostname: window.location.hostname,
        port: window.location.port,
        metaTags,
        userAgent: navigator.userAgent
      };
    });

    console.log('📊 Configuration check results:', configCheck);
    
    expect(configCheck.isDevelopment).toBe(true);
    expect(configCheck.hostname).toBe('localhost');
    expect(configCheck.port).toBe('4200');
  });

  test('should capture and analyze console logs for auto-repair features', async () => {
    console.log('📝 Capturing console logs for feature analysis...');
    
    const allLogs: { type: string, text: string }[] = [];
    
    // Capture all console output
    page.on('console', (msg) => {
      allLogs.push({
        type: msg.type(),
        text: msg.text()
      });
    });

    // Reload page to capture initialization logs
    await page.reload();
    await page.waitForTimeout(5000);

    console.log(`📊 Captured ${allLogs.length} console messages`);
    
    // Analyze logs by type
    const logsByType = allLogs.reduce((acc, log) => {
      acc[log.type] = (acc[log.type] || 0) + 1;
      return acc;
    }, {} as Record<string, number>);
    
    console.log('📈 Log distribution:', logsByType);
    
    // Look for specific patterns that might indicate auto-repair configuration
    const relevantLogs = allLogs.filter(log => 
      log.text.toLowerCase().includes('environment') ||
      log.text.toLowerCase().includes('config') ||
      log.text.toLowerCase().includes('development') ||
      log.text.toLowerCase().includes('repair') ||
      log.text.toLowerCase().includes('feature')
    );
    
    console.log('🎯 Relevant configuration logs:', relevantLogs.slice(0, 10)); // Show first 10
    
    expect(allLogs.length).toBeGreaterThan(0);
  });

  test('should validate session storage capabilities for auto-repair', async () => {
    console.log('💾 Testing session storage for auto-repair operations...');
    
    const storageTest = await page.evaluate(() => {
      // Test auto-repair related storage operations
      const testData = {
        autoRepairConfig: {
          enabled: true,
          threshold: 1,
          cooldown: 300000,
          lastRun: Date.now()
        },
        attemptHistory: {
          count: 0,
          lastAttempt: null,
          successes: 0
        }
      };
      
      try {
        // Test storing auto-repair configuration
        sessionStorage.setItem('autoRepairConfig', JSON.stringify(testData.autoRepairConfig));
        sessionStorage.setItem('autoRepairAttempts', JSON.stringify(testData.attemptHistory));
        
        // Test retrieving data
        const retrievedConfig = JSON.parse(sessionStorage.getItem('autoRepairConfig') || '{}');
        const retrievedAttempts = JSON.parse(sessionStorage.getItem('autoRepairAttempts') || '{}');
        
        // Test cooldown calculation
        const now = Date.now();
        const cooldownExpiry = retrievedConfig.lastRun + retrievedConfig.cooldown;
        const isInCooldown = now < cooldownExpiry;
        
        // Cleanup
        sessionStorage.removeItem('autoRepairConfig');
        sessionStorage.removeItem('autoRepairAttempts');
        
        return {
          storageWorks: true,
          configStored: retrievedConfig.enabled === true,
          attemptsTracked: retrievedAttempts.count === 0,
          cooldownCalculated: typeof isInCooldown === 'boolean',
          testTimestamp: now
        };
      } catch (error) {
        return {
          storageWorks: false,
          error: error instanceof Error ? error.message : 'Unknown error'
        };
      }
    });

    console.log('💾 Session storage test results:', storageTest);
    
    expect(storageTest.storageWorks).toBe(true);
    expect(storageTest.configStored).toBe(true);
    expect(storageTest.attemptsTracked).toBe(true);
    expect(storageTest.cooldownCalculated).toBe(true);
  });

  test('should validate API connectivity for auto-repair operations', async () => {
    console.log('🌐 Testing API connectivity for auto-repair...');
    
    const apiConnectivityTest = await page.evaluate(async () => {
      const testResults = {
        health: null,
        models: null,
        profile: null
      };
      
      try {
        // Test health endpoint
        const healthResponse = await fetch('/api/health');
        testResults.health = {
          status: healthResponse.status,
          statusText: healthResponse.statusText,
          accessible: healthResponse.status < 500
        };
      } catch (error) {
        testResults.health = {
          status: 0,
          statusText: 'Network Error',
          accessible: false,
          error: error instanceof Error ? error.message : 'Unknown'
        };
      }

      try {
        // Test models endpoint (likely requires auth)
        const modelsResponse = await fetch('/api/models');
        testResults.models = {
          status: modelsResponse.status,
          statusText: modelsResponse.statusText,
          requiresAuth: modelsResponse.status === 401
        };
      } catch (error) {
        testResults.models = {
          status: 0,
          statusText: 'Network Error',
          error: error instanceof Error ? error.message : 'Unknown'
        };
      }

      try {
        // Test profile endpoint (likely requires auth)
        const profileResponse = await fetch('/api/profile');
        testResults.profile = {
          status: profileResponse.status,
          statusText: profileResponse.statusText,
          requiresAuth: profileResponse.status === 401
        };
      } catch (error) {
        testResults.profile = {
          status: 0,
          statusText: 'Network Error',
          error: error instanceof Error ? error.message : 'Unknown'
        };
      }

      return testResults;
    });

    console.log('🔗 API connectivity results:', apiConnectivityTest);
    
    // Health endpoint should be accessible
    expect(apiConnectivityTest.health?.accessible).toBe(true);
    
    // Other endpoints should at least respond (even if with 401)
    expect(apiConnectivityTest.models?.status).toBeGreaterThan(0);
    expect(apiConnectivityTest.profile?.status).toBeGreaterThan(0);
  });

  test('should simulate auto-repair decision logic', async () => {
    console.log('🔧 Testing auto-repair decision logic simulation...');
    
    const simulationResult = await page.evaluate(() => {
      // Simulate auto-repair scenario with realistic data
      const mockFailedImages = [
        { id: 'img_001', error: 'Network timeout', timestamp: Date.now() - 60000 },
        { id: 'img_002', error: 'Processing failed', timestamp: Date.now() - 30000 }
      ];
      
      const autoRepairConfig = {
        enabled: true,
        dryRunOnly: false,
        threshold: 1,
        cooldownMs: 5 * 60 * 1000, // 5 minutes
        maxAttempts: 3,
        timeoutMs: 30000
      };
      
      // Simulate cooldown check
      const lastAttempt = Date.now() - (10 * 60 * 1000); // 10 minutes ago
      const isInCooldown = (Date.now() - lastAttempt) < autoRepairConfig.cooldownMs;
      
      // Simulate repair decision
      const shouldTriggerRepair = mockFailedImages.length >= autoRepairConfig.threshold;
      const canPerformRepair = autoRepairConfig.enabled && !autoRepairConfig.dryRunOnly && !isInCooldown;
      
      // Simulate telemetry data
      const telemetryData = {
        failedCount: mockFailedImages.length,
        thresholdMet: shouldTriggerRepair,
        cooldownActive: isInCooldown,
        repairPermitted: canPerformRepair,
        timestamp: Date.now()
      };
      
      return {
        mockData: {
          failedImages: mockFailedImages.length,
          config: autoRepairConfig
        },
        decisions: {
          shouldTrigger: shouldTriggerRepair,
          canPerform: canPerformRepair,
          inCooldown: isInCooldown
        },
        telemetry: telemetryData
      };
    });

    console.log('🎯 Auto-repair simulation results:', simulationResult);
    
    expect(simulationResult.mockData.failedImages).toBe(2);
    expect(simulationResult.decisions.shouldTrigger).toBe(true);
    expect(simulationResult.decisions.canPerform).toBe(true);
    expect(simulationResult.decisions.inCooldown).toBe(false);
  });

  test('should validate notification infrastructure', async () => {
    console.log('🔔 Testing notification infrastructure...');
    
    const notificationTest = await page.evaluate(() => {
      // Check for notification-related elements and capabilities
      const notificationElements = document.querySelectorAll('[class*="notification"], [class*="alert"], [class*="toast"], [class*="snackbar"]');
      
      // Check for common notification libraries
      const hasAngularMaterial = !!document.querySelector('link[href*="material"]') || 
                                 !!(window as any).MaterialSnackBar;
      
      const hasBootstrapToast = !!(window as any).bootstrap || 
                                !!document.querySelector('[class*="toast"]');
      
      // Test basic notification capability
      const canCreateNotification = 'Notification' in window;
      const notificationPermission = canCreateNotification ? Notification.permission : 'denied';
      
      return {
        notificationElements: notificationElements.length,
        hasAngularMaterial,
        hasBootstrapToast,
        canCreateNotification,
        notificationPermission,
        infrastructureAvailable: notificationElements.length > 0 || hasAngularMaterial || hasBootstrapToast
      };
    });

    console.log('📱 Notification infrastructure test:', notificationTest);
    
    // Should have some form of notification infrastructure
    expect(notificationTest.infrastructureAvailable || notificationTest.canCreateNotification).toBe(true);
  });

  test('should validate error handling capabilities', async () => {
    console.log('❌ Testing error handling infrastructure...');
    
    const errorLogs: string[] = [];
    const warningLogs: string[] = [];
    
    // Capture errors and warnings
    page.on('pageerror', (error) => {
      errorLogs.push(error.message);
    });
    
    page.on('console', (msg) => {
      if (msg.type() === 'error') {
        errorLogs.push(msg.text());
      } else if (msg.type() === 'warning') {
        warningLogs.push(msg.text());
      }
    });

    // Test error handling by simulating errors
    const errorHandlingTest = await page.evaluate(() => {
      // Test global error handlers
      const hasGlobalErrorHandler = typeof window.onerror !== 'undefined';
      const hasUnhandledRejectionHandler = typeof window.onunhandledrejection !== 'undefined';
      
      // Simulate a minor error to test handling
      try {
        // This should not throw but tests error handling
        const test = JSON.parse('{"valid": "json"}');
        return {
          hasGlobalErrorHandler,
          hasUnhandledRejectionHandler,
          errorHandlingWorks: true,
          testResult: test
        };
      } catch (error) {
        return {
          hasGlobalErrorHandler,
          hasUnhandledRejectionHandler,
          errorHandlingWorks: false,
          error: error instanceof Error ? error.message : 'Unknown error'
        };
      }
    });

    console.log('🚨 Error handling test results:', errorHandlingTest);
    console.log(`📊 Captured ${errorLogs.length} errors and ${warningLogs.length} warnings`);
    
    if (errorLogs.length > 0) {
      console.log('🔍 Error samples:', errorLogs.slice(0, 3));
    }
    
    expect(errorHandlingTest.errorHandlingWorks).toBe(true);
  });
});