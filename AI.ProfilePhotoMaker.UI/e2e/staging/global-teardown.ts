import { FullConfig } from '@playwright/test';

async function globalTeardown(config: FullConfig) {
  console.log('🏁 Staging validation tests completed');
  console.log('📊 Check the HTML report for detailed results');
  
  // Add any cleanup logic here if needed
  
  return Promise.resolve();
}

export default globalTeardown;