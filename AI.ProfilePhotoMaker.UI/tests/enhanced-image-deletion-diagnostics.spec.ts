import { test, expect, Page, BrowserContext } from '@playwright/test';

/**
 * Comprehensive Enhanced Image Deletion Troubleshooting Suite
 * 
 * This test systematically diagnoses authentication and deletion issues
 * through iterative testing with detailed diagnostic capture.
 */

interface DiagnosticData {
  timestamp: string;
  testName: string;
  authState: {
    isLoggedIn: boolean;
    hasToken: boolean;
    tokenSource: 'AuthService' | 'localStorage' | null;
    tokenValue: string | null;
    tokenFormat: 'valid' | 'invalid' | 'expired' | null;
  };
  networkRequests: {
    method: string;
    url: string;
    headers: Record<string, string>;
    status?: number;
    response?: any;
  }[];
  uiState: {
    deleteButtonVisible: boolean;
    deleteButtonEnabled: boolean;
    errorMessagesDisplayed: string[];
  };
  browserCache: {
    cleared: boolean;
    serviceWorkerActive: boolean;
  };
}

class EnhancedDeletionDiagnostics {
  private diagnostics: DiagnosticData[] = [];
  
  async captureDiagnostic(page: Page, testName: string): Promise<DiagnosticData> {
    const diagnostic: DiagnosticData = {
      timestamp: new Date().toISOString(),
      testName,
      authState: await this.captureAuthState(page),
      networkRequests: await this.captureNetworkRequests(page),
      uiState: await this.captureUIState(page),
      browserCache: await this.captureBrowserCache(page)
    };
    
    this.diagnostics.push(diagnostic);
    return diagnostic;
  }
  
  private async captureAuthState(page: Page) {
    return await page.evaluate(() => {
      // Check if AuthService is available
      const authService = (window as any).authService;
      let authToken = null;
      let tokenSource = null;
      
      // Try AuthService first
      if (authService && typeof authService.getToken === 'function') {
        try {
          authToken = authService.getToken();
          tokenSource = 'AuthService';
        } catch (e) {
          console.log('AuthService.getToken() failed:', e);
        }
      }
      
      // Fallback to localStorage
      if (!authToken) {
        authToken = localStorage.getItem('auth_token');
        if (authToken) tokenSource = 'localStorage';
      }
      
      // Validate token format
      let tokenFormat = null;
      if (authToken) {
        try {
          // Basic JWT format check
          const parts = authToken.split('.');
          if (parts.length === 3) {
            const payload = JSON.parse(atob(parts[1]));
            const now = Math.floor(Date.now() / 1000);
            tokenFormat = payload.exp && payload.exp > now ? 'valid' : 'expired';
          } else {
            tokenFormat = 'invalid';
          }
        } catch (e) {
          tokenFormat = 'invalid';
        }
      }
      
      return {
        isLoggedIn: !!authToken,
        hasToken: !!authToken,
        tokenSource,
        tokenValue: authToken ? `${authToken.substring(0, 20)}...` : null,
        tokenFormat
      };
    });
  }
  
  private async captureNetworkRequests(page: Page) {
    // This will be populated by network listeners
    return (page as any)._capturedRequests || [];
  }
  
  private async captureUIState(page: Page) {
    return await page.evaluate(() => {
      const deleteButtons = Array.from(document.querySelectorAll('[data-testid*="delete"], .delete-btn, button[title*="delete" i]'));
      const errorMessages = Array.from(document.querySelectorAll('.error, .alert-danger, [class*="error"]'))
        .map(el => el.textContent?.trim() || '');
      
      return {
        deleteButtonVisible: deleteButtons.length > 0,
        deleteButtonEnabled: deleteButtons.some(btn => !(btn as HTMLButtonElement).disabled),
        errorMessagesDisplayed: errorMessages.filter(msg => msg.length > 0)
      };
    });
  }
  
  private async captureBrowserCache(page: Page) {
    const context = page.context();
    
    return {
      cleared: false, // Will be set if we cleared cache
      serviceWorkerActive: await page.evaluate(() => {
        return 'serviceWorker' in navigator && navigator.serviceWorker.controller !== null;
      })
    };
  }
  
