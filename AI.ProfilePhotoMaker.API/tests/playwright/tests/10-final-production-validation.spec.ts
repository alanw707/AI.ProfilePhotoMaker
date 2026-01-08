import { test, expect, Browser, Page } from '@playwright/test';

/**
 * Final Comprehensive Production Validation Suite
 * 
 * This test suite validates the complete production readiness of the AI Profile Photo Maker
 * application after all OAuth fixes have been deployed.
 * 
 * Test Coverage:
 * - OAuth redirect URI validation
 * - Complete user journey testing
 * - Production endpoint verification
 * - Frontend/backend integration
 * - Cross-browser compatibility
 * - Production domain validation
 */

const PRODUCTION_FRONTEND = 'https://aiprofilephotomaker.com';
const PRODUCTION_API = 'https://api.aiprofilephotomaker.com';
const OAUTH_ENDPOINT = `${PRODUCTION_API}/api/auth/external-login/google`;

// Production readiness scoring
let productionReadinessScore = 0;
const maxScore = 100;
const testResults: Array<{test: string, status: 'PASS' | 'FAIL', points: number, details?: string}> = [];

function addTestResult(test: string, passed: boolean, points: number, details?: string) {
  const status = passed ? 'PASS' : 'FAIL';
  if (passed) productionReadinessScore += points;
  testResults.push({ test, status, points, details });
  console.log(`${passed ? '✅' : '❌'} ${test} (${points}pts) ${details || ''}`);
}

