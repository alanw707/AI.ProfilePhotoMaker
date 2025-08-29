import { Injectable } from '@angular/core';
import { BehaviorSubject, forkJoin, of, firstValueFrom } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ProfileService } from './profile.service';
import { ReplicateService } from './replicate.service';
import { CreditService } from './credit.service';
import { FileUploadService } from './file-upload.service';
import { StyleService } from './style.service';
import { NotificationService } from './notification.service';
import { CacheManagerService } from './cache-manager.service';
import { ModelStateService } from './model-state.service';
import { FallbackOperationsService } from './fallback-operations.service';
import { ImageValidationService } from './image-validation.service';
import { ConfigService } from './config.service';
import { SubscriptionStateService } from './subscription-state.service';
import { LoggingService, LogLevel } from './logging.service';
import { environment } from '../../environments/environment';
import {
  DashboardState,
  IDashboardStateService,
  UploadedImageThumbnail,
} from '../interfaces/service.interfaces';
import { ModelStatus } from '../models/dashboard.types';

@Injectable({
  providedIn: 'root',
})
export class DashboardStateService implements IDashboardStateService {
  private readonly _initialState: DashboardState = {
    userProfile: null,
    creditsInfo: null,
    userCreditStatus: null,
    uploadedImages: 0,
    uploadedImageThumbnails: [],
    generatedPhotosCount: 0,
    modelStatus: 'Not Started',
    isPremiumWorkflow: false,
    isLoading: false,
    latestTrainedModel: null,
    imagesValidated: false,
    lastValidationTime: 0,
  };

  private readonly _state = new BehaviorSubject<DashboardState>(this._initialState);
  readonly state$ = this._state.asObservable();

  constructor(
    private _profileService: ProfileService,
    private _replicateService: ReplicateService,
    private _creditService: CreditService,
    private _fileUploadService: FileUploadService,
    private _styleService: StyleService,
    private _notificationService: NotificationService,
    private _cacheManager: CacheManagerService,
    private _modelState: ModelStateService,
    private _fallbackOps: FallbackOperationsService,
    private _imageValidation: ImageValidationService,
    private _configService: ConfigService,
    private _subscriptionState: SubscriptionStateService,
    private _logger: LoggingService
  ) {}

  getState(): DashboardState {
    return this._state.getValue();
  }

  setState(newState: Partial<DashboardState>) {
    this._state.next({
      ...this.getState(),
      ...newState,
    });
  }

  // Load basic dashboard data for settings page (counts only, no validation)
  loadBasicDataForSettings() {
    const CACHE_KEY = 'settings_data';

    // Check cache first
    const cachedData = this._cacheManager.getCachedData<{
      userProfile: any;
      userCreditStatus: any;
      uploadedImages: number;
      generatedPhotosCount: number;
    }>(CACHE_KEY);
    if (cachedData) {
      this.setState({
        userProfile: cachedData.userProfile,
        userCreditStatus: cachedData.userCreditStatus,
        uploadedImages: cachedData.uploadedImages,
        generatedPhotosCount: cachedData.generatedPhotosCount,
        isLoading: false,
      });
      return;
    }

    // Debounce rapid reloads
    if (
      this._cacheManager.shouldDebounceRequest(
        'settings_load',
        CacheManagerService.LOAD_DEBOUNCE_MS
      )
    ) {
      return;
    }

    this.setState({ isLoading: true });

    // Load only essential data for settings page
    forkJoin({
      profile: this._profileService.getCurrentUserProfile().pipe(
        catchError(_error => {
          return of({ success: false, data: null, error: _error });
        })
      ),
      creditStatus: this._creditService.getCreditStatus().pipe(
        catchError(_error => {
          return of({ success: false, data: null, error: _error });
        })
      ),
      userImages: this._fileUploadService.getUserImages().pipe(
        catchError(_error => {
          return of({ success: false, data: null, error: _error });
        })
      ),
    }).subscribe({
      next: ({ profile, creditStatus, userImages }) => {
        const userProfile = profile?.success ? profile.data : null;
        const userCreditStatus = creditStatus?.success ? creditStatus.data : null;
        const userImagesData = userImages?.success ? userImages.data : null;

        // Count uploaded images without validation (just filter server data)
        const uploadedImagesCount =
          userImagesData?.images?.filter(
            (img: any) => !img.isGenerated && (img.isOriginalUpload || img.style === 'Original')
          )?.length || 0;

        // Count generated photos
        const generatedPhotosCount =
          userImagesData?.generatedImages ||
          userImagesData?.images?.filter((img: any) => img.isGenerated)?.length ||
          0;

        const newState = {
          userProfile,
          userCreditStatus,
          uploadedImages: uploadedImagesCount,
          generatedPhotosCount,
          isLoading: false,
        };

        this.setState(newState);

        // Cache the settings data
        this._cacheManager.setCachedData(
          CACHE_KEY,
          newState,
          CacheManagerService.DASHBOARD_CACHE_DURATION_MS
        );
      },
      error: _error => {
        this.setState({ isLoading: false });
      },
    });
  }