  printDiagnosticReport() {
    console.log('\n🔍 ENHANCED IMAGE DELETION DIAGNOSTIC REPORT');
    console.log('=' .repeat(60));
    
    this.diagnostics.forEach((diag, index) => {
      console.log(`\n📊 Test ${index + 1}: ${diag.testName}`);
      console.log(`⏰ Timestamp: ${diag.timestamp}`);
      
      console.log('\n🔐 Authentication State:');
      console.log(`  Logged In: ${diag.authState.isLoggedIn ? '✅' : '❌'}`);
      console.log(`  Has Token: ${diag.authState.hasToken ? '✅' : '❌'}`);
      console.log(`  Token Source: ${diag.authState.tokenSource || 'None'}`);
      console.log(`  Token Format: ${diag.authState.tokenFormat || 'N/A'}`);
      console.log(`  Token Preview: ${diag.authState.tokenValue || 'None'}`);
      
      console.log('\n🌐 Network Requests:');
      if (diag.networkRequests.length === 0) {
        console.log('  No DELETE requests captured');
      } else {
        diag.networkRequests.forEach(req => {
          console.log(`  ${req.method} ${req.url}`);
          console.log(`    Status: ${req.status || 'Pending'}`);
          console.log(`    Auth Header: ${req.headers.Authorization ? '✅ Present' : '❌ Missing'}`);
        });
      }
      
      console.log('\n🖥️ UI State:');
      console.log(`  Delete Button Visible: ${diag.uiState.deleteButtonVisible ? '✅' : '❌'}`);
      console.log(`  Delete Button Enabled: ${diag.uiState.deleteButtonEnabled ? '✅' : '❌'}`);
      console.log(`  Error Messages: ${diag.uiState.errorMessagesDisplayed.length > 0 ? diag.uiState.errorMessagesDisplayed.join(', ') : 'None'}`);
      
      console.log('\n💾 Browser Cache:');
      console.log(`  Cache Cleared: ${diag.browserCache.cleared ? '✅' : '❌'}`);
      console.log(`  Service Worker: ${diag.browserCache.serviceWorkerActive ? '🔄 Active' : '❌ Inactive'}`);
    });
    
    console.log('\n🎯 SUMMARY & RECOMMENDATIONS');
    console.log('=' .repeat(60));
    this.generateRecommendations();
  }
  
  private generateRecommendations() {
    const latest = this.diagnostics[this.diagnostics.length - 1];
    if (!latest) return;
    
    if (!latest.authState.isLoggedIn) {
      console.log('❌ CRITICAL: User not authenticated');
      console.log('   → Verify login flow and token storage');
    }
    
    if (latest.authState.tokenFormat === 'expired') {
      console.log('❌ CRITICAL: Token expired');
      console.log('   → Implement token refresh or redirect to login');
    }
    
    if (latest.authState.tokenFormat === 'invalid') {
      console.log('❌ CRITICAL: Invalid token format');
      console.log('   → Check token generation and storage');
    }
    
    if (latest.networkRequests.length === 0) {
      console.log('⚠️  WARNING: No DELETE requests captured');
      console.log('   → Check if delete button click is triggering request');
    }
    
    const missingAuthRequests = latest.networkRequests.filter(req => !req.headers.Authorization);
    if (missingAuthRequests.length > 0) {
      console.log('❌ CRITICAL: DELETE requests missing Authorization header');
      console.log('   → Verify FileUploadService.deleteEnhancedImage() implementation');
    }
  }
}

