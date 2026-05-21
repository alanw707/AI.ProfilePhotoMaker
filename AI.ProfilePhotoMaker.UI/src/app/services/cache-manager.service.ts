import { Injectable } from '@angular/core';
import { CacheEntry, ICacheManagerService } from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root',
})
export class CacheManagerService implements ICacheManagerService {
  private cache = new Map<string, CacheEntry<any>>();
  private lastRequestTimes = new Map<string, number>();

  // Cache duration constants
  static readonly WORKSPACE_CACHE_DURATION_MS = 30000; // 30 seconds
  static readonly HTTP_CACHE_DURATION_MS = 60000; // 60 seconds
  static readonly LOAD_DEBOUNCE_MS = 1000; // 1 second

  constructor() {}

  /**
   * Check if cached data is still valid
   */
  isCacheValid(cacheKey: string): boolean {
    const now = Date.now();
    const cacheEntry = this.cache.get(cacheKey);

    if (!cacheEntry) {
      return false;
    }

    const isValid = now < cacheEntry.expiry;

    if (isValid) {
      // Update last accessed time
      cacheEntry.lastAccessed = now;
    } else {
      // Remove expired cache entry
      this.cache.delete(cacheKey);
    }

    return isValid;
  }

  /**
   * Get cached data if valid
   */
  getCachedData<T>(cacheKey: string): T | null {
    if (this.isCacheValid(cacheKey)) {
      const cacheEntry = this.cache.get(cacheKey);
      return cacheEntry ? cacheEntry.data : null;
    }
    return null;
  }

  /**
   * Set data in cache with expiry
   */
  setCachedData<T>(cacheKey: string, data: T, durationMs: number): void {
    const now = Date.now();
    const cacheEntry: CacheEntry<T> = {
      data,
      expiry: now + durationMs,
      lastAccessed: now,
    };

    this.cache.set(cacheKey, cacheEntry);

    // Clean up old cache entries periodically
    this.cleanupExpiredEntries();
  }

  /**
   * Invalidate specific cache entry
   */
  invalidateCache(cacheKey: string): void {
    if (this.cache.has(cacheKey)) {
      this.cache.delete(cacheKey);
    }
  }

  /**
   * Invalidate all cache entries
   */
  invalidateAllCache(): void {
    this.cache.clear();
    this.lastRequestTimes.clear();
  }

  /**
   * Check if request should be debounced
   */
  shouldDebounceRequest(requestKey: string, debounceMs: number): boolean {
    const now = Date.now();
    const lastRequestTime = this.lastRequestTimes.get(requestKey);

    if (lastRequestTime && now - lastRequestTime < debounceMs) {
      return true;
    }

    this.lastRequestTimes.set(requestKey, now);
    return false;
  }

  /**
   * Force refresh by clearing cache and request times
   */
  forceRefresh(cacheKey?: string): void {
    if (cacheKey) {
      this.invalidateCache(cacheKey);
      this.lastRequestTimes.delete(cacheKey);
    } else {
      this.invalidateAllCache();
    }
  }

  /**
   * Get cache statistics for debugging
   */
  getCacheStats(): {
    totalEntries: number;
    validEntries: number;
    expiredEntries: number;
    cacheHitRatio: number;
  } {
    const now = Date.now();
    let validEntries = 0;
    let expiredEntries = 0;

    for (const [, entry] of this.cache.entries()) {
      if (now < entry.expiry) {
        validEntries++;
      } else {
        expiredEntries++;
      }
    }

    const totalRequests = this.lastRequestTimes.size;
    const cacheHitRatio = totalRequests > 0 ? (validEntries / totalRequests) * 100 : 0;

    return {
      totalEntries: this.cache.size,
      validEntries,
      expiredEntries,
      cacheHitRatio: Math.round(cacheHitRatio * 100) / 100,
    };
  }

  /**
   * Clean up expired cache entries
   */
  private cleanupExpiredEntries(): void {
    const now = Date.now();

    for (const [key, entry] of this.cache.entries()) {
      if (now >= entry.expiry) {
        this.cache.delete(key);
      }
    }

    // Silently clean up expired entries
  }
}