  // Load internal credits only (no Replicate credits, no image validation)
  loadCreditsOnly() {
    const CACHE_KEY = 'credits_data';

    // Check cache first for internal credits
    const cachedData = this._cacheManager.getCachedData<{
      userCreditStatus: any;
      totalCredits: number;
    }>(CACHE_KEY);
    if (cachedData?.userCreditStatus) {
      this.setState({
        userCreditStatus: cachedData.userCreditStatus,
        totalCredits: cachedData.totalCredits,
        isLoading: false,
      });
      return;
    }

    // Debounce rapid reloads
    if (
      this._cacheManager.shouldDebounceRequest('credits_load', CacheManagerService.LOAD_DEBOUNCE_MS)
    ) {
      return;
    }

    this.setState({ isLoading: true });

    // Load ONLY internal credit status - no Replicate credits
    this._creditService
      .getCreditStatus()
      .pipe(
        catchError(_error => {
          return of({ success: false, data: null, error: _error });
        })
      )
      .subscribe({
        next: creditStatus => {
          const userCreditStatus = creditStatus?.success ? creditStatus.data : null;

          // Calculate total credits from internal sources only
          const totalCredits = this._creditService.getTotalAvailableCredits(
            userCreditStatus,
            null // No Replicate credits
          );

          // Set internal credits state only
          const newState = {
            userCreditStatus,
            totalCredits,
            isLoading: false,
          };

          this.setState(newState);

          // Cache the internal credits data
          if (userCreditStatus) {
            this._cacheManager.setCachedData(
              CACHE_KEY,
              { userCreditStatus, totalCredits },
              CacheManagerService.DASHBOARD_CACHE_DURATION_MS
            );
          }
        },
        error: _error => {
          this.setState({ isLoading: false });
        },
      });
  }

