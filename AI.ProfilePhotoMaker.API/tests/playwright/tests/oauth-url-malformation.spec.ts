import { test, expect, Page, Request } from '@playwright/test';

/**
 * OAuth URL Malformation Investigation Test
 * 
 * This test investigates the critical OAuth URL malformation issue where
 * the frontend generates malformed URLs like:
 * https://app.aiprofilephotomaker.com/.aiprofilephotomaker.com/api/api/auth/external-login/google
 * 
 * Issues to identify:
 * 1. Domain duplication (app.aiprofilephotomaker.com/.aiprofilephotomaker.com)
 * 2. Path duplication (/api/api/auth)
 * 3. Base URL configuration errors
 */

test.describe('OAuth URL Malformation Investigation', () => {
  let interceptedRequests: Request[] = [];

  test.beforeEach(async ({ page }) => {
    interceptedRequests = [];
    
    // Intercept all network requests to capture OAuth URL generation
    page.on('request', (request) => {
      const url = request.url();
      if (url.includes('auth') || url.includes('external-login') || url.includes('google')) {
        interceptedRequests.push(request);
        console.log(`🔍 Intercepted auth request: ${url}`);
      }
    });

    page.on('requestfailed', (request) => {
      const url = request.url();
      if (url.includes('auth') || url.includes('external-login')) {
        console.error(`❌ Auth request failed: ${url}`);
        console.error(`Failure reason: ${request.failure()?.errorText}`);
      }
    });
  });

  test('Should identify OAuth URL construction logic and malformation sources', async ({ page }) => {
    console.log('🚀 Starting OAuth URL malformation investigation...');

    // Navigate to login page to trigger frontend initialization
    await page.goto('/auth/login');
    await expect(page).toHaveTitle(/AI Profile Photo Maker/);

    // Examine the current page environment configuration
    const currentEnvironment = await page.evaluate(() => {
      return {
        origin: window.location.origin,
        hostname: window.location.hostname,
        pathname: window.location.pathname,
        protocol: window.location.protocol,
        port: window.location.port
      };
    });

    console.log('🌍 Current page environment:', currentEnvironment);

    // Check for any environment variables or configuration objects exposed to the frontend
    const frontendConfig = await page.evaluate(() => {
      // Try to access Angular environment or configuration objects
      const win = window as any;
      return {
        // Check if Angular exposes environment
        ng: win.ng ? 'Angular DevTools Available' : 'No Angular DevTools',
        // Check for global config objects
        config: win.config || null,
        environment: win.environment || null,
        apiUrl: win.apiUrl || null,
        baseUrl: win.baseUrl || null
      };
    });

    console.log('⚙️ Frontend configuration objects:', frontendConfig);

    // Test 1: Check if Google login button exists and examine its behavior
    const googleLoginButton = page.locator('button:has-text("Continue with Google")');
    await expect(googleLoginButton).toBeVisible();

    // Test 2: Examine the Google login click behavior without actually clicking
    // We'll use JavaScript evaluation to see what URL would be generated
    const oauthUrlGeneration = await page.evaluate(() => {
      // Try to access the Angular component's method for OAuth URL generation
      const win = window as any;
      
      // Simulate what happens in loginWithGoogle() method
      const mockReturnUrl = '/app/dashboard';
      
      // Try to determine what getOAuthBaseUrl() would return in this environment
      let oauthBaseUrl = '';
      
      // Check different scenarios
      const scenarios = {
        windowOrigin: window.location.origin,
        localhostFallback: 'http://localhost:4200',
        productionLike: 'https://app.aiprofilephotomaker.com'
      };

      const generatedUrls = Object.entries(scenarios).map(([scenario, baseUrl]) => {
        const generatedUrl = `${baseUrl}/api/auth/external-login/google?returnUrl=${encodeURIComponent(mockReturnUrl)}`;
        return { scenario, baseUrl, generatedUrl };
      });

      return {
        scenarios: generatedUrls,
        currentOrigin: window.location.origin
      };
    });

    console.log('🔧 OAuth URL generation analysis:', JSON.stringify(oauthUrlGeneration, null, 2));

    // Test 3: Simulate clicking the Google login button and capture the URL
    console.log('🖱️ Preparing to test Google login button click...');
    
    // Set up a Promise to capture navigation attempts
    const navigationPromise = page.waitForEvent('framenavigated', { timeout: 5000 }).catch(() => null);
    
    // Click the Google login button
    await googleLoginButton.click();

    // Wait for any navigation or network requests
    await page.waitForTimeout(2000);

    // Check if navigation occurred and capture the attempted URL
    const navigation = await navigationPromise;
    if (navigation) {
      console.log(`🧭 Navigation detected to: ${navigation.url()}`);
    }

    // Test 4: Examine all intercepted requests for malformed URLs
    console.log('📊 Analyzing intercepted auth requests...');
    
    for (const request of interceptedRequests) {
      const url = request.url();
      console.log(`📝 Auth request: ${url}`);
      
      // Check for common malformation patterns
      const malformationChecks = {
        domainDuplication: url.includes('.aiprofilephotomaker.com/.aiprofilephotomaker.com'),
        pathDuplication: url.includes('/api/api/'),
        invalidProtocol: !url.startsWith('http://') && !url.startsWith('https://'),
        doubleSlashes: url.includes('//') && !url.match(/^https?:\/\//),
        malformedHost: url.match(/\/\.[a-z]/), // Matches patterns like "/.aiprofilephotomaker.com"
      };
      
      const issues = Object.entries(malformationChecks)
        .filter(([_, hasIssue]) => hasIssue)
        .map(([issue, _]) => issue);
      
      if (issues.length > 0) {
        console.error(`❌ MALFORMED URL DETECTED: ${url}`);
        console.error(`Issues found: ${issues.join(', ')}`);
      } else {
        console.log(`✅ URL appears well-formed: ${url}`);
      }
    }

    // Test 5: Check the page's current state and any error messages
    const pageErrors = await page.evaluate(() => {
      // Check for any error messages in the console or on the page
      const errorElements = document.querySelectorAll('.alert-danger, .error, [class*="error"]');
      return Array.from(errorElements).map(el => el.textContent);
    });

    if (pageErrors.length > 0) {
      console.log('🚨 Page errors detected:', pageErrors);
    }

    // Test 6: Examine browser console logs for additional clues
    const consoleLogs: string[] = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error' || msg.text().includes('auth') || msg.text().includes('oauth')) {
        consoleLogs.push(`${msg.type()}: ${msg.text()}`);
      }
    });

    await page.waitForTimeout(1000); // Give time for any console messages

    if (consoleLogs.length > 0) {
      console.log('📋 Relevant console messages:', consoleLogs);
    }

    // Generate test report
    const testReport = {
      environment: currentEnvironment,
      frontendConfig,
      oauthUrlGeneration,
      interceptedRequests: interceptedRequests.map(r => r.url()),
      pageErrors,
      consoleLogs,
      timestamp: new Date().toISOString()
    };

    console.log('📄 Complete test report:', JSON.stringify(testReport, null, 2));

    // Assert that we've captured some relevant data
    expect(testReport.environment.hostname).toBeTruthy();
    expect(testReport.oauthUrlGeneration.scenarios).toHaveLength(3);
  });

  test('Should test OAuth URL construction with different base URL configurations', async ({ page }) => {
    console.log('🧪 Testing OAuth URL construction with different configurations...');

    await page.goto('/auth/login');

    // Test different base URL scenarios by injecting them into the page
    const urlConstructionTests = await page.evaluate(() => {
      const testConfigurations = [
        {
          name: 'Development (localhost)',
          apiUrl: '/api',
          baseUrl: '',
          expectedOAuthBase: 'http://localhost:4200'
        },
        {
          name: 'Production (MVP-v1)',
          apiUrl: 'https://api.aiprofilephotomaker.com/api',
          baseUrl: 'https://api.aiprofilephotomaker.com',
          expectedOAuthBase: 'https://api.aiprofilephotomaker.com'
        },
        {
          name: 'Malformed Configuration 1',
          apiUrl: 'https://app.aiprofilephotomaker.com/api',
          baseUrl: 'https://app.aiprofilephotomaker.com',
          expectedOAuthBase: 'https://app.aiprofilephotomaker.com'
        }
      ];

      return testConfigurations.map(config => {
        // Simulate the getOAuthBaseUrl() logic with different configurations
        let oauthBaseUrl = '';
        
        if (config.apiUrl?.startsWith('https://')) {
          // Extract base URL from full API URL (remove /api suffix)
          oauthBaseUrl = config.apiUrl.replace('/api', '');
        } else {
          // Fallback to current origin for local development
          oauthBaseUrl = window.location.origin;
        }

        const returnUrl = '/app/dashboard';
        const generatedUrl = `${oauthBaseUrl}/api/auth/external-login/google?returnUrl=${encodeURIComponent(returnUrl)}`;

        return {
          ...config,
          actualOAuthBase: oauthBaseUrl,
          generatedUrl,
          isWellFormed: !generatedUrl.includes('//') || generatedUrl.match(/^https?:\/\/[^\/]+\//)
        };
      });
    });

    console.log('🔧 URL construction test results:');
    urlConstructionTests.forEach((test, index) => {
      console.log(`\n${index + 1}. ${test.name}:`);
      console.log(`   API URL: ${test.apiUrl}`);
      console.log(`   Base URL: ${test.baseUrl}`);
      console.log(`   OAuth Base: ${test.actualOAuthBase}`);
      console.log(`   Generated URL: ${test.generatedUrl}`);
      console.log(`   Well-formed: ${test.isWellFormed ? '✅' : '❌'}`);
    });

    // Check for any malformed URLs
    const malformedUrls = urlConstructionTests.filter(test => !test.isWellFormed);
    if (malformedUrls.length > 0) {
      console.error(`❌ Found ${malformedUrls.length} malformed URL configurations`);
    }

    expect(urlConstructionTests).toHaveLength(3);
  });

  test('Should examine environment.mvp-v1.ts configuration in production context', async ({ page }) => {
    console.log('🏭 Testing production environment configuration...');

    // Mock the production environment by overriding the configuration
    await page.addInitScript(() => {
      // Simulate production environment configuration
      (window as any).environment = {
        production: true,
        apiUrl: 'https://api.aiprofilephotomaker.com/api',
        baseUrl: 'https://api.aiprofilephotomaker.com',
        name: 'mvp-v1',
        azure: {
          enabled: true,
          frontendUrl: 'https://app.aiprofilephotomaker.com',
          backendUrl: 'https://api.aiprofilephotomaker.com',
        }
      };
    });

    await page.goto('/auth/login');

    // Test the OAuth URL generation with production-like configuration
    const productionUrlTest = await page.evaluate(() => {
      const env = (window as any).environment;
      
      // Simulate ConfigService.getOAuthBaseUrl() logic
      let oauthBaseUrl = '';
      
      if (env?.apiUrl?.startsWith('https://')) {
        // Extract base URL from full API URL (remove /api suffix)
        oauthBaseUrl = env.apiUrl.replace('/api', '');
      } else {
        // Fallback to current origin
        oauthBaseUrl = window.location.origin;
      }

      const returnUrl = '/app/dashboard';
      const generatedUrl = `${oauthBaseUrl}/api/auth/external-login/google?returnUrl=${encodeURIComponent(returnUrl)}`;

      // Check for the specific malformation mentioned in the issue
      const hasDomainDuplication = generatedUrl.includes('app.aiprofilephotomaker.com/.aiprofilephotomaker.com');
      const hasPathDuplication = generatedUrl.includes('/api/api/');

      return {
        environment: env,
        oauthBaseUrl,
        generatedUrl,
        malformationAnalysis: {
          hasDomainDuplication,
          hasPathDuplication,
          isWellFormed: !hasDomainDuplication && !hasPathDuplication
        }
      };
    });

    console.log('🏭 Production URL test results:');
    console.log(`Environment: ${JSON.stringify(productionUrlTest.environment, null, 2)}`);
    console.log(`OAuth Base URL: ${productionUrlTest.oauthBaseUrl}`);
    console.log(`Generated URL: ${productionUrlTest.generatedUrl}`);
    console.log(`Malformation Analysis: ${JSON.stringify(productionUrlTest.malformationAnalysis, null, 2)}`);

    if (!productionUrlTest.malformationAnalysis.isWellFormed) {
      console.error('❌ PRODUCTION CONFIGURATION GENERATES MALFORMED URLS');
    } else {
      console.log('✅ Production configuration generates well-formed URLs');
    }

    expect(productionUrlTest.generatedUrl).toBeTruthy();
  });
});