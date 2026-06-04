import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { CacheManagerService } from './cache-manager.service';
import { NotificationService } from './notification.service';

/**
 * Base service for state management with common patterns
 * Provides reactive state management, caching, and error handling
 */
@Injectable({
  providedIn: 'root',
})
export abstract class StateBaseService<T> {
  protected readonly _state: BehaviorSubject<T>;
  public readonly state$: Observable<T>;

  constructor(
    protected _initialState: T,
    protected _cacheManager: CacheManagerService,
    protected _notificationService: NotificationService
  ) {
    this._state = new BehaviorSubject<T>(this._initialState);
    this.state$ = this._state.asObservable();
  }

  /**
   * Get current state snapshot
   */
  getState(): T {
    return this._state.getValue();
  }

  /**
   * Update state with partial changes
   */
  setState(newState: Partial<T>): void {
    this._state.next({
      ...this.getState(),
      ...newState,
    });
  }

  /**
   * Reset state to initial values
   */
  resetState(): void {
    this._state.next(this._initialState);
  }

  /**
   * Check if loading should be debounced
   */
  protected shouldDebounceRequest(
    key: string,
    debounceMs: number = CacheManagerService.LOAD_DEBOUNCE_MS
  ): boolean {
    return this._cacheManager.shouldDebounceRequest(key, debounceMs);
  }

  /**
   * Get cached data with type safety
   */
  protected getCachedData<TCache>(key: string): TCache | null {
    return this._cacheManager.getCachedData<TCache>(key);
  }

  /**
   * Set cached data with TTL
   */
  protected setCachedData<TCache>(
    key: string,
    data: TCache,
    ttl: number = CacheManagerService.WORKSPACE_CACHE_DURATION_MS
  ): void {
    this._cacheManager.setCachedData(key, data, ttl);
  }

  /**
   * Invalidate specific cache key
   */
  protected invalidateCache(key: string): void {
    this._cacheManager.invalidateCache(key);
  }

  /**
   * Force refresh by clearing cache
   */
  protected forceRefreshCache(key: string): void {
    this._cacheManager.forceRefresh(key);
  }

  /**
   * Handle API errors consistently
   */
  protected handleApiError(error: any, operation: string, showToUser = true): void {
    // Log critical errors only to avoid console spam
    if (error?.status >= 500 || !error?.status) {
      console.error(`❌ ${operation} failed:`, error);
    }
    if (showToUser) {
      this._notificationService.error(
        `${operation} Failed`,
        'An error occurred while loading data. Please try again.'
      );
    }
  }

  /**
   * Show success notification
   */
  protected showSuccess(title: string, message: string): void {
    this._notificationService.success(title, message);
  }

  /**
   * Show info notification
   */
  protected showInfo(title: string, message: string): void {
    this._notificationService.info(title, message);
  }

  /**
   * Show warning notification
   */
  protected showWarning(title: string, message: string): void {
    this._notificationService.warning(title, message);
  }

  /**
   * Update loading state
   */
  protected setLoading(isLoading: boolean): void {
    // Cast to unknown first, then to Partial<T> to allow setting isLoading
    this.setState({ isLoading } as unknown as Partial<T>);
  }

  /**
   * Log performance timing (disabled for production)
   */
  protected logPerformance(_operation: string, _startTime: number): void {
    // Performance logging disabled for production build
    // const duration = performance.now() - startTime;
    // console.log(`⚡ ${operation} completed in ${duration.toFixed(2)}ms`);
  }

  /**
   * Abstract method for force refresh - implement in derived services
   */
  abstract forceRefresh(): void;
}
