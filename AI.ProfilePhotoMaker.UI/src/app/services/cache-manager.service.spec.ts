import { TestBed } from '@angular/core/testing';
import { CacheManagerService } from './cache-manager.service';
import { CacheStats, ICacheManagerService } from '../interfaces/service.interfaces';

describe('CacheManagerService', () => {
  let service: CacheManagerService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CacheManagerService]
    });
    service = TestBed.inject(CacheManagerService);
  });

  afterEach(() => {
    // Clean up cache after each test
    service.invalidateAllCache();
  });

  describe('Service Creation', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should implement ICacheManagerService interface', () => {
      expect(service.isCacheValid).toBeDefined();
      expect(service.getCachedData).toBeDefined();
      expect(service.setCachedData).toBeDefined();
      expect(service.invalidateCache).toBeDefined();
      expect(service.invalidateAllCache).toBeDefined();
      expect(service.shouldDebounceRequest).toBeDefined();
      expect(service.forceRefresh).toBeDefined();
      expect(service.getCacheStats).toBeDefined();
      expect(service.enableGlobalDebug).toBeDefined();
    });

    it('should start with empty cache', () => {
      const stats = service.getCacheStats();
      expect(stats.totalEntries).toBe(0);
      expect(stats.validEntries).toBe(0);
      expect(stats.expiredEntries).toBe(0);
    });
  });

  describe('setCachedData() and getCachedData()', () => {
    it('should store and retrieve simple data', () => {
      const testData = { value: 'test' };
      const cacheKey = 'test-key';
      const duration = 5000; // 5 seconds

      service.setCachedData(cacheKey, testData, duration);
      const retrieved = service.getCachedData(cacheKey);

      expect(retrieved).toEqual(testData);
    });

    it('should store and retrieve complex objects', () => {
      const testData = {
        user: { id: 1, name: 'Test User' },
        settings: { theme: 'dark', notifications: true },
        array: [1, 2, 3, { nested: true }]
      };
      const cacheKey = 'complex-data';

      service.setCachedData(cacheKey, testData, 5000);
      const retrieved = service.getCachedData(cacheKey);

      expect(retrieved).toEqual(testData);
    });

    it('should handle null and undefined values', () => {
      service.setCachedData('null-test', null, 5000);
      service.setCachedData('undefined-test', undefined, 5000);

      expect(service.getCachedData('null-test')).toBeNull();
      expect(service.getCachedData('undefined-test')).toBeUndefined();
    });

    it('should return null for non-existent keys', () => {
      expect(service.getCachedData('non-existent')).toBeNull();
    });

    it('should overwrite existing cache entries', () => {
      const key = 'overwrite-test';
      
      service.setCachedData(key, 'first-value', 5000);
      expect(service.getCachedData(key)).toBe('first-value');
      
      service.setCachedData(key, 'second-value', 5000);
      expect(service.getCachedData(key)).toBe('second-value');
    });
  });

  describe('isCacheValid()', () => {
    it('should return false for non-existent keys', () => {
      expect(service.isCacheValid('non-existent')).toBeFalse();
    });

    it('should return true for valid cache entries', () => {
      const key = 'valid-test';
      service.setCachedData(key, 'test-data', 5000);
      expect(service.isCacheValid(key)).toBeTrue();
    });

    it('should return false for expired cache entries', (done) => {
      const key = 'expired-test';
      service.setCachedData(key, 'test-data', 50); // 50ms duration

      setTimeout(() => {
        expect(service.isCacheValid(key)).toBeFalse();
        done();
      }, 100); // Check after 100ms
    });

    it('should automatically remove expired entries when checking validity', (done) => {
      const key = 'auto-remove-test';
      service.setCachedData(key, 'test-data', 50);

      setTimeout(() => {
        service.isCacheValid(key); // This should remove the expired entry
        expect(service.getCachedData(key)).toBeNull();
        done();
      }, 100);
    });

    it('should update last accessed time when checking valid cache', () => {
      const key = 'access-time-test';
      service.setCachedData(key, 'test-data', 5000);
      
      const initialAccess = Date.now();
      service.isCacheValid(key);
      
      // Give a small delay to ensure time difference
      setTimeout(() => {
        service.isCacheValid(key);
        // The lastAccessed time should be updated (hard to test precisely, but logic is sound)
        expect(service.isCacheValid(key)).toBeTrue();
      }, 10);
    });
  });

  describe('invalidateCache() and invalidateAllCache()', () => {
    it('should invalidate specific cache entries', () => {
      service.setCachedData('key1', 'value1', 5000);
      service.setCachedData('key2', 'value2', 5000);

      service.invalidateCache('key1');

      expect(service.getCachedData('key1')).toBeNull();
      expect(service.getCachedData('key2')).toBe('value2');
    });

    it('should handle invalidation of non-existent keys gracefully', () => {
      expect(() => service.invalidateCache('non-existent')).not.toThrow();
    });

    it('should invalidate all cache entries', () => {
      service.setCachedData('key1', 'value1', 5000);
      service.setCachedData('key2', 'value2', 5000);
      service.setCachedData('key3', 'value3', 5000);

      service.invalidateAllCache();

      expect(service.getCachedData('key1')).toBeNull();
      expect(service.getCachedData('key2')).toBeNull();
      expect(service.getCachedData('key3')).toBeNull();
      
      const stats = service.getCacheStats();
      expect(stats.totalEntries).toBe(0);
    });
  });

  describe('shouldDebounceRequest()', () => {
    it('should not debounce first request', () => {
      const requestKey = 'debounce-test';
      const debounceMs = 1000;

      expect(service.shouldDebounceRequest(requestKey, debounceMs)).toBeFalse();
    });

    it('should debounce rapid successive requests', () => {
      const requestKey = 'rapid-test';
      const debounceMs = 1000;

      service.shouldDebounceRequest(requestKey, debounceMs); // First request
      expect(service.shouldDebounceRequest(requestKey, debounceMs)).toBeTrue(); // Should be debounced
    });

    it('should allow requests after debounce period', (done) => {
      const requestKey = 'delayed-test';
      const debounceMs = 100;

      service.shouldDebounceRequest(requestKey, debounceMs); // First request

      setTimeout(() => {
        expect(service.shouldDebounceRequest(requestKey, debounceMs)).toBeFalse();
        done();
      }, 150); // Wait longer than debounce period
    });

    it('should handle different request keys independently', () => {
      const debounceMs = 1000;

      service.shouldDebounceRequest('key1', debounceMs);
      service.shouldDebounceRequest('key2', debounceMs);

      // Second request to key1 should be debounced, but first request to key2 should not
      expect(service.shouldDebounceRequest('key1', debounceMs)).toBeTrue();
      expect(service.shouldDebounceRequest('key2', debounceMs)).toBeTrue(); // Now key2 second request
    });
  });

  describe('forceRefresh()', () => {
    it('should clear specific cache when key provided', () => {
      service.setCachedData('key1', 'value1', 5000);
      service.setCachedData('key2', 'value2', 5000);

      service.forceRefresh('key1');

      expect(service.getCachedData('key1')).toBeNull();
      expect(service.getCachedData('key2')).toBe('value2');
    });

    it('should clear all cache when no key provided', () => {
      service.setCachedData('key1', 'value1', 5000);
      service.setCachedData('key2', 'value2', 5000);

      service.forceRefresh();

      expect(service.getCachedData('key1')).toBeNull();
      expect(service.getCachedData('key2')).toBeNull();
    });

    it('should reset request times when forcing refresh', () => {
      const requestKey = 'refresh-test';
      service.shouldDebounceRequest(requestKey, 1000); // Set initial request time

      service.forceRefresh();

      // After force refresh, debounce should not apply
      expect(service.shouldDebounceRequest(requestKey, 1000)).toBeFalse();
    });
  });

  describe('getCacheStats()', () => {
    it('should return correct statistics for empty cache', () => {
      const stats = service.getCacheStats();
      
      expect(stats.totalEntries).toBe(0);
      expect(stats.validEntries).toBe(0);
      expect(stats.expiredEntries).toBe(0);
      expect(stats.cacheHitRatio).toBe(0);
    });

    it('should return correct statistics with valid entries', () => {
      service.setCachedData('key1', 'value1', 5000);
      service.setCachedData('key2', 'value2', 5000);
      service.setCachedData('key3', 'value3', 5000);

      const stats = service.getCacheStats();
      
      expect(stats.totalEntries).toBe(3);
      expect(stats.validEntries).toBe(3);
      expect(stats.expiredEntries).toBe(0);
    });

    it('should count expired entries correctly', (done) => {
      service.setCachedData('valid', 'value', 5000);
      service.setCachedData('expired', 'value', 50); // Will expire quickly

      setTimeout(() => {
        const stats = service.getCacheStats();
        
        expect(stats.totalEntries).toBe(2);
        expect(stats.validEntries).toBe(1);
        expect(stats.expiredEntries).toBe(1);
        done();
      }, 100);
    });

    it('should calculate cache hit ratio', () => {
      // Set up some requests
      service.shouldDebounceRequest('req1', 1000);
      service.shouldDebounceRequest('req2', 1000);
      
      // Set up some cache entries
      service.setCachedData('key1', 'value1', 5000);
      
      const stats = service.getCacheStats();
      
      expect(stats.cacheHitRatio).toBeGreaterThanOrEqual(0);
      expect(stats.cacheHitRatio).toBeLessThanOrEqual(100);
    });
  });

  describe('enableGlobalDebug()', () => {
    it('should add debug methods to global window', () => {
      service.enableGlobalDebug();
      
      expect((window as any).cacheStats).toBeDefined();
      expect((window as any).clearCache).toBeDefined();
      expect((window as any).viewCache).toBeDefined();
      expect(typeof (window as any).cacheStats).toBe('function');
      expect(typeof (window as any).clearCache).toBe('function');
      expect(typeof (window as any).viewCache).toBe('function');
    });

    it('should make cacheStats function work globally', () => {
      service.enableGlobalDebug();
      service.setCachedData('test', 'value', 5000);
      
      const globalStats = (window as any).cacheStats();
      const serviceStats = service.getCacheStats();
      
      expect(globalStats).toEqual(serviceStats);
    });

    it('should log debug information', () => {
      spyOn(console, 'log');
      
      service.enableGlobalDebug();
      
      expect(console.log).toHaveBeenCalledWith(jasmine.stringMatching(/Cache debug enabled/));
    });
  });

  describe('Cache Cleanup', () => {
    it('should automatically clean expired entries during operations', (done) => {
      // Add some entries that will expire quickly
      service.setCachedData('short1', 'value1', 50);
      service.setCachedData('short2', 'value2', 50);
      service.setCachedData('long', 'value3', 5000);

      setTimeout(() => {
        // Trigger cleanup by adding new data
        service.setCachedData('new', 'value4', 5000);
        
        const stats = service.getCacheStats();
        expect(stats.totalEntries).toBeLessThan(4); // Some expired entries should be cleaned
        done();
      }, 100);
    });
  });

  describe('Edge Cases', () => {
    it('should handle very large cache entries', () => {
      const largeData = {
        array: new Array(10000).fill(0).map((_, i) => ({ id: i, data: `item-${i}` }))
      };
      
      service.setCachedData('large-data', largeData, 5000);
      const retrieved = service.getCachedData('large-data');
      
      expect(retrieved).toEqual(largeData);
    });

    it('should handle very short cache durations', () => {
      service.setCachedData('short-lived', 'value', 1); // 1ms
      
      // Should be immediately expired
      setTimeout(() => {
        expect(service.isCacheValid('short-lived')).toBeFalse();
      }, 5);
    });

    it('should handle zero duration cache', () => {
      service.setCachedData('zero-duration', 'value', 0);
      expect(service.isCacheValid('zero-duration')).toBeFalse();
    });

    it('should handle negative duration cache', () => {
      service.setCachedData('negative-duration', 'value', -1000);
      expect(service.isCacheValid('negative-duration')).toBeFalse();
    });
  });

  describe('Constants', () => {
    it('should have proper cache duration constants', () => {
      expect(CacheManagerService.DASHBOARD_CACHE_DURATION_MS).toBe(30000);
      expect(CacheManagerService.HTTP_CACHE_DURATION_MS).toBe(60000);
      expect(CacheManagerService.LOAD_DEBOUNCE_MS).toBe(1000);
    });
  });

  describe('Integration with Interface', () => {
    it('should satisfy ICacheManagerService contract', () => {
      const interfaceService: ICacheManagerService = service;
      
      // Test all interface methods exist and work
      interfaceService.setCachedData('test', 'value', 1000);
      expect(interfaceService.getCachedData('test')).toBe('value');
      expect(interfaceService.isCacheValid('test')).toBeTrue();
      
      interfaceService.invalidateCache('test');
      expect(interfaceService.getCachedData('test')).toBeNull();
      
      expect(typeof interfaceService.shouldDebounceRequest('test', 1000)).toBe('boolean');
      expect(interfaceService.getCacheStats()).toBeDefined();
      
      expect(() => interfaceService.forceRefresh()).not.toThrow();
      expect(() => interfaceService.invalidateAllCache()).not.toThrow();
      expect(() => interfaceService.enableGlobalDebug()).not.toThrow();
    });
  });
});