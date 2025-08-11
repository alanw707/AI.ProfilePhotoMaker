#!/usr/bin/env node

/**
 * AI Profile Photo Maker - Deployment Validation Script
 * 
 * Validates that custom domains are working correctly after deployment
 * using Playwright for real browser testing (not just curl)
 * 
 * Usage:
 *   node scripts/validate-deployment.js
 *   node scripts/validate-deployment.js --domains app.aiprofilephotomaker.com,api.aiprofilephotomaker.com
 */

const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

// Configuration
const DEFAULT_DOMAINS = {
  frontend: 'https://app.aiprofilephotomaker.com',
  backend: 'https://api.aiprofilephotomaker.com'
};

const VALIDATION_TESTS = [
  {
    name: 'Frontend Domain Accessibility',
    type: 'navigation',
    url: DEFAULT_DOMAINS.frontend,
    expectedTitle: 'AI Profile Photo Maker',
    timeout: 30000
  },
  {
    name: 'Backend Health Check',
    type: 'api',
    url: `${DEFAULT_DOMAINS.backend}/api/health`,
    expectedStatus: 200,
    expectedContent: 'Healthy'
  },
  {
    name: 'CORS Functionality',
    type: 'cors',
    origin: DEFAULT_DOMAINS.frontend,
    endpoint: `${DEFAULT_DOMAINS.backend}/api/health`,
    expectedCredentials: true
  },
  {
    name: 'SSL Certificate Validation',
    type: 'ssl',
    domains: [DEFAULT_DOMAINS.frontend, DEFAULT_DOMAINS.backend]
  }
];

class DeploymentValidator {
  constructor(options = {}) {
    this.options = {
      headless: true,
      timeout: 30000,
      ...options
    };
    this.results = [];
    this.browser = null;
    this.context = null;
  }

  async initialize() {
    console.log('🚀 Initializing deployment validation...');
    this.browser = await chromium.launch({ 
      headless: this.options.headless 
    });
    this.context = await this.browser.newContext();
  }

  async cleanup() {
    if (this.browser) {
      await this.browser.close();
    }
  }

  async validateFrontendAccessibility(test) {
    console.log(`🧪 Testing: ${test.name}`);
    const page = await this.context.newPage();
    
    try {
      const response = await page.goto(test.url, { 
        waitUntil: 'domcontentloaded',
        timeout: test.timeout 
      });
      
      if (!response.ok()) {
        throw new Error(`HTTP ${response.status()}: ${response.statusText()}`);
      }

      const title = await page.title();
      if (!title.includes(test.expectedTitle)) {
        throw new Error(`Expected title to contain "${test.expectedTitle}", got "${title}"`);
      }

      // Check for successful API calls in console
      const logs = [];
      page.on('console', msg => {
        if (msg.type() === 'log' && msg.text().includes('Successfully loaded')) {
          logs.push(msg.text());
        }
      });

      // Wait a bit for API calls to complete
      await page.waitForTimeout(3000);

      return {
        success: true,
        status: response.status(),
        title,
        url: page.url(),
        apiLogsDetected: logs.length > 0,
        message: `✅ Frontend accessible with valid title and ${logs.length} API success logs`
      };
    } catch (error) {
      return {
        success: false,
        error: error.message,
        message: `❌ Frontend accessibility failed: ${error.message}`
      };
    } finally {
      await page.close();
    }
  }

  async validateBackendHealth(test) {
    console.log(`🧪 Testing: ${test.name}`);
    const page = await this.context.newPage();
    
    try {
      const response = await page.goto(test.url, { timeout: 10000 });
      
      if (response.status() !== test.expectedStatus) {
        throw new Error(`Expected status ${test.expectedStatus}, got ${response.status()}`);
      }

      const content = await page.textContent('body');
      const healthData = JSON.parse(content);
      
      if (!healthData.status || !healthData.status.includes(test.expectedContent)) {
        throw new Error(`Expected content containing "${test.expectedContent}", got "${healthData.status}"`);
      }

      return {
        success: true,
        status: response.status(),
        healthData,
        message: `✅ Backend health check passed: ${healthData.status} (${healthData.environment})`
      };
    } catch (error) {
      return {
        success: false,
        error: error.message,
        message: `❌ Backend health check failed: ${error.message}`
      };
    } finally {
      await page.close();
    }
  }

  async validateCORSFunctionality(test) {
    console.log(`🧪 Testing: ${test.name}`);
    const page = await this.context.newPage();
    
    try {
      // Navigate to the frontend domain first
      await page.goto(test.origin);
      
      // Execute CORS test from the frontend context
      const corsResult = await page.evaluate(async (endpoint) => {
        try {
          const response = await fetch(endpoint, {
            method: 'GET',
            headers: { 'Content-Type': 'application/json' },
            mode: 'cors',
            credentials: 'include'
          });
          
          const data = await response.json();
          
          return {
            success: true,
            status: response.status,
            url: response.url,
            data,
            origin: window.location.origin
          };
        } catch (error) {
          return {
            success: false,
            error: error.message,
            name: error.name
          };
        }
      }, test.endpoint);

      if (!corsResult.success) {
        throw new Error(`CORS request failed: ${corsResult.error}`);
      }

      return {
        success: true,
        ...corsResult,
        message: `✅ CORS working correctly from ${corsResult.origin} to API`
      };
    } catch (error) {
      return {
        success: false,
        error: error.message,
        message: `❌ CORS validation failed: ${error.message}`
      };
    } finally {
      await page.close();
    }
  }

