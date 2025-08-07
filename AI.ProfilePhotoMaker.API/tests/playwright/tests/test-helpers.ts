import { Page, expect, APIResponse } from '@playwright/test';
import { PERFORMANCE_THRESHOLDS, HTTP_STATUS } from './test-data';

/**
 * Test helper utilities for style preview validation
 */

export interface ImageLoadResult {
  success: boolean;
  loadTime: number;
  naturalWidth: number;
  naturalHeight: number;
  fileSize?: number;
  error?: string;
}

export interface ApiResponseResult {
  status: number;
  success: boolean;
  responseTime: number;
  contentType?: string;
  contentLength?: number;
  error?: string;
}

/**
 * Load an image and measure performance metrics
 */
export async function loadImageWithMetrics(
  page: Page, 
  imageUrl: string, 
  timeout: number = PERFORMANCE_THRESHOLDS.imageLoadTimeout
): Promise<ImageLoadResult> {
  const startTime = Date.now();
  
  try {
    // Navigate to a test page and inject image loading script
    await page.evaluate(({ url, timeoutMs }) => {
      return new Promise<ImageLoadResult>((resolve, reject) => {
        const img = new Image();
        const startTime = Date.now();
        
        const timeoutId = setTimeout(() => {
          reject(new Error(`Image load timeout after ${timeoutMs}ms`));
        }, timeoutMs);
        
        img.onload = () => {
          clearTimeout(timeoutId);
          const loadTime = Date.now() - startTime;
          resolve({
            success: true,
            loadTime,
            naturalWidth: img.naturalWidth,
            naturalHeight: img.naturalHeight
          });
        };
        
        img.onerror = () => {
          clearTimeout(timeoutId);
          const loadTime = Date.now() - startTime;
          resolve({
            success: false,
            loadTime,
            naturalWidth: 0,
            naturalHeight: 0,
            error: 'Image failed to load'
          });
        };
        
        img.src = url;
      });
    }, { url: imageUrl, timeoutMs: timeout });
    
    const loadTime = Date.now() - startTime;
    const result = await page.evaluate(() => window.lastImageLoadResult) as ImageLoadResult;
    
    return {
      ...result,
      loadTime
    };
    
  } catch (error) {
    return {
      success: false,
      loadTime: Date.now() - startTime,
      naturalWidth: 0,
      naturalHeight: 0,
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Make HTTP request and measure response metrics
 */
export async function makeRequestWithMetrics(
  page: Page,
  url: string,
  timeout: number = PERFORMANCE_THRESHOLDS.apiResponseTimeout
): Promise<ApiResponseResult> {
  const startTime = Date.now();
  
  try {
    const response = await page.request.get(url, { timeout });
    const responseTime = Date.now() - startTime;
    
    return {
      status: response.status(),
      success: response.ok(),
      responseTime,
      contentType: response.headers()['content-type'],
      contentLength: parseInt(response.headers()['content-length'] || '0'),
    };
  } catch (error) {
    return {
      status: 0,
      success: false,
      responseTime: Date.now() - startTime,
      error: error instanceof Error ? error.message : 'Unknown error'
    };
  }
}

/**
 * Validate image dimensions and quality
 */
export async function validateImageQuality(
  page: Page, 
  imageUrl: string
): Promise<{
  isValid: boolean;
  width: number;
  height: number;
  aspectRatio: number;
  issues: string[];
}> {
  const issues: string[] = [];
  const result = await loadImageWithMetrics(page, imageUrl);
  
  if (!result.success) {
    issues.push('Image failed to load');
    return {
      isValid: false,
      width: 0,
      height: 0,
      aspectRatio: 0,
      issues
    };
  }
  
  const { naturalWidth: width, naturalHeight: height } = result;
  
  // Check minimum dimensions
  if (width < PERFORMANCE_THRESHOLDS.minImageWidth) {
    issues.push(`Width ${width}px is below minimum ${PERFORMANCE_THRESHOLDS.minImageWidth}px`);
  }
  
  if (height < PERFORMANCE_THRESHOLDS.minImageHeight) {
    issues.push(`Height ${height}px is below minimum ${PERFORMANCE_THRESHOLDS.minImageHeight}px`);
  }
  
  // Check aspect ratio (should be reasonable for headshots)
  const aspectRatio = width / height;
  if (aspectRatio < 0.5 || aspectRatio > 2.0) {
    issues.push(`Unusual aspect ratio: ${aspectRatio.toFixed(2)}`);
  }
  
  return {
    isValid: issues.length === 0,
    width,
    height,
    aspectRatio,
    issues
  };
}

/**
 * Test image accessibility and loading performance
 */
export async function testImageAccessibility(
  page: Page,
  imageUrl: string,
  expectedStatus: number
): Promise<{
  status: number;
  accessible: boolean;
  loadTime: number;
  imageValid: boolean;
  issues: string[];
}> {
  const issues: string[] = [];
  
  // Test HTTP response
  const response = await makeRequestWithMetrics(page, imageUrl);
  
  // Check expected status
  if (response.status !== expectedStatus) {
    if (expectedStatus === HTTP_STATUS.NOT_FOUND && response.status === HTTP_STATUS.NOT_FOUND) {
      // This is expected for pre-upload tests
      return {
        status: response.status,
        accessible: false,
        loadTime: response.responseTime,
        imageValid: false,
        issues: ['Image not found (expected)']
      };
    } else if (expectedStatus === HTTP_STATUS.OK && response.status !== HTTP_STATUS.OK) {
      issues.push(`Expected status ${expectedStatus}, got ${response.status}`);
    }
  }
  
  let imageValid = false;
  let loadTime = response.responseTime;
  
  // If we expect the image to exist, test actual image loading
  if (expectedStatus === HTTP_STATUS.OK && response.status === HTTP_STATUS.OK) {
    const imageResult = await loadImageWithMetrics(page, imageUrl);
    imageValid = imageResult.success;
    loadTime = Math.max(loadTime, imageResult.loadTime);
    
    if (!imageResult.success) {
      issues.push('Image failed to render');
    }
    
    // Test performance
    if (imageResult.loadTime > PERFORMANCE_THRESHOLDS.targetLoadTime) {
      issues.push(`Slow load time: ${imageResult.loadTime}ms`);
    }
    
    // Test dimensions
    if (imageResult.success) {
      const qualityResult = await validateImageQuality(page, imageUrl);
      issues.push(...qualityResult.issues);
    }
  }
  
  return {
    status: response.status,
    accessible: response.status === expectedStatus,
    loadTime,
    imageValid,
    issues
  };
}

/**
 * Generate a comprehensive test report
 */
export function generateTestReport(
  testName: string,
  results: Array<{
    styleName: string;
    status: number;
    accessible: boolean;
    loadTime: number;
    imageValid: boolean;
    issues: string[];
  }>
): string {
  const totalTests = results.length;
  const passedTests = results.filter(r => r.accessible && r.issues.length === 0).length;
  const failedTests = totalTests - passedTests;
  
  const avgLoadTime = results.reduce((sum, r) => sum + r.loadTime, 0) / totalTests;
  const maxLoadTime = Math.max(...results.map(r => r.loadTime));
  
  let report = `
# ${testName} Test Report

## Summary
- **Total Tests**: ${totalTests}
- **Passed**: ${passedTests}
- **Failed**: ${failedTests}
- **Success Rate**: ${((passedTests / totalTests) * 100).toFixed(1)}%

## Performance Metrics
- **Average Load Time**: ${avgLoadTime.toFixed(0)}ms
- **Maximum Load Time**: ${maxLoadTime}ms
- **Target Load Time**: ${PERFORMANCE_THRESHOLDS.targetLoadTime}ms

## Detailed Results

`;

  results.forEach((result, index) => {
    const statusIcon = result.accessible && result.issues.length === 0 ? '✅' : '❌';
    const statusText = result.status === HTTP_STATUS.NOT_FOUND ? '404 Not Found' :
                       result.status === HTTP_STATUS.OK ? '200 OK' :
                       `${result.status} ${getStatusText(result.status)}`;
    
    report += `### ${index + 1}. ${result.styleName} ${statusIcon}
- **Status**: ${statusText}
- **Load Time**: ${result.loadTime}ms
- **Image Valid**: ${result.imageValid ? 'Yes' : 'No'}
- **Issues**: ${result.issues.length === 0 ? 'None' : result.issues.join(', ')}

`;
  });
  
  return report;
}

/**
 * Get HTTP status text
 */
function getStatusText(status: number): string {
  switch (status) {
    case 200: return 'OK';
    case 404: return 'Not Found';
    case 403: return 'Forbidden';
    case 500: return 'Server Error';
    default: return 'Unknown';
  }
}

/**
 * Wait for network idle state
 */
export async function waitForNetworkIdle(
  page: Page,
  timeout: number = 5000
): Promise<void> {
  await page.waitForLoadState('networkidle', { timeout });
}

/**
 * Capture screenshot with timestamp
 */
export async function captureTimestampedScreenshot(
  page: Page,
  name: string,
  path?: string
): Promise<string> {
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const filename = `${name}-${timestamp}.png`;
  const screenshotPath = path ? `${path}/${filename}` : filename;
  
  await page.screenshot({ 
    path: screenshotPath,
    fullPage: true 
  });
  
  return screenshotPath;
}