import { Injectable } from '@angular/core';
import { CacheEntry, CacheStats, ICacheManagerService } from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root'
})
export class CacheManagerService implements ICacheManagerService {
  private cache = new Map<string, CacheEntry<any>>();
  private lastRequestTimes = new Map<string, number>();
  
  // Cache duration constants
  static readonly DASHBOARD_CACHE_DURATION_MS = 30000; // 30 seconds
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
      console.log(`💾 Cache hit for ${cacheKey} - valid for ${Math.round((cacheEntry.expiry - now) / 1000)}s more`);
    } else {
      // Remove expired cache entry
      this.cache.delete(cacheKey);
      console.log(`🗑️ Cache expired for ${cacheKey}`);
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
      lastAccessed: now
    };
    
    this.cache.set(cacheKey, cacheEntry);
    console.log(`💾 Data cached for ${cacheKey} - expires in ${Math.round(durationMs / 1000)}s`);
    
    // Clean up old cache entries periodically
    this.cleanupExpiredEntries();
  }

  /**
   * Invalidate specific cache entry
   */
  invalidateCache(cacheKey: string): void {
    if (this.cache.has(cacheKey)) {
      this.cache.delete(cacheKey);
      console.log(`🗑️ Cache invalidated for ${cacheKey}`);
    }
  }

  /**
   * Invalidate all cache entries
   */
  invalidateAllCache(): void {
    const keysCount = this.cache.size;
    this.cache.clear();
    this.lastRequestTimes.clear();
    console.log(`🗑️ All cache invalidated (${keysCount} entries cleared)`);
  }

  /**
   * Check if request should be debounced
   */
  shouldDebounceRequest(requestKey: string, debounceMs: number): boolean {
    const now = Date.now();
    const lastRequestTime = this.lastRequestTimes.get(requestKey);
    
    if (lastRequestTime && (now - lastRequestTime) < debounceMs) {
      console.log(`🚫 Request debounced for ${requestKey} - too soon after last request`);
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
      console.log(`🔄 Force refresh for ${cacheKey}`);
    } else {
      this.invalidateAllCache();
      console.log('🔄 Force refresh - all caches cleared');
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
    
    for (const [key, entry] of this.cache.entries()) {
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
      cacheHitRatio: Math.round(cacheHitRatio * 100) / 100
    };
  }

  /**
   * Clean up expired cache entries
   */
  private cleanupExpiredEntries(): void {
    const now = Date.now();
    let cleanedCount = 0;
    
    for (const [key, entry] of this.cache.entries()) {
      if (now >= entry.expiry) {
        this.cache.delete(key);
        cleanedCount++;
      }
    }
    
    if (cleanedCount > 0) {
      console.log(`🧹 Cleaned up ${cleanedCount} expired cache entries`);
    }
  }

  /**
   * Enable global debug methods for cache inspection
   */
  enableGlobalDebug(): void {
    (window as any).cacheStats = () => this.getCacheStats();
    (window as any).clearCache = (key?: string) => this.forceRefresh(key);
    (window as any).viewCache = () => {
      const cacheData: any = {};
      for (const [key, entry] of this.cache.entries()) {
        cacheData[key] = {
          expiry: new Date(entry.expiry).toISOString(),
          lastAccessed: new Date(entry.lastAccessed).toISOString(),
          isValid: Date.now() < entry.expiry
        };
      }
      return cacheData;
    };
    
    console.log('🔍 Cache debug enabled! Available commands:');
    console.log('  - cacheStats() - View cache statistics');
    console.log('  - clearCache(key?) - Clear specific or all cache');
    console.log('  - viewCache() - View all cache entries with metadata');
  }
}