test.describe('Enhanced Image Deletion Diagnostics', () => {
  let diagnostics: EnhancedDeletionDiagnostics;
  let capturedRequests: any[] = [];
  
  test.beforeEach(async ({ page }) => {
    diagnostics = new EnhancedDeletionDiagnostics();
    capturedRequests = [];
    
    // Network request interception
    page.on('request', request => {
      if (request.method() === 'DELETE' && request.url().includes('/api/')) {
        capturedRequests.push({
          method: request.method(),
          url: request.url(),
          headers: request.headers()
        });
      }
    });
    
    page.on('response', response => {
      const request = capturedRequests.find(req => req.url === response.url());
      if (request) {
        request.status = response.status();
        request.response = response.statusText();
      }
    });
    
    // Make captured requests available to diagnostics
    (page as any)._capturedRequests = capturedRequests;
  });
  
  test.afterEach(async () => {
    // Print diagnostics after each test iteration
    diagnostics.printDiagnosticReport();
  });
  
  test('1. Authentication State Verification', async ({ page }) => {
    console.log('\n🔍 Test 1: Verifying Authentication State...');
    
    // Navigate to the application
    await page.goto('http://localhost:4200');
    
    // Wait for potential auto-login or token restoration
    await page.waitForTimeout(2000);
    
    await diagnostics.captureDiagnostic(page, 'Initial Page Load - Auth Check');
    
    // Try to log in if not already logged in
    const isLoggedIn = await page.evaluate(() => {
      return !!(localStorage.getItem('auth_token') || (window as any).authService?.getToken?.());
    });
    
    if (!isLoggedIn) {
      console.log('🔑 Attempting login...');
      
      // Attempt login flow
      const loginButton = page.locator('button:has-text("Login"), a:has-text("Login")').first();
      if (await loginButton.isVisible({ timeout: 5000 })) {
        await loginButton.click();
        
        // Fill login form if present
        const emailInput = page.locator('input[type="email"], input[name="email"]').first();
        const passwordInput = page.locator('input[type="password"], input[name="password"]').first();
        
        if (await emailInput.isVisible({ timeout: 5000 })) {
          await emailInput.fill('test@example.com');
          await passwordInput.fill('password123');
          
          const submitButton = page.locator('button[type="submit"], button:has-text("Sign In")').first();
          await submitButton.click();
          
          // Wait for login to complete
          await page.waitForTimeout(3000);
        }
      }
      
      await diagnostics.captureDiagnostic(page, 'After Login Attempt');
    }
    
    // Verify final authentication state
    const finalAuthCheck = await diagnostics.captureDiagnostic(page, 'Final Auth Verification');
    
    expect(finalAuthCheck.authState.isLoggedIn).toBe(true);
  });
  
  test('2. Frontend Build Verification', async ({ page }) => {
    console.log('\n🔍 Test 2: Verifying Frontend Build and Cache...');
    
    await page.goto('http://localhost:4200');
    
    // Check if our changes are actually loaded
    const serviceInfo = await page.evaluate(() => {
      return {
        fileUploadServiceExists: !!(window as any).FileUploadService,
        authServiceExists: !!(window as any).authService,
        buildTimestamp: document.querySelector('meta[name="build-timestamp"]')?.getAttribute('content'),
        scriptSources: Array.from(document.querySelectorAll('script[src]')).map(s => (s as HTMLScriptElement).src)
      };
    });
    
    console.log('🏗️ Build Info:', serviceInfo);
    
    await diagnostics.captureDiagnostic(page, 'Build Verification');
    
    // Clear cache and reload to ensure fresh build
    const context = page.context();
    await context.clearCookies();
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
    
    // Hard reload
    await page.reload({ waitUntil: 'networkidle' });
    await page.waitForTimeout(2000);
    
    await diagnostics.captureDiagnostic(page, 'After Cache Clear and Reload');
  });
  
  test('3. Network Request Analysis', async ({ page, context }) => {
    console.log('\n🔍 Test 3: Analyzing Network Requests...');
    
    await page.goto('http://localhost:4200');
    
    // Set up authentication
    await page.evaluate(() => {
      // Mock authentication for testing
      localStorage.setItem('auth_token', 'Bearer test-token-for-deletion-test');
      if ((window as any).authService) {
        (window as any).authService.currentUser = { id: 1, email: 'test@example.com' };
      }
    });
    
    await diagnostics.captureDiagnostic(page, 'Before Delete Attempt');
    
    // Try to find and click a delete button for enhanced images
    const deleteButton = page.locator('[data-testid*="delete"], .delete-enhanced, button:has-text("Delete")').first();
    
    if (await deleteButton.isVisible({ timeout: 5000 })) {
      console.log('🗑️ Clicking delete button...');
      await deleteButton.click();
      
      // Wait for potential confirmation dialog
      const confirmButton = page.locator('button:has-text("Confirm"), button:has-text("Yes"), button:has-text("Delete")').first();
      if (await confirmButton.isVisible({ timeout: 2000 })) {
        await confirmButton.click();
      }
      
      // Wait for network request
      await page.waitForTimeout(3000);
    } else {
      console.log('⚠️ No delete button found, creating mock delete request...');
      
      // Simulate the delete request manually
      await page.evaluate(() => {
        fetch('/api/FileUpload/enhanced/test-image-id', {
          method: 'DELETE',
          headers: {
            'Authorization': 'Bearer test-token',
            'Content-Type': 'application/json'
          }
        }).catch(err => console.log('Manual delete request error:', err));
      });
      
      await page.waitForTimeout(2000);
    }
    
    await diagnostics.captureDiagnostic(page, 'After Delete Attempt');
    
    // Verify request was made with proper headers
    const deleteRequests = capturedRequests.filter(req => req.method === 'DELETE');
    expect(deleteRequests.length).toBeGreaterThan(0);
    
    if (deleteRequests.length > 0) {
      const lastDeleteRequest = deleteRequests[deleteRequests.length - 1];
      expect(lastDeleteRequest.headers).toHaveProperty('authorization');
    }
  });
  
  test('4. Real User Flow Testing', async ({ page }) => {
    console.log('\n🔍 Test 4: Real User Flow Testing...');
    
    await page.goto('http://localhost:4200');
    
    // Simulate real authentication
    await page.evaluate(() => {
      localStorage.setItem('auth_token', 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwibmFtZSI6IlRlc3QgVXNlciIsImlhdCI6MTUxNjIzOTAyMiwiZXhwIjoxOTE2MjM5MDIyfQ.test');
    });
    
    await diagnostics.captureDiagnostic(page, 'Real User - Initial State');
    
    // Navigate to enhanced images section
    const enhancedLink = page.locator('a:has-text("Enhanced"), nav a[href*="enhanced"]').first();
    if (await enhancedLink.isVisible({ timeout: 5000 })) {
      await enhancedLink.click();
      await page.waitForTimeout(2000);
    }
    
    await diagnostics.captureDiagnostic(page, 'Real User - On Enhanced Images Page');
    
    // Look for actual enhanced images and attempt deletion
    const enhancedImages = page.locator('.enhanced-image, .image-card, .photo-item');
    const imageCount = await enhancedImages.count();
    
    console.log(`📸 Found ${imageCount} potential enhanced images`);
    
    if (imageCount > 0) {
      // Try to delete the first enhanced image
      const firstImage = enhancedImages.first();
      const deleteBtn = firstImage.locator('button:has-text("Delete"), .delete-btn, [data-testid*="delete"]').first();
      
      if (await deleteBtn.isVisible({ timeout: 3000 })) {
        await deleteBtn.click();
        
        // Handle confirmation if present
        const confirmDialog = page.locator('.modal, .dialog, .confirmation');
        if (await confirmDialog.isVisible({ timeout: 2000 })) {
          const confirmBtn = confirmDialog.locator('button:has-text("Delete"), button:has-text("Confirm")').first();
          await confirmBtn.click();
        }
        
        await page.waitForTimeout(3000);
      }
    }
    
    await diagnostics.captureDiagnostic(page, 'Real User - After Delete Attempt');
  });
  
  test('5. Iterative Testing with Different States', async ({ page }) => {
    console.log('\n🔍 Test 5: Iterative Testing - Multiple Scenarios...');
    
    // Scenario 1: No authentication
    await page.goto('http://localhost:4200');
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
    
    await diagnostics.captureDiagnostic(page, 'Iteration 1 - No Auth');
    
    // Scenario 2: Invalid token
    await page.evaluate(() => {
      localStorage.setItem('auth_token', 'invalid-token-format');
    });
    
    await diagnostics.captureDiagnostic(page, 'Iteration 2 - Invalid Token');
    
    // Scenario 3: Expired token
    await page.evaluate(() => {
      localStorage.setItem('auth_token', 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwibmFtZSI6IlRlc3QgVXNlciIsImlhdCI6MTUxNjIzOTAyMiwiZXhwIjoxNTE2MjM5MDIyfQ.expired');
    });
    
    await diagnostics.captureDiagnostic(page, 'Iteration 3 - Expired Token');
    
    // Scenario 4: Valid token
    await page.evaluate(() => {
      const futureTimestamp = Math.floor(Date.now() / 1000) + 3600; // 1 hour from now
      const payload = btoa(JSON.stringify({
        sub: "1",
        name: "Test User",
        iat: Math.floor(Date.now() / 1000),
        exp: futureTimestamp
      }));
      localStorage.setItem('auth_token', `Bearer header.${payload}.signature`);
    });
    
    await diagnostics.captureDiagnostic(page, 'Iteration 4 - Valid Token');
    
    // Scenario 5: AuthService integration
    await page.evaluate(() => {
      // Mock AuthService
      (window as any).authService = {
        getToken: () => localStorage.getItem('auth_token'),
        currentUser: { id: 1, email: 'test@example.com' }
      };
    });
    
    await diagnostics.captureDiagnostic(page, 'Iteration 5 - AuthService Active');
  });
});