  async loadInitialDashboardData() {
    const CACHE_KEY = 'dashboard_data';

    // 🔧 FIX: Load subscription data using dedicated service first
    await this._subscriptionState.loadFullSubscriptionData();

    // Get the loaded subscription state
    const subscriptionData = this._subscriptionState.getState();

    // Check cache first for non-credit data
    const cachedData = this._cacheManager.getCachedData<DashboardState>(CACHE_KEY);
    if (cachedData?.userProfile) {
      // Merge cached data with fresh subscription data
      const mergedState = {
        ...cachedData,
        userCreditStatus: subscriptionData.userCreditStatus,
        creditsInfo: subscriptionData.creditsInfo,
        totalCredits: subscriptionData.totalCredits,
        isPremiumWorkflow: subscriptionData.isPremiumWorkflow,
        isLoading: false,
      };
      this.setState(mergedState);

      // Always validate images even from cache to ensure broken images are cleaned up
      if (cachedData.uploadedImageThumbnails && cachedData.uploadedImageThumbnails.length > 0) {
        this._validateCachedImages(cachedData.uploadedImageThumbnails);
      }
      return;
    }

    // Debounce rapid reloads
    if (
      this._cacheManager.shouldDebounceRequest(
        'dashboard_load',
        CacheManagerService.LOAD_DEBOUNCE_MS
      )
    ) {
      return;
    }

    this.setState({ isLoading: true });
    // Performance tracking disabled
    // const loadStartTime = performance.now();

    // 🔧 FIX: Load only non-credit data since subscription data is already loaded
    const apiCalls: any = {
      profile: this._profileService.getCurrentUserProfile().pipe(
        catchError(_error => {
          return of({ success: false, data: null, error: _error });
        })
      ),
      userImages: this._fileUploadService.getUserImages().pipe(
        catchError(_error => {
          return of({ success: false, data: null, error: _error });
        })
      ),
    };

    // Skip credit API calls since we already loaded subscription data

    forkJoin(apiCalls).subscribe({
      next: (result: any) => {
        const { profile, userImages } = result;

        const userProfile = profile?.success ? profile.data : null;
        // 🔧 FIX: Use subscription service data instead of API calls
        const userCreditStatus = subscriptionData.userCreditStatus;
        const creditsInfo = subscriptionData.creditsInfo;

        // Process uploaded images into thumbnails format
        const userImagesData = userImages?.success ? userImages.data : null;

        // Process and validate uploaded images with robust filtering
        const rawImageThumbnails: UploadedImageThumbnail[] =
          userImagesData?.images
            ?.filter((img: any) => {
              // Early exit: Skip generated images entirely (no logging, no processing)
              if (img.isGenerated) {
                return false;
              }

              // Only process uploaded images from here
              const isOriginalByFlag = img.isOriginalUpload;
              // Secondary check: Use style as fallback for corrupted flags
              const isOriginalByStyle = img.style === 'Original';
              // Must have a valid URL
              const hasUrl = !!img.originalImageUrl;

              // Robust filtering: Image is considered uploaded selfie if:
              // 1. Has IsOriginalUpload flag true, OR
              // 2. Has "Original" style (even if flag is corrupted), AND
              // 3. Has a valid URL, AND
              // 4. Is NOT an enhanced image
              const isUploadedImage = (isOriginalByFlag || isOriginalByStyle) && hasUrl;

              // Only log uploaded images (much cleaner console)

              // Skip logging flag/style mismatch - handled by image validation service

              return isUploadedImage;
            })
            ?.map((img: any) => ({
              id: img.id,
              url: img.originalImageUrl,
              fileName: `Image ${img.id}`, // Use a default filename since it's not in ProcessedImage
            })) || [];

        // Clean URLs immediately to prevent browser caching issues
        const uploadedImageThumbnails = rawImageThumbnails.map(thumb => ({
          ...thumb,
          url: this._cleanImageUrl(thumb.url),
        }));

        // Validate images asynchronously after state is set with clean URLs
        if (uploadedImageThumbnails.length > 0) {
          this._validateAndCleanupImages(uploadedImageThumbnails, false).then(result => {
            if (result.removedCount > 0) {
              this._updateStateWithValidatedImages(
                result.validImages,
                result.removedCount,
                result.repairTriggered
              );
            }
          });
        }

        // Count generated photos (use API count or filter generated images)
        const generatedPhotosCount =
          userImagesData?.generatedImages ||
          userImagesData?.images?.filter((img: any) => img.isGenerated)?.length ||
          0;

        // Check if we need immediate filesystem repair
        if (generatedPhotosCount === 0 && uploadedImageThumbnails.length > 0) {
          // TODO: Add filesystem repair logic if needed
        }

        // 🔧 FIX: Use total credits from subscription service
        const totalCredits = subscriptionData.totalCredits;

        // Set initial state with critical data for fast render
        const newState = {
          userProfile,
          userCreditStatus,
          creditsInfo,
          uploadedImages: uploadedImageThumbnails.length,
          uploadedImageThumbnails,
          generatedPhotosCount,
          modelStatus: 'Loading...', // Temporary status
          isPremiumWorkflow: subscriptionData.isPremiumWorkflow,
          isLoading: false,
          totalCredits,
        };

        this.setState(newState);

        // Cache the loaded data
        this._cacheManager.setCachedData(
          'dashboard_data',
          this.getState(),
          CacheManagerService.DASHBOARD_CACHE_DURATION_MS
        );
        // Load time tracking for performance monitoring
        // const loadTime = performance.now() - loadStartTime;

        // Load remaining data asynchronously (non-blocking)
        this._loadRemainingDataAsync();

        // Set a timeout to avoid infinite "Loading..." state
        setTimeout(() => {
          const currentState = this.getState();
          if (currentState.modelStatus === 'Loading...') {
            this.setState({ modelStatus: 'Not Started' });
          }
        }, 10000);
      },
      error: _error => {
        // Log only critical errors (500+ status codes)
        if (_error?.status >= 500 || !_error?.status) {
          this._logger.error('Dashboard API call failed', _error);
        }
        this._notificationService.error(
          'Dashboard Load Failed',
          'Could not load dashboard data. Please try again.'
        );
        this.setState({
          isLoading: false,
          modelStatus: 'Error',
        });
      },
    });
  }

