import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface ImageValidationResult {
  isValid: boolean;
  status: number | null;
  error?: string;
  url: string;
  repairSuggested?: boolean;
}

export interface ImageValidationSummary {
  validUrls: string[];
  invalidUrls: string[];
  results: ImageValidationResult[];
  repairSuggested: boolean;
  notFoundCount: number;
}

@Injectable({
  providedIn: 'root',
})
export class ImageValidationService {
  private validationCache = new Map<string, ImageValidationResult>();
  private readonly CACHE_TTL = 5 * 60 * 1000; // 5 minutes
  private readonly REQUEST_TIMEOUT = 10000; // 10 seconds
  private readonly FIRST_LOAD_DELAY = 2000; // 2 seconds delay for first load
  private isFirstLoad = true;

  constructor() {}

  /**
   * Validates if an image URL is accessible and returns a valid image
   * Uses HTTP HEAD request to check without downloading the full image
   * Defers validation on first load to improve performance
   */
  async validateImageUrl(url: string, skipFirstLoadDelay = false): Promise<ImageValidationResult> {
    // Fix: Convert absolute frontend URLs to relative paths for proxy routing
    const correctedUrl = this.correctImageUrl(url);

    // Skip validation for relative URLs (assume they're valid to avoid network calls)
    if (this.shouldSkipValidation(correctedUrl)) {
      return {
        isValid: true,
        status: 200,
        url: correctedUrl,
      };
    }

    // Check cache first
    const cached = this.validationCache.get(correctedUrl);
    if (cached && this.isCacheValid(correctedUrl)) {
      return cached;
    }

    // Defer validation on first load for performance
    if (this.isFirstLoad && !skipFirstLoadDelay) {
      setTimeout(() => this.validateImageUrl(correctedUrl, true), this.FIRST_LOAD_DELAY);
      this.isFirstLoad = false;
      // Return optimistic result for first load
      return {
        isValid: true,
        status: 200,
        url: correctedUrl,
      };
    }

    try {
      // Use HEAD request to check if image exists without downloading it
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.REQUEST_TIMEOUT);

      const response = await fetch(correctedUrl, {
        method: 'HEAD',
        signal: controller.signal,
        // Add credentials if needed for authenticated endpoints
        credentials: 'include',
      });

      clearTimeout(timeoutId);

      const result: ImageValidationResult = {
        isValid: response.ok,
        status: response.status,
        url: correctedUrl,
      };

      if (!response.ok) {
        result.error = `HTTP ${response.status}: ${response.statusText}`;
        // Suggest repair for 404s (orphaned database records)
        result.repairSuggested = response.status === 404;
      }

      // Cache the result
      this.cacheResult(correctedUrl, result);

      return result;
    } catch (error: any) {
      console.warn(`❌ Image validation failed for ${correctedUrl}:`, error);

      const result: ImageValidationResult = {
        isValid: false,
        status: null,
        error: error.name === 'AbortError' ? 'Timeout' : error.message,
        url: correctedUrl,
      };

      // Cache failed results for a shorter time to allow retries
      this.cacheResult(correctedUrl, result, 30000); // 30 seconds for failures

      return result;
    }
  }

  /**
   * Validates multiple image URLs in parallel
   * Returns detailed validation summary including repair suggestions
   */
  async validateImageUrls(urls: string[]): Promise<ImageValidationSummary> {
    const validationPromises = urls.map(url => this.validateImageUrl(url));
    const results = await Promise.all(validationPromises);

    const validUrls = results.filter(r => r.isValid).map(r => r.url);
    const invalidUrls = results.filter(r => !r.isValid).map(r => r.url);
    const notFoundCount = results.filter(r => r.status === 404).length;
    const repairSuggested = results.some(r => r.repairSuggested);

    console.log(
      `✅ Validation complete: ${validUrls.length} valid, ${invalidUrls.length} invalid${repairSuggested ? ', repair suggested' : ''}`
    );

    return {
      validUrls,
      invalidUrls,
      results,
      repairSuggested,
      notFoundCount,
    };
  }

  /**
   * Checks if a specific URL returns a 404 error
   */
  async isImageNotFound(url: string): Promise<boolean> {
    const result = await this.validateImageUrl(url);
    return result.status === 404;
  }

  /**
   * Filters an array of image objects, removing those with invalid URLs
   */
  async filterValidImages<T extends { url: string }>(
    images: T[]
  ): Promise<{
    validImages: T[];
    invalidImages: T[];
    removedCount: number;
    repairSuggested: boolean;
    notFoundCount: number;
  }> {
    if (images.length === 0) {
      return {
        validImages: [],
        invalidImages: [],
        removedCount: 0,
        repairSuggested: false,
        notFoundCount: 0,
      };
    }

    const urls = images.map(img => img.url);
    const validation = await this.validateImageUrls(urls);

    const validImages = images.filter(img => validation.validUrls.includes(img.url));
    const invalidImages = images.filter(img => validation.invalidUrls.includes(img.url));

    return {
      validImages,
      invalidImages,
      removedCount: invalidImages.length,
      repairSuggested: validation.repairSuggested,
      notFoundCount: validation.notFoundCount,
    };
  }

  private cacheResult(url: string, result: ImageValidationResult, ttl: number = this.CACHE_TTL) {
    this.validationCache.set(url, {
      ...result,
      // Add timestamp for TTL
      timestamp: Date.now(),
    } as any);

    // Set cleanup timer
    setTimeout(() => {
      this.validationCache.delete(url);
    }, ttl);
  }

  private isCacheValid(url: string): boolean {
    const cached = this.validationCache.get(url) as any;
    if (!cached) {
      return false;
    }

    const age = Date.now() - (cached.timestamp || 0);
    return age < this.CACHE_TTL;
  }

  /**
   * Clears the validation cache
   */
  clearCache() {
    this.validationCache.clear();
  }

  /**
   * Gets cache stats for debugging
   */
  getCacheStats() {
    return {
      size: this.validationCache.size,
      entries: Array.from(this.validationCache.keys()),
    };
  }

  /**
   * Validates images progressively with batching and delays
   * Improves first-load performance by spreading validation requests
   */
  async validateImageUrlsProgressive(
    urls: string[],
    batchSize = 3,
    delayMs = 500
  ): Promise<ImageValidationSummary> {
    console.log(
      `🔍 Progressive validation of ${urls.length} URLs (batch: ${batchSize}, delay: ${delayMs}ms)`
    );

    const results: ImageValidationResult[] = [];

    // Process URLs in batches with delays
    for (let i = 0; i < urls.length; i += batchSize) {
      const batch = urls.slice(i, i + batchSize);

      // Add delay between batches (except first batch)
      if (i > 0) {
        await new Promise(resolve => setTimeout(resolve, delayMs));
      }

      const batchPromises = batch.map(url => this.validateImageUrl(url, true));
      const batchResults = await Promise.all(batchPromises);
      results.push(...batchResults);
    }

    // Compile summary
    const validUrls = results.filter(r => r.isValid).map(r => r.url);
    const invalidUrls = results.filter(r => !r.isValid).map(r => r.url);
    const notFoundCount = results.filter(r => r.status === 404).length;
    const repairSuggested = results.some(r => r.repairSuggested);

    console.log(
      `✅ Progressive validation complete: ${validUrls.length} valid, ${invalidUrls.length} invalid`
    );

    return {
      validUrls,
      invalidUrls,
      results,
      repairSuggested,
      notFoundCount,
    };
  }

  /**
   * Determines if validation should be skipped for certain URL patterns
   * Skips validation for relative URLs and known-good patterns to improve performance
   */
  private shouldSkipValidation(url: string): boolean {
    if (!url) {
      return true;
    }

    // Skip validation for relative URLs (they go through proxy)
    if (
      url.startsWith('/uploads/') ||
      url.startsWith('/generated/') ||
      url.startsWith('/style-previews/')
    ) {
      return true;
    }

    // Skip validation for localhost URLs (development)
    if (url.includes('localhost:')) {
      return true;
    }

    return false;
  }

  /**
   * Corrects absolute frontend URLs to relative paths for proper proxy routing
   * Fixes the architectural issue where frontend URLs don't route through the backend
   */
  private correctImageUrl(url: string): string {
    if (!url) {
      return url;
    }

    // If it's already a relative path, keep it as is
    if (url.startsWith('/')) {
      return url;
    }

    // Fix absolute frontend ngrok URLs to relative paths
    if (url.includes('awlocaldev.ngrok.app/uploads/')) {
      const path = url.substring(url.indexOf('/uploads/'));
      console.log(`🔧 Correcting frontend URL to relative path: ${url} → ${path}`);
      return path;
    }

    // Fix absolute frontend ngrok URLs for other static paths
    if (url.includes('awlocaldev.ngrok.app/generated/')) {
      const path = url.substring(url.indexOf('/generated/'));
      console.log(`🔧 Correcting frontend URL to relative path: ${url} → ${path}`);
      return path;
    }

    // Fix localhost URLs to relative paths
    if (url.includes('localhost:4200/uploads/')) {
      const path = url.substring(url.indexOf('/uploads/'));
      console.log(`🔧 Correcting localhost URL to relative path: ${url} → ${path}`);
      return path;
    }

    if (url.includes('localhost:4200/generated/')) {
      const path = url.substring(url.indexOf('/generated/'));
      console.log(`🔧 Correcting localhost URL to relative path: ${url} → ${path}`);
      return path;
    }

    // Keep other URLs as is (backend URLs, external URLs, etc.)
    return url;
  }
}
