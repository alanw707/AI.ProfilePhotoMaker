import { Injectable } from '@angular/core';
import { forkJoin, of, firstValueFrom } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { StateBaseService } from './state-base.service';
import { CreditService, UserCreditStatus } from './credit.service';
import { CreditsInfo, ReplicateService } from './replicate.service';
import { ConfigService } from './config.service';
import { CacheManagerService } from './cache-manager.service';
import { NotificationService } from './notification.service';

export interface SubscriptionState {
  userCreditStatus: UserCreditStatus | null;
  creditsInfo: CreditsInfo | null;
  totalCredits: number;
  isPremiumWorkflow: boolean;
  isLoading: boolean;
}

/**
 * Service responsible for managing subscription, credits, and premium workflow state
 * Extracted from DashboardStateService for better separation of concerns
 */
@Injectable({
  providedIn: 'root',
})
export class SubscriptionStateService extends StateBaseService<SubscriptionState> {
  private readonly cacheKey = 'subscription_state_data';
  private readonly creditsCacheKey = 'credits_data';

  constructor(
    _cacheManager: CacheManagerService,
    _notificationService: NotificationService,
    private _creditService: CreditService,
    private _replicateService: ReplicateService,
    private _configService: ConfigService
  ) {
    super(
      {
        userCreditStatus: null,
        creditsInfo: null,
        totalCredits: 0,
        isPremiumWorkflow: false,
        isLoading: false,
      },
      _cacheManager,
      _notificationService
    );
  }

  /**
   * Load full subscription data including internal and external credits
   */
  async loadFullSubscriptionData(): Promise<void> {
    const startTime = performance.now();

    // Check cache first
    const cachedData = this.getCachedData<SubscriptionState>(this.cacheKey);

    if (cachedData?.userCreditStatus) {
      this.setState(cachedData);
      return;
    }

    // Debounce rapid reloads
    if (this.shouldDebounceRequest('subscription_load')) {
      return;
    }

    this.setLoading(true);

    try {
      const apiCalls: Record<string, any> = {
        creditStatus: this._creditService.getCreditStatus().pipe(
          catchError(error => {
            return of({ success: false, data: null, error });
          })
        ),
      };

      // Only call Replicate credits API if enabled in environment
      if (this._configService.isReplicateCreditsEnabled) {
        apiCalls['credits'] = this._replicateService.getCredits().pipe(
          catchError(error => {
            return of({ success: false, data: null, error });
          })
        );
      } else {
        apiCalls['credits'] = of({ success: false, data: null, error: 'disabled' });
      }

      const result = await firstValueFrom(forkJoin(apiCalls));
      if (!result) {
        throw new Error('Failed to load subscription data');
      }
      const { creditStatus, credits } = result;

      const userCreditStatus = (creditStatus as any)?.success ? (creditStatus as any).data : null;
      const creditsInfo = (credits as any)?.success ? (credits as any).data : null;

      // Calculate total credits
      const totalCredits = this._creditService.getTotalAvailableCredits(
        userCreditStatus,
        creditsInfo || null
      );

      // Determine premium workflow status
      const isPremiumWorkflow = (userCreditStatus?.purchasedCredits || 0) > 0;

      const newState: SubscriptionState = {
        userCreditStatus,
        creditsInfo,
        totalCredits,
        isPremiumWorkflow,
        isLoading: false,
      };

      this.setState(newState);

      // Cache the subscription data
      this.setCachedData(this.cacheKey, newState);

      // Show info if credits API failed but internal credits loaded
      if (
        !(credits as any)?.success &&
        this._configService.isReplicateCreditsEnabled &&
        (creditStatus as any)?.success
      ) {
        // Could add notification about external credits not available
        console.info('External credits API not available, using internal credits only');
      }

      this.logPerformance('Subscription data loaded', startTime);
    } catch (error) {
      this.handleApiError(error, 'Load Subscription Data');
      this.setLoading(false);
    }
  }