  resetState() {
    this._state.next(this._initialState);
    this._cacheManager.invalidateCache('dashboard_data');
  }

  // Force refresh by clearing cache and reloading
  forceRefresh() {
    this._cacheManager.forceRefresh('dashboard_data');
    this._fallbackOps.resetFallbackTracking();
    this._fileUploadService.invalidateUserImagesCache();
    this._imageValidation.clearCache(); // Clear image validation cache too

    // Reset validation state to force re-validation
    this.setState({
      imagesValidated: false,
      lastValidationTime: 0,
    });

    this.loadInitialDashboardData();
  }

  // Force refresh after repair to sync counts with server reality
  private async _forceRefreshAfterRepair(): Promise<void> {
    try {
      // Clear all caches to ensure fresh data
      this._cacheManager.forceRefresh('dashboard_data');
      this._fileUploadService.invalidateUserImagesCache();
      this._imageValidation.clearCache();

      // Get fresh data from server
      const userImages = await firstValueFrom(this._fileUploadService.getUserImages(true));

      if (userImages?.success && userImages.data) {
        const userImagesData = userImages.data;

        // Process images with robust validation (same logic as initial load)
        const uploadedImageThumbnails: UploadedImageThumbnail[] =
          userImagesData.images
            ?.filter(img => {
              const isOriginalByFlag = img.isOriginalUpload;
              const isOriginalByStyle = img.style === 'Original';
              const isNotGenerated = !img.isGenerated;
              const hasUrl = !!img.originalImageUrl;
              return (isOriginalByFlag || isOriginalByStyle) && isNotGenerated && hasUrl;
            })
            ?.map(img => ({
              id: img.id,
              url: img.originalImageUrl,
              fileName: `Image ${img.id}`,
            })) || [];

        // Update state with accurate counts - force validation of fresh data
        this.setState({
          uploadedImageThumbnails,
          uploadedImages: uploadedImageThumbnails.length,
          imagesValidated: false, // Force validation to run on fresh data
          lastValidationTime: 0,
        });
      }
    } catch {
      // Silent failure - repair issues are handled by parent caller
    }
  }

  // Sync user images cache with current state
  invalidateAndRefreshImages() {
    this._fileUploadService.invalidateUserImagesCache();
    this.refreshGeneratedPhotosCount();
  }

  // Load remaining data asynchronously after initial render
  private _loadRemainingDataAsync() {
    // Load non-critical data in parallel but don't block rendering
    forkJoin({
      trainingStatus: this._fileUploadService.getTrainingStatus().pipe(
        catchError(_error => {
          return of(null); // Return null for failed training status
        })
      ),
      modelRequests: this._fileUploadService.getUserModelRequests().pipe(
        catchError(_error => {
          return of({ success: false, data: null, error: _error });
        })
      ),
    }).subscribe({
      next: ({ trainingStatus, modelRequests }) => {
        const currentState = this.getState();
        const modelRequestsData = modelRequests?.success ? modelRequests.data : null;

        // Training status is either TrainingStatusResponse or null
        const trainingStatusData = trainingStatus; // Use directly since it's not wrapped

        // Get enhanced model status with semantic status
        const modelInfo = this._modelState.getEnhancedModelStatusFromData(
          modelRequestsData,
          trainingStatusData
        );
        const { modelStatus, modelStatusSemantic, latestTrainedModel } = modelInfo;

        // Update state with additional data
        this.setState({
          uploadedImages: trainingStatusData?.totalUploadedImages || currentState.uploadedImages,
          modelStatus,
          modelStatusSemantic,
          latestTrainedModel,
        });

        // Reset fallback tracking periodically
        this._fallbackOps.resetFallbackTracking();

        // Check if fallback operations are needed
        const fallbackCheck = this._fallbackOps.checkIfFallbackNeeded({
          generatedPhotosCount: currentState.generatedPhotosCount,
          modelStatus,
          hasLatestTrainedModel: !!latestTrainedModel,
          uploadedImages: currentState.uploadedImages,
          hasUserProfile: !!currentState.userProfile,
          latestTrainedModel,
        });

        // Execute fallback operations if needed
        if (fallbackCheck.shouldCheckFilesystem) {
          // Skip filesystem check as the backend endpoint doesn't exist
          this._fallbackOps.markFilesystemCheckCompleted();
        }

        if (fallbackCheck.shouldDiscoverModels) {
          this._modelState.runAsyncModelDiscovery();
        }
      },
      error: _error => {
        // Set fallback model status on async load failure
        const currentState = this.getState();
        if (currentState.modelStatus === 'Loading...') {
          this.setState({ modelStatus: 'Not Started' });
        }
        // Don't show error to user since initial data loaded successfully
      },
    });
  }

