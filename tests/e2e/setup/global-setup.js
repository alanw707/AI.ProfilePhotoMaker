/**
 * Global Setup for E2E Tests
 * 
 * Performs environment validation and setup before running tests
 */

const { chromium } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

async function globalSetup(config) {
  console.log('🚀 Starting E2E Test Global Setup...');
  
  // 1. Validate required files exist
  console.log('📁 Validating test files...');
  await validateTestFiles();
  
  // 2. Validate target environment health
  console.log('🏥 Validating target environment...');
  await validateEnvironmentHealth(config);
  
  // 3. Create test artifacts directory
  console.log('📂 Setting up test artifacts directory...');
  await setupArtifactsDirectory();
  
  // 4. Log test configuration
  console.log('⚙️ Logging test configuration...');
  await logTestConfiguration(config);
  
  console.log('✅ Global setup completed successfully!');
}

/**
 * Validates that required test files exist
 */
async function validateTestFiles() {
  const testImagePath = path.join(__dirname, '..', 'test-images', 'sample-selfie.jpg');
  
  if (!fs.existsSync(testImagePath)) {
    console.warn('⚠️ Test image not found. Creating placeholder instructions...');
    
    const instructionsPath = path.join(path.dirname(testImagePath), 'SETUP-REQUIRED.md');
    const instructions = `# Test Image Setup Required

The E2E tests require a test image at: ${testImagePath}

## Quick Setup
1. Download a placeholder image:
   \`\`\`bash
   curl -o "${testImagePath}" "https://via.placeholder.com/400x400/4A90E2/FFFFFF?text=Test+Image"
   \`\`\`

2. Or create manually:
   - Size: 400x400 pixels
   - Format: JPEG
   - Content: Any test image (not personal photos)

## Note
Tests will be skipped until this file exists.
`;
    
    fs.writeFileSync(instructionsPath, instructions);
    console.log(`📝 Setup instructions written to: ${instructionsPath}`);
  } else {
    console.log('✅ Test image found');
  }
}

/**
 * Validates the target environment is healthy
 */
async function validateEnvironmentHealth(config) {
  const baseURL =
    process.env.TEST_BASE_URL ||
    config.use?.baseURL ||
    config.projects?.find(p => p?.use?.baseURL)?.use?.baseURL ||
    'https://app.aiprofilephotomaker.com';
  const apiBaseURL = resolveApiBaseUrl(baseURL);
  
  try {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    
    console.log(`🌐 Testing connectivity to: ${baseURL}`);
    
    // Test basic connectivity
    const response = await page.goto(baseURL, { waitUntil: 'networkidle', timeout: 30000 });
    if (!response.ok()) {
      throw new Error(`Environment not accessible: ${response.status()}`);
    }
    
    console.log('✅ Environment is accessible');
    
    // Test API health endpoint (API is hosted on a separate origin in prod/docker-local)
    try {
      const healthResponse = await page.request.get(`${apiBaseURL}/api/health`);
      if (healthResponse.ok()) {
        const healthData = await healthResponse.json();
        console.log(`🏥 API Health: ${healthData.status || 'Healthy'}`);
        
        // Test storage health specifically
        const storageHealthResponse = await page.request.get(`${apiBaseURL}/api/health/storage`);
        if (storageHealthResponse.ok()) {
          const storageData = await storageHealthResponse.json();
          console.log(`💾 Storage Health: ${storageData.status} (${storageData.provider})`);
          
          // Warn if production is using LocalStorage
          if (apiBaseURL.includes('api.aiprofilephotomaker.com') && storageData.provider === 'LocalStorage') {
            console.warn('⚠️ WARNING: Production environment using LocalStorage - expect image upload failures');
          }
        }
      }
    } catch (apiError) {
      console.warn('⚠️ API health check failed:', apiError.message);
    }
    
    await browser.close();
    
  } catch (error) {
    console.error('❌ Environment validation failed:', error.message);
    throw new Error(`Environment setup failed: ${error.message}`);
  }
}

function resolveApiBaseUrl(appBaseUrl) {
  const explicit = process.env.TEST_API_BASE_URL || process.env.API_BASE_URL;
  if (explicit) {
    return explicit.replace(/\/+$/, '');
  }

  // Local docker/dev convention.
  if (appBaseUrl.includes('localhost:4200') || appBaseUrl.includes('127.0.0.1:4200')) {
    return 'http://localhost:5032';
  }

  // Production convention.
  if (appBaseUrl.includes('app.aiprofilephotomaker.com') || appBaseUrl === 'https://aiprofilephotomaker.com') {
    return 'https://api.aiprofilephotomaker.com';
  }

  // Staging convention (best-effort).
  if (appBaseUrl.includes('staging-app.aiprofilephotomaker.com')) {
    return 'https://staging-api.aiprofilephotomaker.com';
  }

  // If appBaseUrl is already an API host, use it.
  if (appBaseUrl.includes('api.aiprofilephotomaker.com')) {
    return appBaseUrl.replace(/\/+$/, '');
  }

  // Default to production API.
  return 'https://api.aiprofilephotomaker.com';
}

/**
 * Sets up the test artifacts directory
 */
async function setupArtifactsDirectory() {
  const artifactsDir = path.join(__dirname, '..', '..', '..', 'test-results');
  
  // Create directories if they don't exist
  const dirs = [
    artifactsDir,
    path.join(artifactsDir, 'artifacts'),
    path.join(artifactsDir, 'html-report'),
    path.join(artifactsDir, 'screenshots'),
    path.join(artifactsDir, 'videos'),
    path.join(artifactsDir, 'traces')
  ];
  
  dirs.forEach(dir => {
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
      console.log(`📁 Created directory: ${dir}`);
    }
  });
  
  // Create a test run metadata file
  const metadata = {
    testRunId: `run-${Date.now()}`,
    startTime: new Date().toISOString(),
    environment: process.env.NODE_ENV || 'test',
    baseURL: process.env.TEST_BASE_URL || 'https://app.aiprofilephotomaker.com',
    playwright: {
      version: require('@playwright/test/package.json').version
    }
  };
  
  fs.writeFileSync(
    path.join(artifactsDir, 'test-run-metadata.json'),
    JSON.stringify(metadata, null, 2)
  );
  
  console.log('📊 Test run metadata created');
}

/**
 * Logs the test configuration for debugging
 */
async function logTestConfiguration(config) {
  const configSummary = {
    projects: config.projects?.map(p => ({
      name: p.name,
      baseURL: p.use?.baseURL,
      timeout: p.timeout
    })) || [],
    globalTimeout: config.timeout,
    retries: config.retries,
    workers: config.workers,
    reporter: config.reporter?.map(r => Array.isArray(r) ? r[0] : r) || []
  };
  
  console.log('⚙️ Test Configuration:');
  console.log(JSON.stringify(configSummary, null, 2));
}

module.exports = globalSetup;