  /**
   * Load only internal credits (faster for settings/basic views)
   */
  async loadCreditsOnly(): Promise<void> {
    const startTime = performance.now();

    // Check cache first for internal credits
    const cachedData = this.getCachedData<{
      userCreditStatus: UserCreditStatus;
      totalCredits: number;
    }>(this.creditsCacheKey);

    if (cachedData?.userCreditStatus) {
      this.setState({
        userCreditStatus: cachedData.userCreditStatus,
        totalCredits: cachedData.totalCredits,
        isPremiumWorkflow: (cachedData.userCreditStatus?.purchasedCredits || 0) > 0,
        isLoading: false,
      });
      return;
    }

    // Debounce rapid reloads
    if (this.shouldDebounceRequest('credits_load')) {
      return;
    }

    this.setLoading(true);

    try {
      const creditStatus = await firstValueFrom(
        this._creditService.getCreditStatus().pipe(
          catchError(error => {
            return of({ success: false, data: null, error });
          })
        )
      );

      const userCreditStatus = creditStatus?.success ? creditStatus.data : null;

      // Calculate total credits from internal sources only
      const totalCredits = this._creditService.getTotalAvailableCredits(
        userCreditStatus,
        null // No Replicate credits
      );

      const isPremiumWorkflow = (userCreditStatus?.purchasedCredits || 0) > 0;

      // Set internal credits state only
      const newState = {
        userCreditStatus,
        totalCredits,
        isPremiumWorkflow,
        isLoading: false,
      };

      this.setState(newState);

      // Cache the internal credits data
      if (userCreditStatus) {
        this.setCachedData(
          this.creditsCacheKey,
          { userCreditStatus, totalCredits },
          CacheManagerService.DASHBOARD_CACHE_DURATION_MS
        );
      }

      this.logPerformance('Internal credits loaded', startTime);
    } catch (error) {
      this.handleApiError(error, 'Load Internal Credits');
      this.setLoading(false);
    }
  }

  /**
   * Refresh credit status after purchases or usage
   */
  async refreshCredits(): Promise<void> {
    this.invalidateCache(this.cacheKey);
    this.invalidateCache(this.creditsCacheKey);
    await this.loadFullSubscriptionData();
  }

  /**
   * Refresh only internal credits (weekly + purchased) without calling external providers
   */
  async refreshInternalCredits(): Promise<void> {
    this.forceRefreshCache(this.creditsCacheKey);
    this._cacheManager.forceRefresh('credits_load');
    await this.loadCreditsOnly();
  }

  /**
   * Check if user has enough credits for an operation
   */
  hasEnoughCredits(requiredCredits: number): boolean {
    const currentState = this.getState();
    return currentState.totalCredits >= requiredCredits;
  }

  /**
   * Get user's current credit balance
   */
  getCurrentCredits(): number {
    return this.getState().totalCredits;
  }

  /**
   * Check if user is on premium workflow
   */
  isPremiumUser(): boolean {
    return this.getState().isPremiumWorkflow;
  }

  /**
   * Get detailed credit breakdown
   */
  getCreditBreakdown(): {
    purchasedCredits: number;
    freeCredits: number;
    replicateCredits: number;
    totalCredits: number;
  } {
    const state = this.getState();
    const userCreditStatus = state.userCreditStatus;
    const creditsInfo = state.creditsInfo;

    return {
      purchasedCredits: userCreditStatus?.purchasedCredits || 0,
      freeCredits: userCreditStatus?.weeklyCredits || 0, // weeklyCredits represents free credits
      replicateCredits: creditsInfo?.availableCredits || 0,
      totalCredits: state.totalCredits,
    };
  }

  /**
   * Update credit status after an operation (decrease credits)
   */
  updateCreditsAfterUsage(creditsUsed: number): void {
    const currentState = this.getState();
    const newTotalCredits = Math.max(0, currentState.totalCredits - creditsUsed);

    this.setState({
      totalCredits: newTotalCredits,
    });

    // Invalidate cache to ensure fresh data on next load
    this.invalidateCache(this.cacheKey);
    this.invalidateCache(this.creditsCacheKey);
  }

  /**
   * Check if credits need refreshing based on last update time
   */
  shouldRefreshCredits(_maxAgeMinutes = 15): boolean {
    // This could be enhanced to track last refresh time
    // For now, we'll use cache validity
    return !this.getCachedData<SubscriptionState>(this.cacheKey);
  }

  /**
   * Force refresh implementation
   */
  forceRefresh(): void {
    this.forceRefreshCache(this.cacheKey);
    this.forceRefreshCache(this.creditsCacheKey);
    this.loadFullSubscriptionData();
  }
}