  // Removed sync workarounds - ModelCreationRequest is now the single source of truth

  // Refresh only the photos count after generation completion
  refreshGeneratedPhotosCount() {
    this._fileUploadService
      .getUserImages()
      .pipe(
        catchError(_error => {
          return of({ success: false, data: null, error: _error });
        })
      )
      .subscribe({
        next: userImages => {
          const userImagesData = userImages?.success ? userImages.data : null;
          const generatedPhotosCount =
            userImagesData?.generatedImages ||
            userImagesData?.images?.filter(img => img.isGenerated)?.length ||
            0;

          this.setState({ generatedPhotosCount });
        },
        error: _error => {
          // Silent failure for photo count refresh
        },
      });
  }

  // Validate cached images and update state if needed
  private async _validateCachedImages(images: UploadedImageThumbnail[]): Promise<void> {
    // Check if validation is recent enough (5 minutes)
    const currentState = this.getState();
    const validationAge = Date.now() - (currentState.lastValidationTime || 0);
    const VALIDATION_TTL = 5 * 60 * 1000; // 5 minutes

    if (currentState.imagesValidated && validationAge < VALIDATION_TTL) {
      return;
    }

    const result = await this._validateAndCleanupImages(images, true);

    if (result.removedCount > 0) {
      this._updateStateWithValidatedImages(
        result.validImages,
        result.removedCount,
        result.repairTriggered
      );
    } else {
      // Mark as validated even if no cleanup needed
      this.setState({
        imagesValidated: true,
        lastValidationTime: Date.now(),
      });
    }
  }

  // Validate and cleanup broken images
  private async _validateAndCleanupImages(
    images: UploadedImageThumbnail[],
    _isFromCache: boolean
  ): Promise<{
    validImages: UploadedImageThumbnail[];
    removedCount: number;
    repairTriggered?: boolean;
  }> {
    // Check if image validation is disabled via environment configuration
    if (!this._configService.isImageValidationEnabled) {
      return {
        validImages: images, // Return all images as valid
        removedCount: 0,
        repairTriggered: false,
      };
    }

    const validation = await this._imageValidation.filterValidImages(images);

    if (validation.removedCount > 0 && this._shouldTriggerAutoRepair(validation)) {
      return await this._attemptAutoRepair(validation);
    }

    return {
      validImages: validation.validImages,
      removedCount: validation.removedCount,
      repairTriggered: false,
    };
  }

  // Update state with validated images and invalidate cache if needed
  private _updateStateWithValidatedImages(
    validImages: UploadedImageThumbnail[],
    removedCount: number,
    repairTriggered?: boolean
  ): void {
    this.setState({
      uploadedImageThumbnails: validImages,
      uploadedImages: validImages.length,
      imagesValidated: true,
      lastValidationTime: Date.now(),
    });

    // Invalidate cache since we have new data
    this._cacheManager.invalidateCache('dashboard_data');

    // Show notification if images were removed
    if (removedCount > 0) {
      const message = repairTriggered
        ? `Removed ${removedCount} broken image reference(s) and repaired database inconsistencies.`
        : `Removed ${removedCount} broken image reference(s) that could no longer be loaded.`;

      this._notificationService.info(
        repairTriggered ? 'Cleaned up & Repaired' : 'Cleaned up broken images',
        message
      );
    }
  }

