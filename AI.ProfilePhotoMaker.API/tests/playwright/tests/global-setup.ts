import { chromium, FullConfig } from '@playwright/test';
import { loadAzureConfig, validateAzureConfig, generateConfigReport } from './azure-config';

async function globalSetup(config: FullConfig) {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  
  console.log('🔧 Running global test setup...');
  
  // Load and validate Azure configuration
  console.log('🔍 Loading Azure Storage configuration...');
  const azureConfig = loadAzureConfig();
  const validation = validateAzureConfig(azureConfig);
  const report = generateConfigReport(azureConfig);
  
  console.log(report);
  
  if (validation.warnings.length > 0) {
    console.warn('⚠️  Configuration warnings detected:');
    validation.warnings.forEach(warning => console.warn(`  - ${warning}`));
  }
  
  if (validation.recommendations.length > 0) {
    console.log('💡 Configuration recommendations:');
    validation.recommendations.forEach(rec => console.log(`  - ${rec}`));
  }
  
  // Test connectivity to the frontend application
  try {
    console.log('🌐 Testing frontend connectivity...');
    await page.goto('https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io', {
      waitUntil: 'networkidle',
      timeout: 30000
    });
    console.log('✅ Frontend application is accessible');
  } catch (error) {
    console.error('❌ Frontend application is not accessible:', error);
    throw error;
  }
  
  // Test connectivity to Azure Blob Storage
  try {
    console.log('🗄️  Testing Azure Blob Storage connectivity...');
    const testUrl = `${azureConfig.baseUrl}/corporate.jpg`;
    const response = await page.request.get(testUrl, {
      timeout: 10000
    });
    console.log(`📊 Azure Blob Storage response status: ${response.status()}`);
    
    if (response.status() === 200) {
      console.log('✅ Azure Storage is accessible with images available');
    } else if (response.status() === 404) {
      console.log('⚠️  Azure Storage is accessible but images not uploaded (404 expected)');
    } else {
      console.log(`ℹ️  Azure Storage returned status ${response.status()}`);
    }
  } catch (error) {
    console.warn('⚠️  Azure Blob Storage test failed (may be expected):', error);
  }
  
  await browser.close();
  console.log('✅ Global setup completed');
  
  // Log test mode summary
  console.log(`\n🎯 Test Mode: ${azureConfig.testMode}`);
  console.log(`📋 Enabled Tests: ${[
    azureConfig.performanceTests && 'Performance',
    azureConfig.visualTests && 'Visual',
    azureConfig.uploadTests && 'Upload'
  ].filter(Boolean).join(', ') || 'Basic validation only'}`);
}

export default globalSetup;