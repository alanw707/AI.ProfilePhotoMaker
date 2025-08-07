import { test, expect } from '@playwright/test';

const STYLE_IMAGES = [
  'academic.jpg', 'artistic.jpg', 'author.jpg', 'casual.jpg', 'consultant.jpg',
  'corporate.jpg', 'creative.jpg', 'digital-nomad.jpg', 'edgy-urban.jpg',
  'entrepreneur.jpg', 'executive.jpg', 'fashion.jpg', 'fitness.jpg',
  'glamour.jpg', 'influencer.jpg', 'legal.jpg', 'linkedin.jpg',
  'medical.jpg', 'spiritual.jpg', 'startup.jpg', 'tech-professional.jpg'
];

const BASE_URL = 'https://aipmstv16j74jubocuukg.blob.core.windows.net/style-previews';

test.describe('Direct Image Validation - Post Upload', () => {
  test('should validate all style preview images return 200 and are accessible', async ({ page }) => {
    console.log('🔍 Starting direct image validation...');
    
    const results = [];
    let successCount = 0;
    let failCount = 0;

    for (const imageName of STYLE_IMAGES) {
      const imageUrl = `${BASE_URL}/${imageName}`;
      console.log(`Testing: ${imageName}`);
      
      try {
        // Test HTTP response
        const response = await page.request.get(imageUrl);
        const status = response.status();
        const contentType = response.headers()['content-type'] || '';
        const contentLength = response.headers()['content-length'] || '0';
        
        // Test image loading in browser
        await page.goto('data:text/html,<html><body></body></html>');
        const imageLoadResult = await page.evaluate(async (url) => {
          return new Promise((resolve) => {
            const img = new Image();
            const startTime = Date.now();
            
            const timeout = setTimeout(() => {
              resolve({ success: false, loadTime: 5000, error: 'Timeout' });
            }, 5000);
            
            img.onload = () => {
              clearTimeout(timeout);
              resolve({
                success: true,
                loadTime: Date.now() - startTime,
                width: img.naturalWidth,
                height: img.naturalHeight
              });
            };
            
            img.onerror = () => {
              clearTimeout(timeout);
              resolve({ success: false, loadTime: Date.now() - startTime, error: 'Load error' });
            };
            
            img.src = url;
          });
        }, imageUrl);
        
        const result = {
          imageName,
          httpStatus: status,
          contentType,
          contentLength: parseInt(contentLength),
          imageLoaded: imageLoadResult.success,
          loadTime: imageLoadResult.loadTime,
          dimensions: imageLoadResult.success ? `${imageLoadResult.width}x${imageLoadResult.height}` : 'N/A',
          error: imageLoadResult.error || null
        };
        
        results.push(result);
        
        if (status === 200 && imageLoadResult.success) {
          successCount++;
          console.log(`✅ ${imageName}: 200 OK, loaded in ${imageLoadResult.loadTime}ms (${result.dimensions})`);
        } else {
          failCount++;
          console.log(`❌ ${imageName}: HTTP ${status}, loaded: ${imageLoadResult.success}, error: ${imageLoadResult.error}`);
        }
        
      } catch (error) {
        failCount++;
        console.log(`❌ ${imageName}: Exception - ${error.message}`);
        results.push({
          imageName,
          httpStatus: 0,
          contentType: '',
          contentLength: 0,
          imageLoaded: false,
          loadTime: 0,
          dimensions: 'N/A',
          error: error.message
        });
      }
    }
    
    // Generate summary report
    console.log('\n📊 DIRECT IMAGE VALIDATION SUMMARY');
    console.log('=======================================');
    console.log(`Total images tested: ${STYLE_IMAGES.length}`);
    console.log(`Successful loads: ${successCount}`);
    console.log(`Failed loads: ${failCount}`);
    console.log(`Success rate: ${((successCount / STYLE_IMAGES.length) * 100).toFixed(1)}%`);
    
    // Performance metrics
    const successfulResults = results.filter(r => r.imageLoaded);
    if (successfulResults.length > 0) {
      const avgLoadTime = successfulResults.reduce((sum, r) => sum + r.loadTime, 0) / successfulResults.length;
      const maxLoadTime = Math.max(...successfulResults.map(r => r.loadTime));
      const minLoadTime = Math.min(...successfulResults.map(r => r.loadTime));
      
      console.log(`Average load time: ${avgLoadTime.toFixed(0)}ms`);
      console.log(`Max load time: ${maxLoadTime}ms`);
      console.log(`Min load time: ${minLoadTime}ms`);
    }
    
    console.log('\n📋 DETAILED RESULTS:');
    results.forEach(result => {
      console.log(`${result.imageName}: HTTP ${result.httpStatus}, Loaded: ${result.imageLoaded}, Size: ${result.contentLength} bytes, Time: ${result.loadTime}ms`);
    });
    
    // Assertions
    expect(successCount, 'At least 90% of images should load successfully').toBeGreaterThanOrEqual(STYLE_IMAGES.length * 0.9);
    expect(failCount, 'No more than 2 images should fail to load').toBeLessThanOrEqual(2);
    
    // Performance assertion
    const avgLoadTime = successfulResults.reduce((sum, r) => sum + r.loadTime, 0) / successfulResults.length;
    expect(avgLoadTime, 'Average load time should be under 3 seconds').toBeLessThan(3000);
  });

  test('should validate specific high-priority images load correctly', async ({ page }) => {
    const highPriorityImages = ['corporate.jpg', 'linkedin.jpg', 'executive.jpg', 'casual.jpg', 'tech-professional.jpg'];
    
    console.log('🎯 Testing high-priority images...');
    
    for (const imageName of highPriorityImages) {
      const imageUrl = `${BASE_URL}/${imageName}`;
      
      // HTTP test
      const response = await page.request.get(imageUrl);
      expect(response.status(), `${imageName} should return 200 OK`).toBe(200);
      
      const contentType = response.headers()['content-type'];
      expect(contentType, `${imageName} should be JPEG`).toMatch(/image\/jpeg/i);
      
      const contentLength = parseInt(response.headers()['content-length'] || '0');
      expect(contentLength, `${imageName} should have content`).toBeGreaterThan(0);
      
      console.log(`✅ ${imageName}: HTTP 200, ${contentType}, ${contentLength} bytes`);
    }
  });

  test('should validate CORS and accessibility for frontend integration', async ({ page }) => {
    console.log('🌐 Testing CORS and frontend integration...');
    
    const testImage = 'corporate.jpg';
    const imageUrl = `${BASE_URL}/${testImage}`;
    
    // Create a test page that loads the image
    await page.goto('data:text/html,<!DOCTYPE html><html><body><img id="testImg" /></body></html>');
    
    // Test image loading via DOM
    const loadResult = await page.evaluate(async (url) => {
      const img = document.getElementById('testImg');
      return new Promise((resolve) => {
        img.onload = () => resolve({ success: true, width: img.naturalWidth, height: img.naturalHeight });
        img.onerror = () => resolve({ success: false, error: 'Load failed' });
        img.src = url;
        
        // Timeout after 5 seconds
        setTimeout(() => resolve({ success: false, error: 'Timeout' }), 5000);
      });
    }, imageUrl);
    
    expect(loadResult.success, 'Image should load successfully in DOM').toBe(true);
    expect(loadResult.width, 'Image should have valid dimensions').toBeGreaterThan(0);
    expect(loadResult.height, 'Image should have valid dimensions').toBeGreaterThan(0);
    
    console.log(`✅ ${testImage} loaded successfully: ${loadResult.width}x${loadResult.height}`);
  });
});