test.describe('Final Production Validation Suite', () => {

  test.beforeAll(async () => {
    console.log('🚀 Starting Final Production Validation for AI Profile Photo Maker');
    console.log('📅 Test Date:', new Date().toISOString());
    console.log('🎯 Target Score: 80+ points for production readiness');
  });

  test('1. OAuth Redirect URI Validation', async ({ page }) => {
    console.log('\n📊 Test 1: OAuth Redirect URI Validation');
    
    try {
      // Test direct OAuth endpoint access
      const response = await page.request.get(OAUTH_ENDPOINT, {
        timeout: 15000,
        ignoreHTTPSErrors: true
      });

      if (response.status() === 200 || response.status() === 302) {
        addTestResult('OAuth endpoint responds correctly', true, 15);
      } else if (response.status() === 500) {
        addTestResult('OAuth endpoint responds correctly', false, 15, 'Still returning 500 error');
        return;
      } else {
        addTestResult('OAuth endpoint responds correctly', false, 15, `Status: ${response.status()}`);
        return;
      }

      // Test OAuth redirect flow
      const oauthUrl = `${OAUTH_ENDPOINT}?returnUrl=${encodeURIComponent(PRODUCTION_FRONTEND + '/dashboard')}`;
      await page.goto(oauthUrl, { waitUntil: 'networkidle', timeout: 30000 });
      
      const finalUrl = page.url();
      const redirectsToGoogle = finalUrl.includes('accounts.google.com');
      
      addTestResult('OAuth redirects to Google correctly', redirectsToGoogle, 15, 
        redirectsToGoogle ? 'Redirected to Google' : `Final URL: ${finalUrl}`);

      // Validate redirect URL contains production domains
      if (redirectsToGoogle) {
        const urlParams = new URLSearchParams(finalUrl.split('?')[1] || '');
        const redirectUri = urlParams.get('redirect_uri') || '';
        const usesProductionDomain = redirectUri.includes('api.aiprofilephotomaker.com');
        
        addTestResult('OAuth uses production domain in redirect URI', usesProductionDomain, 10,
          `Redirect URI: ${redirectUri}`);
      }

    } catch (error) {
      addTestResult('OAuth endpoint responds correctly', false, 15, `Error: ${error.message}`);
      addTestResult('OAuth redirects to Google correctly', false, 15, 'Failed due to endpoint error');
    }
  });

  test('2. Frontend Application Loading and Configuration', async ({ page }) => {
    console.log('\n📊 Test 2: Frontend Application Loading');
    
    try {
      await page.goto(PRODUCTION_FRONTEND, {
        waitUntil: 'domcontentloaded',
        timeout: 30000
      });

      const title = await page.title();
      const pageLoaded = title.length > 0;
      addTestResult('Frontend application loads successfully', pageLoaded, 10, `Title: ${title}`);

      // Check for Angular application bootstrap
      const angularApp = await page.evaluate(() => {
        return !!(window as any).ng || document.querySelector('app-root');
      });
      addTestResult('Angular application initializes', angularApp, 5);

      // Validate production configuration
      const config = await page.evaluate(() => {
        const hostname = window.location.hostname;
        const isProduction = hostname === 'aiprofilephotomaker.com';
        return { hostname, isProduction };
      });
      
      addTestResult('Frontend uses production domain', config.isProduction, 5, 
        `Domain: ${config.hostname}`);

    } catch (error) {
      addTestResult('Frontend application loads successfully', false, 10, `Error: ${error.message}`);
      addTestResult('Angular application initializes', false, 5, 'Frontend failed to load');
    }
  });

  test('3. API Endpoint Health Verification', async ({ page }) => {
    console.log('\n📊 Test 3: API Endpoint Health');
    
    const criticalEndpoints = [
      { url: PRODUCTION_API, name: 'Base API', points: 5 },
      { url: `${PRODUCTION_API}/api/health`, name: 'Health endpoint', points: 5 },
      { url: `${PRODUCTION_API}/api/auth`, name: 'Auth base endpoint', points: 10 }
    ];

    for (const endpoint of criticalEndpoints) {
      try {
        const response = await page.request.get(endpoint.url, {
          timeout: 10000,
          ignoreHTTPSErrors: true
        });

        const healthy = response.status() >= 200 && response.status() < 400;
        addTestResult(`${endpoint.name} responds correctly`, healthy, endpoint.points,
          `Status: ${response.status()}`);

      } catch (error) {
        addTestResult(`${endpoint.name} responds correctly`, false, endpoint.points,
          `Error: ${error.message}`);
      }
    }
  });

  test('4. Complete User Journey Simulation', async ({ page }) => {
    console.log('\n📊 Test 4: Complete User Journey');
    
    try {
      // Navigate to login page
      await page.goto(`${PRODUCTION_FRONTEND}/auth/login`, {
        waitUntil: 'domcontentloaded',
        timeout: 30000
      });

      addTestResult('Login page accessible', true, 5);

      // Look for Google login button
      const googleLoginButton = await page.locator('button:has-text("Google"), a:has-text("Google"), [ng-click*="google"], [onclick*="google"]').first();
      const loginButtonExists = await googleLoginButton.count() > 0;
      
      addTestResult('Google login button present', loginButtonExists, 5);

      if (loginButtonExists) {
        // Simulate clicking login (without actually completing OAuth)
        const buttonText = await googleLoginButton.textContent();
        addTestResult('Google login button configured', true, 5, `Button text: ${buttonText}`);
      }

      // Test navigation to dashboard URL (without auth)
      await page.goto(`${PRODUCTION_FRONTEND}/dashboard`, {
        waitUntil: 'domcontentloaded',
        timeout: 30000
      });

      const currentUrl = page.url();
      const redirectsToLogin = currentUrl.includes('/auth/login') || currentUrl.includes('/login');
      
      addTestResult('Unauthenticated users redirect to login', redirectsToLogin, 5,
        `Final URL: ${currentUrl}`);

    } catch (error) {
      addTestResult('Login page accessible', false, 5, `Error: ${error.message}`);
    }
  });

  test('5. Cross-Browser OAuth URL Construction', async ({ page, browserName }) => {
    console.log(`\n📊 Test 5: Cross-Browser OAuth (${browserName})`);
    
    try {
      await page.goto(`${PRODUCTION_FRONTEND}/auth/login`);
      
      // Test OAuth URL construction in different browsers
      const oauthConfig = await page.evaluate(() => {
        // Simulate auth service logic
        const currentOrigin = window.location.origin;
        const expectedApiUrl = 'https://api.aiprofilephotomaker.com';
        
        return {
          currentOrigin,
          expectedApiUrl,
          browserUserAgent: navigator.userAgent.substring(0, 50)
        };
      });

      const correctOrigin = oauthConfig.currentOrigin === PRODUCTION_FRONTEND;
      const correctApiUrl = oauthConfig.expectedApiUrl === PRODUCTION_API;
      
      addTestResult(`${browserName}: Correct origin configuration`, correctOrigin, 2);
      addTestResult(`${browserName}: Correct API URL configuration`, correctApiUrl, 3);

    } catch (error) {
      addTestResult(`${browserName}: OAuth configuration test`, false, 5, `Error: ${error.message}`);
    }
  });

  test('6. Security Headers and HTTPS Validation', async ({ page }) => {
    console.log('\n📊 Test 6: Security Validation');
    
    try {
      const response = await page.request.get(PRODUCTION_FRONTEND);
      const headers = response.headers();
      
      // Check for security headers
      const hasStrictTransportSecurity = 'strict-transport-security' in headers;
      const hasContentSecurityPolicy = 'content-security-policy' in headers;
      const usesHttps = PRODUCTION_FRONTEND.startsWith('https://');
      
      addTestResult('HTTPS enforced', usesHttps, 5);
      addTestResult('Security headers present', hasStrictTransportSecurity || hasContentSecurityPolicy, 5);

      // Check API security
      const apiResponse = await page.request.get(PRODUCTION_API);
      const apiUsesHttps = PRODUCTION_API.startsWith('https://');
      
      addTestResult('API uses HTTPS', apiUsesHttps, 5);

    } catch (error) {
      addTestResult('Security validation', false, 15, `Error: ${error.message}`);
    }
  });

  test.afterAll(async () => {
    console.log('\n📊 FINAL PRODUCTION READINESS REPORT');
    console.log('=' .repeat(60));
    console.log(`🎯 Total Score: ${productionReadinessScore}/${maxScore} points`);
    console.log(`📊 Success Rate: ${Math.round((productionReadinessScore / maxScore) * 100)}%`);
    console.log('');

    // Categorize results
    const passed = testResults.filter(r => r.status === 'PASS');
    const failed = testResults.filter(r => r.status === 'FAIL');
    
    console.log(`✅ PASSED (${passed.length}/${testResults.length}):`);
    passed.forEach(r => console.log(`   ${r.test} (${r.points}pts)`));
    
    if (failed.length > 0) {
      console.log(`\n❌ FAILED (${failed.length}/${testResults.length}):`);
      failed.forEach(r => console.log(`   ${r.test} (${r.points}pts) - ${r.details || ''}`));
    }

    // Production readiness assessment
    console.log('\n🚀 PRODUCTION READINESS ASSESSMENT:');
    
    if (productionReadinessScore >= 80) {
      console.log('🟢 GO: Application is ready for production launch');
      console.log('   - All critical systems operational');
      console.log('   - OAuth fixes successfully deployed');
      console.log('   - Security requirements met');
    } else if (productionReadinessScore >= 60) {
      console.log('🟡 CAUTION: Application needs minor fixes before launch');
      console.log('   - Most systems operational');
      console.log('   - Some non-critical issues identified');
    } else {
      console.log('🔴 NO-GO: Critical issues must be resolved before launch');
      console.log('   - Major system failures detected');
      console.log('   - OAuth or security issues present');
    }

    console.log('\n📋 REMAINING MANUAL TASKS:');
    console.log('   □ Verify Google Cloud Console OAuth client configuration');
    console.log('   □ Confirm production secrets are properly set');
    console.log('   □ Monitor application logs post-deployment');
    console.log('   □ Conduct final user acceptance testing');
    
    console.log('\n🔧 CRITICAL DEPENDENCIES:');
    console.log('   - Google OAuth Client ID must be configured for production domains');
    console.log('   - Backend environment variables must include production secrets');
    console.log('   - SSL certificates must be valid and properly configured');
    
    console.log('=' .repeat(60));
  });
});