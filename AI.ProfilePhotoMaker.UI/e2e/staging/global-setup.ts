import { chromium, FullConfig } from '@playwright/test';

async function globalSetup(config: FullConfig) {
  console.log('🚀 Starting Staging Environment Validation Suite');
  console.log(`📍 Base URL: ${config.projects[0].use?.baseURL}`);
  
  // Pre-flight checks
  const browser = await chromium.launch();
  const page = await browser.newPage();
  
  try {
    // Verify staging site is accessible
    console.log('🔍 Performing pre-flight checks...');
    await page.goto(config.projects[0].use?.baseURL || '', { timeout: 15000 });
    
    const title = await page.title();
    console.log(`✅ Site accessible: ${title}`);
    
    // Basic health check
    await page.waitForLoadState('networkidle', { timeout: 10000 });
    console.log('✅ Site loaded successfully');
    
  } catch (error) {
    console.warn('⚠️ Pre-flight check failed (staging may be temporarily unavailable):', error.message);
    console.log('ℹ️ Tests will still run but may fail if staging is not accessible');
    // Don't throw error - allow tests to run and handle failures individually
  } finally {
    await browser.close();
  }
  
  console.log('✅ Global setup completed');
}

export default globalSetup;