  async validateSSLCertificates(test) {
    console.log(`🧪 Testing: ${test.name}`);
    const results = [];
    
    for (const domain of test.domains) {
      try {
        const page = await this.context.newPage();
        // For API domain, test the health endpoint specifically
        const testUrl = domain.includes('api.') ? `${domain}/api/health` : domain;
        const response = await page.goto(testUrl, { timeout: 15000 });
        
        // Check if the connection is secure
        const isSecure = page.url().startsWith('https://');
        const status = response.status();
        
        results.push({
          domain,
          secure: isSecure,
          status,
          success: isSecure && (status === 200 || (domain.includes('app.') && status >= 200 && status < 400))
        });
        
        await page.close();
      } catch (error) {
        results.push({
          domain,
          secure: false,
          error: error.message,
          success: false
        });
      }
    }
    
    const allSecure = results.every(r => r.success);
    
    return {
      success: allSecure,
      results,
      message: allSecure 
        ? `✅ All SSL certificates valid for ${results.length} domains`
        : `❌ SSL certificate issues detected`
    };
  }

  async runTest(test) {
    const startTime = Date.now();
    let result;

    try {
      switch (test.type) {
        case 'navigation':
          result = await this.validateFrontendAccessibility(test);
          break;
        case 'api':
          result = await this.validateBackendHealth(test);
          break;
        case 'cors':
          result = await this.validateCORSFunctionality(test);
          break;
        case 'ssl':
          result = await this.validateSSLCertificates(test);
          break;
        default:
          result = {
            success: false,
            error: `Unknown test type: ${test.type}`,
            message: `❌ Unknown test type: ${test.type}`
          };
      }
    } catch (error) {
      result = {
        success: false,
        error: error.message,
        message: `❌ Test execution failed: ${error.message}`
      };
    }

    const duration = Date.now() - startTime;
    
    const testResult = {
      name: test.name,
      type: test.type,
      duration,
      ...result,
      timestamp: new Date().toISOString()
    };

    this.results.push(testResult);
    console.log(`   ${testResult.message} (${duration}ms)`);
    
    return testResult;
  }

  async validateDeployment() {
    console.log('🔍 Starting deployment validation...');
    console.log(`📋 Running ${VALIDATION_TESTS.length} validation tests...\n`);

    for (const test of VALIDATION_TESTS) {
      await this.runTest(test);
    }

    return this.generateReport();
  }

  generateReport() {
    const totalTests = this.results.length;
    const passedTests = this.results.filter(r => r.success).length;
    const failedTests = totalTests - passedTests;
    const totalDuration = this.results.reduce((sum, r) => sum + r.duration, 0);

    const report = {
      summary: {
        total: totalTests,
        passed: passedTests,
        failed: failedTests,
        successRate: Math.round((passedTests / totalTests) * 100),
        totalDuration
      },
      tests: this.results,
      timestamp: new Date().toISOString(),
      status: failedTests === 0 ? 'PASS' : 'FAIL'
    };

    // Console output
    console.log('\n🎯 Deployment Validation Results:');
    console.log('═'.repeat(50));
    console.log(`📊 Tests: ${passedTests}/${totalTests} passed (${report.summary.successRate}%)`);
    console.log(`⏱️  Duration: ${totalDuration}ms`);
    console.log(`🎭 Status: ${report.status === 'PASS' ? '✅ DEPLOYMENT VALID' : '❌ DEPLOYMENT ISSUES'}`);

    if (failedTests > 0) {
      console.log('\n❌ Failed Tests:');
      this.results
        .filter(r => !r.success)
        .forEach(test => {
          console.log(`   • ${test.name}: ${test.error || 'Unknown error'}`);
        });
    }

    // Save detailed report in the scripts directory
    const reportPath = path.join(__dirname, 'validation-report.json');
    fs.writeFileSync(reportPath, JSON.stringify(report, null, 2));
    console.log(`\n📄 Detailed report saved to: ${reportPath}`);

    return report;
  }
}

// CLI execution
async function main() {
  const validator = new DeploymentValidator();
  
  try {
    await validator.initialize();
    const report = await validator.validateDeployment();
    
    // Exit with error code if validation failed
    process.exit(report.status === 'PASS' ? 0 : 1);
  } catch (error) {
    console.error('💥 Validation failed:', error.message);
    process.exit(1);
  } finally {
    await validator.cleanup();
  }
}

// Run if this file is executed directly
if (require.main === module) {
  main();
}

module.exports = { DeploymentValidator, VALIDATION_TESTS };