  // Clean image URL to remove fragments and cache-busting artifacts
  private _cleanImageUrl(url: string): string {
    try {
      // Remove URL fragments (#) and normalize
      const cleanUrl = url.split('#')[0].split('?')[0];

      // Ensure proper leading slash for relative URLs
      if (!cleanUrl.startsWith('http') && !cleanUrl.startsWith('/')) {
        return '/' + cleanUrl;
      }

      return cleanUrl;
    } catch {
      return url;
    }
  }

  // Public method for manual validation (debug purposes)
  async validateCurrentImages(): Promise<void> {
    const currentState = this.getState();
    const images = currentState.uploadedImageThumbnails;

    if (images.length === 0) {
      return;
    }

    const result = await this._validateAndCleanupImages(images, false);

    if (result.removedCount > 0) {
      this._updateStateWithValidatedImages(
        result.validImages,
        result.removedCount,
        result.repairTriggered
      );

      const message = result.repairTriggered
        ? `Validated ${images.length} images. Removed ${result.removedCount} broken references and repaired database.`
        : `Validated ${images.length} images. Removed ${result.removedCount} broken references.`;

      this._notificationService.info(
        result.repairTriggered ? 'Validation & Repair Complete' : 'Image Validation Complete',
        message
      );
    } else {
      // Mark as validated
      this.setState({
        imagesValidated: true,
        lastValidationTime: Date.now(),
      });

      this._notificationService.success(
        'All Images Valid',
        'All uploaded images are accessible and valid.'
      );
    }
  }

  // NEW: Enhanced Auto-Repair Helper Methods with Safety Guards

  /**
   * Check if auto-repair should be triggered based on safety conditions
   * @param validation - The validation result containing broken image counts
   * @returns true if auto-repair should proceed, false if safety conditions not met
   */
  private _shouldTriggerAutoRepair(validation: any): boolean {
    const enableAutoRepairDebug = environment.features.logging?.enableAutoRepairDebug ?? false;

    // Check master feature flag
    if (!this._configService.isAutoRepairEnabled) {
      this._logger.conditionalLog(
        enableAutoRepairDebug,
        LogLevel.DEBUG,
        'Auto-repair disabled by feature flag'
      );
      return false;
    }

    // Check threshold requirement
    if (validation.notFoundCount < this._configService.autoRepairThreshold) {
      this._logger.conditionalLog(
        enableAutoRepairDebug,
        LogLevel.DEBUG,
        'Auto-repair threshold not met',
        {
          notFoundCount: validation.notFoundCount,
          threshold: this._configService.autoRepairThreshold,
        }
      );
      return false;
    }

    // Check cooldown period
    const lastRepairKey = 'lastAutoRepairTime';
    const lastRepair = localStorage.getItem(lastRepairKey);
    if (lastRepair) {
      const timeSinceLastRepair = Date.now() - (parseInt(lastRepair) || 0);
      if (timeSinceLastRepair < this._configService.autoRepairCooldownMs) {
        const remainingCooldown = Math.ceil(
          (this._configService.autoRepairCooldownMs - timeSinceLastRepair) / (60 * 1000)
        );
        this._logger.conditionalLog(
          enableAutoRepairDebug,
          LogLevel.DEBUG,
          `Auto-repair in cooldown: ${remainingCooldown} minutes remaining`
        );
        return false;
      }
    }

    // Check session attempt limit
    const sessionAttemptsKey = 'sessionAutoRepairAttempts';
    const sessionAttempts = parseInt(sessionStorage.getItem(sessionAttemptsKey) || '0') || 0;
    if (sessionAttempts >= this._configService.autoRepairMaxAttempts) {
      this._logger.conditionalLog(
        enableAutoRepairDebug,
        LogLevel.DEBUG,
        'Auto-repair max attempts reached',
        { attempts: sessionAttempts, maxAttempts: this._configService.autoRepairMaxAttempts }
      );
      return false;
    }

    // Check if repair is suggested by validation logic
    if (!validation.repairSuggested || validation.notFoundCount <= 0) {
      this._logger.conditionalLog(
        enableAutoRepairDebug,
        LogLevel.DEBUG,
        'Auto-repair not suggested by validation logic'
      );
      return false;
    }

    this._logger.conditionalLog(
      enableAutoRepairDebug,
      LogLevel.DEBUG,
      'Auto-repair conditions met',
      { brokenImages: validation.notFoundCount }
    );
    return true;
  }

