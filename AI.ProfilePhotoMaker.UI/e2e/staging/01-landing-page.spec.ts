import { test, expect } from '@playwright/test';
import { StagingTestHelpers, waitForStableLoad } from './utils/test-helpers';

test.describe('Landing Page Functionality - Staging Environment', () => {
  let helpers: StagingTestHelpers;

  test.beforeEach(async ({ page }) => {
    helpers = new StagingTestHelpers(page);
    
    // Start monitoring
    await helpers.captureNetworkMetrics();
    await helpers.captureConsoleErrors();
  });

  test('should load homepage without critical errors', async ({ page }) => {
    console.log('🏠 Testing homepage load...');
    
    const startTime = Date.now();
    await page.goto('/');
    await waitForStableLoad(page);
    const loadTime = Date.now() - startTime;
    
    // Verify page loaded successfully
    await expect(page).toHaveTitle(/AI Profile Photo Maker/);
    
    // Check load time is reasonable (less than 5 seconds)
    expect(loadTime).toBeLessThan(5000);
    console.log(`⏱️ Page load time: ${loadTime}ms`);
    
    // Verify critical elements are present
    await expect(page.locator('h1')).toContainText('AI Profile Photo Maker');
    await expect(page.locator('.hero-section, .main-content')).toBeVisible();
    
    // Take screenshot for evidence
    await page.screenshot({ path: 'screenshots/01-homepage-loaded.png', fullPage: true });
  });

  test('should display style preview images from Azure Blob Storage', async ({ page }) => {
    console.log('🎨 Testing style preview images...');
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait for styles section to load
    await page.waitForSelector('.styled-photos-grid, .style-showcase, .styles-section', { timeout: 15000 });
    
    const imageMetrics = await helpers.verifyStylePreviewImages();
    
    console.log('📊 Style Image Metrics:', imageMetrics);
    
    // Verify we have style images
    expect(imageMetrics.total).toBeGreaterThan(0);
    
    // Critical: Verify images are hosted on Azure Blob Storage (not placeholders)
    expect(imageMetrics.azureHosted).toBeGreaterThan(0);
    console.log(`✅ ${imageMetrics.azureHosted} images hosted on Azure Blob Storage`);
    
    // Alert if too many placeholders
    if (imageMetrics.placeholders > imageMetrics.total * 0.2) {
      console.warn(`⚠️ High placeholder count: ${imageMetrics.placeholders}/${imageMetrics.total}`);
    }
    
    // Verify most images loaded successfully
    expect(imageMetrics.loadErrors).toBeLessThan(imageMetrics.total * 0.1);
    
    // Take screenshot of styles section
    await page.screenshot({ path: 'screenshots/02-style-previews.png', fullPage: true });
  });

  test('should verify Azure Blob Storage integration', async ({ page }) => {
    console.log('☁️ Testing Azure Blob Storage integration...');
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    const storageMetrics = await helpers.verifyAzureBlobStorageUsage();
    
    console.log('📊 Azure Storage Metrics:');
    console.log(`  Azure URLs: ${storageMetrics.azureUrls.length}`);
    console.log(`  Non-Azure URLs: ${storageMetrics.nonAzureUrls.length}`);
    
    // Verify Azure Blob Storage is being used
    expect(storageMetrics.azureUrls.length).toBeGreaterThan(0);
    
    // Log URLs for debugging
    if (storageMetrics.azureUrls.length > 0) {
      console.log('✅ Azure Blob Storage URLs found:');
      storageMetrics.azureUrls.slice(0, 3).forEach(url => console.log(`  ${url}`));
    }
    
    if (storageMetrics.nonAzureUrls.length > 0) {
      console.log('ℹ️ Non-Azure image URLs:');
      storageMetrics.nonAzureUrls.slice(0, 3).forEach(url => console.log(`  ${url}`));
    }
  });

  test('should load credit packages without errors', async ({ page }) => {
    console.log('💳 Testing credit packages...');
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Scroll to pricing section or look for packages
    const pricingSection = page.locator('.pricing-section, .plans-section, .packages-section');
    if (await pricingSection.count() > 0) {
      await pricingSection.scrollIntoViewIfNeeded();
      
      // Wait for packages to load
      await page.waitForSelector('.plan-card, .package-card, .pricing-card', { timeout: 10000 });
      
      const packageCards = page.locator('.plan-card, .package-card, .pricing-card');
      const packageCount = await packageCards.count();
      
      console.log(`📦 Found ${packageCount} credit packages`);
      expect(packageCount).toBeGreaterThan(0);
      
      // Verify package details are visible
      for (let i = 0; i < Math.min(packageCount, 3); i++) {
        const card = packageCards.nth(i);
        await expect(card.locator('.price, .plan-price')).toBeVisible();
        await expect(card.locator('.features, .plan-features')).toBeVisible();
      }
      
      await page.screenshot({ path: 'screenshots/03-credit-packages.png', fullPage: true });
    } else {
      console.log('ℹ️ No pricing section found on landing page');
    }
  });

  test('should validate API endpoints are working', async ({ page }) => {
    console.log('🔌 Testing API endpoints...');
    
    const apiMetrics = await helpers.verifyApiEndpoints();
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait a bit more for any API calls to complete
    await page.waitForTimeout(3000);
    
    console.log('📊 API Metrics:');
    console.log(`  Working endpoints: ${apiMetrics.workingEndpoints.length}`);
    console.log(`  Failed endpoints: ${apiMetrics.failedEndpoints.length}`);
    
    if (apiMetrics.workingEndpoints.length > 0) {
      console.log('✅ Working API endpoints:');
      apiMetrics.workingEndpoints.slice(0, 5).forEach(endpoint => console.log(`  ${endpoint}`));
    }
    
    if (apiMetrics.failedEndpoints.length > 0) {
      console.log('❌ Failed API endpoints:');
      apiMetrics.failedEndpoints.forEach(endpoint => console.log(`  ${endpoint}`));
    }
    
    // Allow some API failures but ensure not all are failing
    if (apiMetrics.workingEndpoints.length + apiMetrics.failedEndpoints.length > 0) {
      const successRate = apiMetrics.workingEndpoints.length / 
        (apiMetrics.workingEndpoints.length + apiMetrics.failedEndpoints.length);
      expect(successRate).toBeGreaterThan(0.7); // 70% success rate minimum
    }
  });

  test('should verify no critical console errors', async ({ page }) => {
    console.log('🐛 Testing for console errors...');
    
    const criticalErrors = await helpers.verifyCriticalErrors();
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait for any lazy-loaded content
    await page.waitForTimeout(2000);
    
    console.log(`📊 Critical errors found: ${criticalErrors.length}`);
    
    if (criticalErrors.length > 0) {
      console.log('❌ Critical console errors:');
      criticalErrors.forEach(error => console.log(`  ${error}`));
    }
    
    // Allow some minor errors but fail on critical ones
    const severeCriticalErrors = criticalErrors.filter(error => 
      error.includes('TypeError') || 
      error.includes('ReferenceError') ||
      error.includes('Failed to fetch') ||
      error.includes('404') ||
      error.includes('500')
    );
    
    expect(severeCriticalErrors.length).toBe(0);
  });

  test('should verify responsive design works', async ({ page }) => {
    console.log('📱 Testing responsive design...');
    
    // Test desktop
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    await waitForStableLoad(page);
    await page.screenshot({ path: 'screenshots/04-desktop-view.png', fullPage: true });
    
    // Test tablet
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.reload();
    await waitForStableLoad(page);
    await page.screenshot({ path: 'screenshots/05-tablet-view.png', fullPage: true });
    
    // Test mobile
    await page.setViewportSize({ width: 375, height: 667 });
    await page.reload();
    await waitForStableLoad(page);
    await page.screenshot({ path: 'screenshots/06-mobile-view.png', fullPage: true });
    
    // Verify key elements are still visible on mobile
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('.hero-section, .main-content')).toBeVisible();
  });

  test('should verify navigation and CTAs work', async ({ page }) => {
    console.log('🧭 Testing navigation and CTAs...');
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Test main CTA buttons
    const ctaButtons = page.locator('button:has-text("Get Started"), button:has-text("Start Creating"), a:has-text("Get Started")');
    const ctaCount = await ctaButtons.count();
    
    if (ctaCount > 0) {
      console.log(`✅ Found ${ctaCount} CTA buttons`);
      await expect(ctaButtons.first()).toBeVisible();
      
      // Verify CTA is clickable (don't actually click to avoid navigation)
      await expect(ctaButtons.first()).toBeEnabled();
    }
    
    // Test navigation menu if present
    const navMenu = page.locator('nav, .navigation, .header-menu');
    if (await navMenu.count() > 0) {
      await expect(navMenu.first()).toBeVisible();
      console.log('✅ Navigation menu found and visible');
    }
    
    await page.screenshot({ path: 'screenshots/07-navigation-elements.png', fullPage: true });
  });
});