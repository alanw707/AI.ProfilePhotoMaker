import { test, expect } from '@playwright/test';
import { StagingTestHelpers, waitForStableLoad } from './utils/test-helpers';

test.describe('Image Loading - Azure Blob Storage Integration', () => {
  let helpers: StagingTestHelpers;

  test.beforeEach(async ({ page }) => {
    helpers = new StagingTestHelpers(page);
  });

  test('should load real images from Azure Blob Storage (not placeholders)', async ({ page }) => {
    console.log('☁️ Testing Azure Blob Storage image loading...');
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait for images to load
    await page.waitForTimeout(5000);
    
    const storageMetrics = await helpers.verifyAzureBlobStorageUsage();
    
    console.log('📊 Azure Blob Storage Analysis:');
    console.log(`  Images from Azure: ${storageMetrics.azureUrls.length}`);
    console.log(`  Images from other sources: ${storageMetrics.nonAzureUrls.length}`);
    
    // Critical test: Verify real images are loaded from Azure
    expect(storageMetrics.azureUrls.length).toBeGreaterThan(0);
    
    console.log('\n✅ Azure Blob Storage URLs (sample):');
    storageMetrics.azureUrls.slice(0, 5).forEach((url, index) => {
      console.log(`  ${index + 1}. ${url}`);
    });
    
    if (storageMetrics.nonAzureUrls.length > 0) {
      console.log('\nℹ️ Non-Azure image URLs (sample):');
      storageMetrics.nonAzureUrls.slice(0, 3).forEach((url, index) => {
        console.log(`  ${index + 1}. ${url}`);
      });
    }
    
    await page.screenshot({ path: 'screenshots/10-azure-blob-images.png', fullPage: true });
  });

  test('should verify style preview images are real photos (no placeholders)', async ({ page }) => {
    console.log('🎨 Testing style preview image quality...');
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait for style images to load
    await page.waitForSelector('.styled-photos-grid, .style-showcase, .styles-section', { timeout: 15000 });
    await page.waitForTimeout(3000);
    
    const imageAnalysis = await page.evaluate(() => {
      const styleImages = document.querySelectorAll(
        '.styled-photos-grid img, .style-showcase img, .style-card img, [class*="style"] img'
      );
      
      const analysis = {
        totalImages: styleImages.length,
        azureHosted: 0,
        placeholderImages: 0,
        loadedImages: 0,
        failedImages: 0,
        imageDetails: [] as Array<{src: string, alt: string, loaded: boolean, isPlaceholder: boolean, isAzure: boolean}>
      };
      
      styleImages.forEach(img => {
        const imgElement = img as HTMLImageElement;
        const src = imgElement.src;
        const alt = imgElement.alt || '';
        const loaded = imgElement.complete && imgElement.naturalWidth > 0;
        const isPlaceholder = src.startsWith('data:image/svg+xml') || 
                             src.includes('placeholder') || 
                             alt.includes('placeholder');
        const isAzure = src.includes('blob.core.windows.net') || 
                       src.includes('aiprofilemakerstrg3bawc74');
        
        if (loaded) analysis.loadedImages++;
        else analysis.failedImages++;
        
        if (isPlaceholder) analysis.placeholderImages++;
        if (isAzure) analysis.azureHosted++;
        
        analysis.imageDetails.push({ src, alt, loaded, isPlaceholder, isAzure });
      });
      
      return analysis;
    });
    
    console.log('📊 Style Image Analysis:');
    console.log(`  Total style images: ${imageAnalysis.totalImages}`);
    console.log(`  Loaded successfully: ${imageAnalysis.loadedImages}`);
    console.log(`  Failed to load: ${imageAnalysis.failedImages}`);
    console.log(`  Azure Blob hosted: ${imageAnalysis.azureHosted}`);
    console.log(`  Placeholder images: ${imageAnalysis.placeholderImages}`);
    
    // Critical assertions
    expect(imageAnalysis.totalImages).toBeGreaterThan(0);
    expect(imageAnalysis.azureHosted).toBeGreaterThan(0);
    
    // Alert if too many placeholders
    const placeholderPercentage = (imageAnalysis.placeholderImages / imageAnalysis.totalImages) * 100;
    console.log(`  Placeholder percentage: ${placeholderPercentage.toFixed(1)}%`);
    
    if (placeholderPercentage > 20) {
      console.warn(`⚠️ High placeholder percentage: ${placeholderPercentage.toFixed(1)}%`);
    }
    
    // Show sample real images
    const realImages = imageAnalysis.imageDetails.filter(img => img.isAzure && !img.isPlaceholder);
    if (realImages.length > 0) {
      console.log('\n✅ Real Azure images (sample):');
      realImages.slice(0, 3).forEach((img, index) => {
        console.log(`  ${index + 1}. ${img.alt || 'Style image'}: ${img.src.substring(0, 80)}...`);
      });
    }
    
    // Show placeholders if any
    const placeholders = imageAnalysis.imageDetails.filter(img => img.isPlaceholder);
    if (placeholders.length > 0) {
      console.log('\n⚠️ Placeholder images found:');
      placeholders.slice(0, 3).forEach((img, index) => {
        console.log(`  ${index + 1}. ${img.alt || 'Placeholder'}: ${img.src.substring(0, 80)}...`);
      });
    }
    
    await page.screenshot({ path: 'screenshots/11-style-image-analysis.png', fullPage: true });
  });

  test('should verify image load performance', async ({ page }) => {
    console.log('⚡ Testing image load performance...');
    
    const imageLoadTimes: Array<{url: string, loadTime: number}> = [];
    const imageStartTimes = new Map<string, number>();
    
    // Monitor image requests
    page.on('request', request => {
      const url = request.url();
      if (request.resourceType() === 'image') {
        imageStartTimes.set(url, Date.now());
      }
    });
    
    page.on('response', response => {
      const url = response.url();
      if (response.request().resourceType() === 'image') {
        const startTime = imageStartTimes.get(url);
        if (startTime) {
          const loadTime = Date.now() - startTime;
          imageLoadTimes.push({ url, loadTime });
        }
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait for images to load
    await page.waitForTimeout(5000);
    
    if (imageLoadTimes.length > 0) {
      const azureImages = imageLoadTimes.filter(img => 
        img.url.includes('blob.core.windows.net') || 
        img.url.includes('aiprofilemakerstrg3bawc74')
      );
      
      console.log('📊 Image Load Performance:');
      console.log(`  Total images loaded: ${imageLoadTimes.length}`);
      console.log(`  Azure images loaded: ${azureImages.length}`);
      
      if (azureImages.length > 0) {
        const averageLoadTime = azureImages.reduce((sum, img) => sum + img.loadTime, 0) / azureImages.length;
        const maxLoadTime = Math.max(...azureImages.map(img => img.loadTime));
        
        console.log(`  Average Azure image load time: ${averageLoadTime.toFixed(0)}ms`);
        console.log(`  Maximum Azure image load time: ${maxLoadTime}ms`);
        
        // Performance assertions
        expect(averageLoadTime).toBeLessThan(3000); // 3 seconds average
        expect(maxLoadTime).toBeLessThan(10000); // 10 seconds max
        
        // Show fastest and slowest
        const sorted = azureImages.sort((a, b) => a.loadTime - b.loadTime);
        console.log(`  Fastest load: ${sorted[0].loadTime}ms`);
        console.log(`  Slowest load: ${sorted[sorted.length - 1].loadTime}ms`);
        
        if (averageLoadTime > 1500) {
          console.warn(`⚠️ Slow image loading: ${averageLoadTime.toFixed(0)}ms average`);
        }
      }
    } else {
      console.log('ℹ️ No image load timing data collected');
    }
  });

  test('should verify image accessibility and alt text', async ({ page }) => {
    console.log('♿ Testing image accessibility...');
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    const accessibilityCheck = await page.evaluate(() => {
      const images = document.querySelectorAll('img');
      const analysis = {
        totalImages: images.length,
        withAltText: 0,
        withoutAltText: 0,
        decorativeImages: 0,
        missingAltText: [] as string[]
      };
      
      images.forEach(img => {
        const imgElement = img as HTMLImageElement;
        const alt = imgElement.alt;
        const src = imgElement.src;
        
        if (alt && alt.trim().length > 0) {
          analysis.withAltText++;
          if (alt.toLowerCase() === 'decorative' || alt === '') {
            analysis.decorativeImages++;
          }
        } else {
          analysis.withoutAltText++;
          analysis.missingAltText.push(src.substring(0, 100));
        }
      });
      
      return analysis;
    });
    
    console.log('📊 Image Accessibility Analysis:');
    console.log(`  Total images: ${accessibilityCheck.totalImages}`);
    console.log(`  With alt text: ${accessibilityCheck.withAltText}`);
    console.log(`  Without alt text: ${accessibilityCheck.withoutAltText}`);
    console.log(`  Decorative images: ${accessibilityCheck.decorativeImages}`);
    
    if (accessibilityCheck.missingAltText.length > 0) {
      console.log('\n⚠️ Images missing alt text:');
      accessibilityCheck.missingAltText.slice(0, 5).forEach((src, index) => {
        console.log(`  ${index + 1}. ${src}...`);
      });
    }
    
    // Accessibility requirements
    const altTextPercentage = (accessibilityCheck.withAltText / accessibilityCheck.totalImages) * 100;
    console.log(`  Alt text coverage: ${altTextPercentage.toFixed(1)}%`);
    
    // Most images should have alt text (allow some decorative images)
    expect(altTextPercentage).toBeGreaterThan(80);
  });

  test('should verify no broken image links', async ({ page }) => {
    console.log('🔗 Testing for broken image links...');
    
    const brokenImages: Array<{src: string, status: number, error?: string}> = [];
    
    page.on('response', response => {
      if (response.request().resourceType() === 'image' && response.status() >= 400) {
        brokenImages.push({
          src: response.url(),
          status: response.status()
        });
      }
    });
    
    page.on('requestfailed', request => {
      if (request.resourceType() === 'image') {
        brokenImages.push({
          src: request.url(),
          status: 0,
          error: request.failure()?.errorText
        });
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    
    // Wait for all images to load or fail
    await page.waitForTimeout(7000);
    
    console.log('📊 Broken Image Analysis:');
    console.log(`  Broken images found: ${brokenImages.length}`);
    
    if (brokenImages.length > 0) {
      console.log('\n❌ Broken images:');
      brokenImages.forEach((img, index) => {
        console.log(`  ${index + 1}. ${img.status} - ${img.src} ${img.error ? `(${img.error})` : ''}`);
      });
      
      // Allow a small number of broken images but fail if too many
      expect(brokenImages.length).toBeLessThan(5);
    } else {
      console.log('✅ No broken images detected');
    }
  });

  test('should verify image optimization and formats', async ({ page }) => {
    console.log('🖼️ Testing image optimization...');
    
    const imageFormats: Array<{url: string, format: string, size?: number}> = [];
    
    page.on('response', async response => {
      if (response.request().resourceType() === 'image') {
        const url = response.url();
        const contentType = response.headers()['content-type'] || '';
        const contentLength = response.headers()['content-length'];
        
        let format = 'unknown';
        if (contentType.includes('jpeg') || url.includes('.jpg') || url.includes('.jpeg')) {
          format = 'JPEG';
        } else if (contentType.includes('png') || url.includes('.png')) {
          format = 'PNG';
        } else if (contentType.includes('webp') || url.includes('.webp')) {
          format = 'WebP';
        } else if (contentType.includes('svg') || url.includes('.svg')) {
          format = 'SVG';
        } else if (url.startsWith('data:image/svg+xml')) {
          format = 'SVG (inline)';
        }
        
        imageFormats.push({
          url,
          format,
          size: contentLength ? parseInt(contentLength) : undefined
        });
      }
    });
    
    await page.goto('/');
    await waitForStableLoad(page);
    await page.waitForTimeout(5000);
    
    if (imageFormats.length > 0) {
      const formatCounts = imageFormats.reduce((acc, img) => {
        acc[img.format] = (acc[img.format] || 0) + 1;
        return acc;
      }, {} as Record<string, number>);
      
      console.log('📊 Image Format Analysis:');
      Object.entries(formatCounts).forEach(([format, count]) => {
        console.log(`  ${format}: ${count} images`);
      });
      
      // Check for modern formats
      const modernFormats = imageFormats.filter(img => 
        img.format === 'WebP' || img.format === 'SVG' || img.format === 'SVG (inline)'
      );
      
      console.log(`  Modern formats (WebP/SVG): ${modernFormats.length}/${imageFormats.length}`);
      
      // Check file sizes
      const sizesKnown = imageFormats.filter(img => img.size);
      if (sizesKnown.length > 0) {
        const averageSize = sizesKnown.reduce((sum, img) => sum + (img.size || 0), 0) / sizesKnown.length;
        const maxSize = Math.max(...sizesKnown.map(img => img.size || 0));
        
        console.log(`  Average image size: ${(averageSize / 1024).toFixed(1)}KB`);
        console.log(`  Largest image: ${(maxSize / 1024).toFixed(1)}KB`);
        
        // Alert for large images
        if (averageSize > 500 * 1024) { // 500KB
          console.warn(`⚠️ Large average image size: ${(averageSize / 1024).toFixed(1)}KB`);
        }
      }
    }
  });
});