  /**
   * Attempt auto-repair with comprehensive safety measures and logging
   * @param validation - The validation result containing broken image information
   * @returns Enhanced validation result with repair status
   */
  private async _attemptAutoRepair(validation: any): Promise<any> {
    const enableAutoRepairDebug = environment.features.logging?.enableAutoRepairDebug ?? false;

    try {
      this._logger.conditionalLog(enableAutoRepairDebug, LogLevel.DEBUG, 'Auto-repair triggered', {
        brokenReferences: validation.notFoundCount,
      });

      // Record repair attempt
      const now = Date.now();
      localStorage.setItem('lastAutoRepairTime', now.toString());

      // Increment session attempts counter
      const sessionAttemptsKey = 'sessionAutoRepairAttempts';
      const currentAttempts =
        (parseInt(sessionStorage.getItem(sessionAttemptsKey) || '0') || 0) + 1;
      sessionStorage.setItem(sessionAttemptsKey, currentAttempts.toString());

      // Check dry-run mode
      if (this._configService.isAutoRepairDryRunOnly) {
        this._logger.conditionalLog(
          enableAutoRepairDebug,
          LogLevel.DEBUG,
          'Auto-repair in DRY-RUN mode - no actual changes will be made'
        );

        if (this._configService.isAutoRepairNotificationsEnabled) {
          this._notificationService.info(
            'Auto-Repair Simulation',
            `Would repair ${validation.notFoundCount} broken image references (DRY-RUN mode)`
          );
        }

        return {
          validImages: validation.validImages,
          removedCount: validation.removedCount,
          repairTriggered: false,
          dryRunMode: true,
        };
      }

      // Perform actual repair
      this._logger.conditionalLog(
        enableAutoRepairDebug,
        LogLevel.DEBUG,
        'Executing auto-repair...'
      );
      const repairResult = await firstValueFrom(this._fileUploadService.repairImageDatabase());

      if (repairResult?.success) {
        this._logger.info('Auto-repair completed successfully');

        if (this._configService.isAutoRepairNotificationsEnabled) {
          this._notificationService.success(
            'Auto-Repair Complete',
            `Successfully repaired ${validation.notFoundCount} broken image references`
          );
        }

        // Force refresh to get accurate data after repair
        await this._forceRefreshAfterRepair();

        return {
          validImages: validation.validImages,
          removedCount: validation.removedCount,
          repairTriggered: true,
          repairSuccess: true,
        };
      } else {
        this._logger.warn('Auto-repair completed but returned no success indicator');
        return {
          validImages: validation.validImages,
          removedCount: validation.removedCount,
          repairTriggered: false,
          repairError: 'No success indicator returned',
        };
      }
    } catch (error) {
      this._logger.error('Auto-repair failed', error);

      // Send error telemetry if enabled
      if (this._configService.isAutoRepairTelemetryEnabled) {
        this._trackRepairError(error, validation);
      }

      if (this._configService.isAutoRepairNotificationsEnabled) {
        this._notificationService.error(
          'Auto-Repair Failed',
          'Image repair encountered an error. Please try manual refresh.'
        );
      }

      return {
        validImages: validation.validImages,
        removedCount: validation.removedCount,
        repairTriggered: false,
        repairError: error,
      };
    }
  }

  /**
   * Track auto-repair errors for telemetry and debugging
   * @param error - The error that occurred during repair
   * @param validation - The validation context when error occurred
   */
  private _trackRepairError(error: any, validation: any): void {
    const errorData = {
      timestamp: new Date().toISOString(),
      errorMessage: error?.message || 'Unknown error',
      validationContext: {
        notFoundCount: validation.notFoundCount,
        removedCount: validation.removedCount,
        repairSuggested: validation.repairSuggested,
      },
      environment: this._configService.getCurrentEnvironment(),
      validationLevel: this._configService.autoRepairValidationLevel,
    };

    this._logger.warn('Auto-repair error telemetry', errorData);

    // Future: Send to analytics service
    // this._analyticsService.trackError('auto-repair-failed', errorData);
  }
}
