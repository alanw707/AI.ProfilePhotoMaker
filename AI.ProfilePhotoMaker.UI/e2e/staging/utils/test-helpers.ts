import { Page, expect } from '@playwright/test';

export interface TestMetrics {
  pageLoadTime: number;
  imageLoadCount: number;
  failedNetworkRequests: string[];
  consoleErrors: string[];
  performanceScore?: number;
}

export class StagingTestHelpers {
  constructor(private page: Page) {}

  /**
   * Monitor network requests and capture failures
   */
  async captureNetworkMetrics(): Promise<{ success: number; failed: string[] }> {
    const metrics = { success: 0, failed: [] as string[] };
    
    this.page.on('response', response => {
      if (response.status() >= 400) {
        metrics.failed.push(`${response.status()} - ${response.url()}`);
      } else {
        metrics.success++;
      }
    });
    
    return metrics;
  }

  /**
   * Monitor console for errors
   */
  async captureConsoleErrors(): Promise<string[]> {
    const errors: string[] = [];
    
    this.page.on('console', msg => {
      if (msg.type() === 'error') {
        errors.push(msg.text());
      }
    });
    
    return errors;
  }

  /**
   * Wait for images to load and verify they're not placeholders
   */
  async verifyImageLoading(selector: string): Promise<{
    totalImages: number;
    loadedImages: number;
    placeholderImages: number;
    realImages: number;
  }> {
    await this.page.waitForSelector(selector);
    
    const imageMetrics = await this.page.evaluate((sel) => {
      const images = document.querySelectorAll(sel);
      let loaded = 0;
      let placeholder = 0;
      let real = 0;
      
      images.forEach(img => {
        const imgElement = img as HTMLImageElement;
        if (imgElement.complete && imgElement.naturalWidth > 0) {
          loaded++;
          // Check if it's a placeholder (SVG data URI or contains specific placeholder indicators)
          if (imgElement.src.startsWith('data:image/svg+xml') || 
              imgElement.src.includes('placeholder') ||
              imgElement.alt?.includes('placeholder')) {
            placeholder++;
          } else {
            real++;
          }
        }
      });
      
      return {
        totalImages: images.length,
        loadedImages: loaded,
        placeholderImages: placeholder,
        realImages: real
      };
    }, selector);
    
    return imageMetrics;
  }

  /**
   * Check if Azure Blob Storage URLs are being used
   */
  async verifyAzureBlobStorageUsage(): Promise<{
    azureUrls: string[];
    nonAzureUrls: string[];
  }> {
    const imageUrls = await this.page.evaluate(() => {
      const images = document.querySelectorAll('img');
      return Array.from(images).map(img => img.src);
    });
    
    const azureUrls = imageUrls.filter(url => 
      url.includes('blob.core.windows.net') || 
      url.includes('aiprofilemakerstrg3bawc74')
    );
    
    const nonAzureUrls = imageUrls.filter(url => 
      !url.includes('blob.core.windows.net') && 
      !url.includes('aiprofilemakerstrg3bawc74') &&
      !url.startsWith('data:') &&
      url.startsWith('http')
    );
    
    return { azureUrls, nonAzureUrls };
  }

  /**
   * Measure page load performance
   */
  async measurePageLoadTime(): Promise<number> {
    const startTime = Date.now();
    await this.page.waitForLoadState('networkidle');
    return Date.now() - startTime;
  }

  /**
   * Verify API endpoints are working
   */
  async verifyApiEndpoints(): Promise<{
    workingEndpoints: string[];
    failedEndpoints: string[];
  }> {
    const working: string[] = [];
    const failed: string[] = [];
    
    this.page.on('response', response => {
      const url = response.url();
      if (url.includes('/api/')) {
        if (response.status() < 400) {
          working.push(`${response.status()} - ${url}`);
        } else {
          failed.push(`${response.status()} - ${url}`);
        }
      }
    });
    
    return { workingEndpoints: working, failedEndpoints: failed };
  }

  /**
   * Take screenshot with timestamp
   */
  async takeTimestampedScreenshot(name: string): Promise<string> {
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const filename = `${name}-${timestamp}.png`;
    await this.page.screenshot({ path: `screenshots/${filename}`, fullPage: true });
    return filename;
  }

  /**
   * Verify no critical console errors
   */
  async verifyCriticalErrors(): Promise<string[]> {
    const criticalErrors: string[] = [];
    
    this.page.on('console', msg => {
      if (msg.type() === 'error') {
        const text = msg.text();
        // Filter out non-critical errors
        if (!text.includes('favicon.ico') && 
            !text.includes('Extension') &&
            !text.includes('DevTools')) {
          criticalErrors.push(text);
        }
      }
    });
    
    return criticalErrors;
  }

  /**
   * Verify style preview images are real photos from Azure Blob Storage
   */
  async verifyStylePreviewImages(): Promise<{
    total: number;
    azureHosted: number;
    placeholders: number;
    loadErrors: number;
  }> {
    // Wait for style section to load
    await this.page.waitForSelector('.styled-photos-grid, .style-showcase', { timeout: 10000 });
    
    const results = await this.page.evaluate(async () => {
      const styleImages = document.querySelectorAll('.styled-photos-grid img, .style-showcase img, .style-card img');
      let total = styleImages.length;
      let azureHosted = 0;
      let placeholders = 0;
      let loadErrors = 0;
      
      for (const img of styleImages) {
        const imgElement = img as HTMLImageElement;
        
        // Check if hosted on Azure Blob Storage
        if (imgElement.src.includes('blob.core.windows.net') || 
            imgElement.src.includes('aiprofilemakerstrg3bawc74')) {
          azureHosted++;
        }
        
        // Check if it's a placeholder
        if (imgElement.src.startsWith('data:image/svg+xml') ||
            imgElement.alt?.includes('placeholder') ||
            imgElement.src.includes('placeholder')) {
          placeholders++;
        }
        
        // Check for load errors
        if (!imgElement.complete || imgElement.naturalWidth === 0) {
          loadErrors++;
        }
      }
      
      return { total, azureHosted, placeholders, loadErrors };
    });
    
    return results;
  }
}

export async function waitForStableLoad(page: Page, timeout = 10000): Promise<void> {
  await page.waitForLoadState('networkidle', { timeout });
  await page.waitForFunction(() => document.readyState === 'complete');
}