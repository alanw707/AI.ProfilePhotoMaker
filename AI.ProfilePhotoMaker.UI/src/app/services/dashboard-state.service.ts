import { Injectable } from '@angular/core';
import { BehaviorSubject, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ProfileService, UserProfile } from './profile.service';
import { CreditsInfo, ReplicateService } from './replicate.service';
import { CreditService, UserCreditStatus } from './credit.service';
import { FileUploadService } from './file-upload.service';
import { StyleService } from './style.service';
import { NotificationService } from './notification.service';
import { CacheManagerService } from './cache-manager.service';
import { ModelStateService } from './model-state.service';
import { FallbackOperationsService } from './fallback-operations.service';
import { ImageValidationService } from './image-validation.service';
import { ConfigService } from './config.service';
import { SubscriptionStateService } from './subscription-state.service';
import {
  DashboardState,
  IDashboardStateService,
  UploadedImageThumbnail,
} from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root',
})
export class DashboardStateService implements IDashboardStateService {
  private readonly initialState: DashboardState = {
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

  private readonly _state = new BehaviorSubject<DashboardState>(this.initialState);
  readonly state$ = this._state.asObservable();

  constructor(
    private profileService: ProfileService,
    private replicateService: ReplicateService,
    private creditService: CreditService,
    private fileUploadService: FileUploadService,
    private styleService: StyleService,
    private notificationService: NotificationService,
    private cacheManager: CacheManagerService,
    private modelState: ModelStateService,
    private fallbackOps: FallbackOperationsService,
    private imageValidation: ImageValidationService,
    private configService: ConfigService,
    private subscriptionState: SubscriptionStateService
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
    const cachedData = this.cacheManager.getCachedData<{
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
      this.cacheManager.shouldDebounceRequest('settings_load', CacheManagerService.LOAD_DEBOUNCE_MS)
    ) {
      return;
    }

    this.setState({ isLoading: true });

    // Load only essential data for settings page
    forkJoin({
      profile: this.profileService.getCurrentUserProfile().pipe(
        catchError(error => {
          return of({ success: false, data: null, error });
        })
      ),
      creditStatus: this.creditService.getCreditStatus().pipe(
        catchError(error => {
          return of({ success: false, data: null, error });
        })
      ),
      userImages: this.fileUploadService.getUserImages().pipe(
        catchError(error => {
          return of({ success: false, data: null, error });
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
        this.cacheManager.setCachedData(
          CACHE_KEY,
          newState,
          CacheManagerService.DASHBOARD_CACHE_DURATION_MS
        );
      },
      error: error => {
        this.setState({ isLoading: false });
      },
    });
  }

  // Load internal credits only (no Replicate credits, no image validation)
  loadCreditsOnly() {
    const CACHE_KEY = 'credits_data';

    // Check cache first for internal credits
    const cachedData = this.cacheManager.getCachedData<{
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
      this.cacheManager.shouldDebounceRequest('credits_load', CacheManagerService.LOAD_DEBOUNCE_MS)
    ) {
      return;
    }

    this.setState({ isLoading: true });

    // Load ONLY internal credit status - no Replicate credits
    this.creditService
      .getCreditStatus()
      .pipe(
        catchError(error => {
          return of({ success: false, data: null, error });
        })
      )
      .subscribe({
        next: creditStatus => {
          const userCreditStatus = creditStatus?.success ? creditStatus.data : null;

          // Calculate total credits from internal sources only
          const totalCredits = this.creditService.getTotalAvailableCredits(
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
            this.cacheManager.setCachedData(
              CACHE_KEY,
              { userCreditStatus, totalCredits },
              CacheManagerService.DASHBOARD_CACHE_DURATION_MS
            );
          }
        },
        error: error => {
          this.setState({ isLoading: false });
        },
      });
  }

  async loadInitialDashboardData() {
    const CACHE_KEY = 'dashboard_data';

    // 🔧 FIX: Load subscription data using dedicated service first
    await this.subscriptionState.loadFullSubscriptionData();

    // Get the loaded subscription state
    const subscriptionData = this.subscriptionState.getState();

    // Check cache first for non-credit data
    const cachedData = this.cacheManager.getCachedData<DashboardState>(CACHE_KEY);
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
        this.validateCachedImages(cachedData.uploadedImageThumbnails);
      }
      return;
    }

    // Debounce rapid reloads
    if (
      this.cacheManager.shouldDebounceRequest(
        'dashboard_load',
        CacheManagerService.LOAD_DEBOUNCE_MS
      )
    ) {
      return;
    }

    this.setState({ isLoading: true });
    const loadStartTime = performance.now();

    // 🔧 FIX: Load only non-credit data since subscription data is already loaded
    const apiCalls: any = {
      profile: this.profileService.getCurrentUserProfile().pipe(
        catchError(error => {
          return of({ success: false, data: null, error });
        })
      ),
      userImages: this.fileUploadService.getUserImages().pipe(
        catchError(error => {
          return of({ success: false, data: null, error });
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
          url: this.cleanImageUrl(thumb.url),
        }));

        // Validate images asynchronously after state is set with clean URLs
        if (uploadedImageThumbnails.length > 0) {
          this.validateAndCleanupImages(uploadedImageThumbnails, false).then(result => {
            if (result.removedCount > 0) {
              this.updateStateWithValidatedImages(
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
        this.cacheManager.setCachedData(
          'dashboard_data',
          this.getState(),
          CacheManagerService.DASHBOARD_CACHE_DURATION_MS
        );
        const loadTime = performance.now() - loadStartTime;

        // Load remaining data asynchronously (non-blocking)
        this.loadRemainingDataAsync();

        // Set a timeout to avoid infinite "Loading..." state
        setTimeout(() => {
          const currentState = this.getState();
          if (currentState.modelStatus === 'Loading...') {
            this.setState({ modelStatus: 'Not Started' });
          }
        }, 10000);
      },
      error: error => {
        // Log only critical errors (500+ status codes)
        if (error?.status >= 500 || !error?.status) {
          console.error('❌ Dashboard API call failed:', error);
        }
        this.notificationService.error(
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
    this._state.next(this.initialState);
    this.cacheManager.invalidateCache('dashboard_data');
  }

  // Force refresh by clearing cache and reloading
  forceRefresh() {
    this.cacheManager.forceRefresh('dashboard_data');
    this.fallbackOps.resetFallbackTracking();
    this.fileUploadService.invalidateUserImagesCache();
    this.imageValidation.clearCache(); // Clear image validation cache too

    // Reset validation state to force re-validation
    this.setState({
      imagesValidated: false,
      lastValidationTime: 0,
    });

    this.loadInitialDashboardData();
  }

  // Force refresh after repair to sync counts with server reality
  private async forceRefreshAfterRepair(): Promise<void> {
    try {
      // Clear all caches to ensure fresh data
      this.cacheManager.forceRefresh('dashboard_data');
      this.fileUploadService.invalidateUserImagesCache();
      this.imageValidation.clearCache();

      // Get fresh data from server
      const userImages = await this.fileUploadService.getUserImages(true).toPromise();

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
    } catch (error) {
      // Silent failure - repair issues are handled by parent caller
    }
  }

  // Sync user images cache with current state
  invalidateAndRefreshImages() {
    this.fileUploadService.invalidateUserImagesCache();
    this.refreshGeneratedPhotosCount();
  }

  // Load remaining data asynchronously after initial render
  private loadRemainingDataAsync() {
    // Load non-critical data in parallel but don't block rendering
    forkJoin({
      trainingStatus: this.fileUploadService.getTrainingStatus().pipe(
        catchError(error => {
          return of(null); // Return null for failed training status
        })
      ),
      modelRequests: this.fileUploadService.getUserModelRequests().pipe(
        catchError(error => {
          return of({ success: false, data: null, error });
        })
      ),
    }).subscribe({
      next: ({ trainingStatus, modelRequests }) => {
        const currentState = this.getState();
        const modelRequestsData = modelRequests?.success ? modelRequests.data : null;

        // Training status is either TrainingStatusResponse or null
        const trainingStatusData = trainingStatus; // Use directly since it's not wrapped

        // Get model status using ModelStateService
        const modelInfo = this.modelState.getModelStatusFromData(
          modelRequestsData,
          trainingStatusData
        );
        const { modelStatus, hasTrainedModel, latestTrainedModel } = modelInfo;

        // Update state with additional data
        this.setState({
          uploadedImages: trainingStatusData?.totalUploadedImages || currentState.uploadedImages,
          modelStatus,
          latestTrainedModel,
        });

        // Reset fallback tracking periodically
        this.fallbackOps.resetFallbackTracking();

        // Check if fallback operations are needed
        const fallbackCheck = this.fallbackOps.checkIfFallbackNeeded({
          generatedPhotosCount: currentState.generatedPhotosCount,
          modelStatus,
          hasLatestTrainedModel: !!latestTrainedModel,
          uploadedImages: currentState.uploadedImages,
          hasUserProfile: !!currentState.userProfile,
          latestTrainedModel,
        });

        // Execute fallback operations if needed
        if (fallbackCheck.shouldCheckFilesystem) {
          this.fallbackOps.checkGeneratedImagesFromFilesystem().subscribe({
            next: result => {
              if (result.actualGeneratedCount) {
                this.setState({ generatedPhotosCount: result.actualGeneratedCount });
              }
            },
            error: error => {}, // Silent failure for filesystem check
          });
        }

        if (fallbackCheck.shouldDiscoverModels) {
          this.modelState.runAsyncModelDiscovery();
        }
      },
      error: error => {
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
    this.fileUploadService
      .getUserImages()
      .pipe(
        catchError(error => {
          return of({ success: false, data: null, error });
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
        error: error => {
          // Silent failure for photo count refresh
        },
      });
  }

  // Validate cached images and update state if needed
  private async validateCachedImages(images: UploadedImageThumbnail[]): Promise<void> {
    // Check if validation is recent enough (5 minutes)
    const currentState = this.getState();
    const validationAge = Date.now() - (currentState.lastValidationTime || 0);
    const VALIDATION_TTL = 5 * 60 * 1000; // 5 minutes

    if (currentState.imagesValidated && validationAge < VALIDATION_TTL) {
      return;
    }

    const result = await this.validateAndCleanupImages(images, true);

    if (result.removedCount > 0) {
      this.updateStateWithValidatedImages(
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
  private async validateAndCleanupImages(
    images: UploadedImageThumbnail[],
    isFromCache: boolean
  ): Promise<{
    validImages: UploadedImageThumbnail[];
    removedCount: number;
    repairTriggered?: boolean;
  }> {
    // Check if image validation is disabled via environment configuration
    if (!this.configService.isImageValidationEnabled) {
      return {
        validImages: images, // Return all images as valid
        removedCount: 0,
        repairTriggered: false,
      };
    }

    const validation = await this.imageValidation.filterValidImages(images);

    if (validation.removedCount > 0) {
      // Trigger repair if 404s were found (orphaned database records)
      if (validation.repairSuggested && validation.notFoundCount > 0) {
        try {
          const repairResult = await this.fileUploadService.repairImageDatabase().toPromise();
          if (repairResult?.success) {
            // Force refresh from server to get accurate counts after repair
            await this.forceRefreshAfterRepair();

            return {
              validImages: validation.validImages,
              removedCount: validation.removedCount,
              repairTriggered: true,
            };
          }
        } catch (repairError) {
          // Silent failure - repair error is handled by caller
        }
      }
    }

    return {
      validImages: validation.validImages,
      removedCount: validation.removedCount,
      repairTriggered: false,
    };
  }

  // Update state with validated images and invalidate cache if needed
  private updateStateWithValidatedImages(
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
    this.cacheManager.invalidateCache('dashboard_data');

    // Show notification if images were removed
    if (removedCount > 0) {
      const message = repairTriggered
        ? `Removed ${removedCount} broken image reference(s) and repaired database inconsistencies.`
        : `Removed ${removedCount} broken image reference(s) that could no longer be loaded.`;

      this.notificationService.info(
        repairTriggered ? 'Cleaned up & Repaired' : 'Cleaned up broken images',
        message
      );
    }
  }

  // Clean image URL to remove fragments and cache-busting artifacts
  private cleanImageUrl(url: string): string {
    try {
      // Remove URL fragments (#) and normalize
      const cleanUrl = url.split('#')[0].split('?')[0];

      // Ensure proper leading slash for relative URLs
      if (!cleanUrl.startsWith('http') && !cleanUrl.startsWith('/')) {
        return '/' + cleanUrl;
      }

      return cleanUrl;
    } catch (error) {
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

    const result = await this.validateAndCleanupImages(images, false);

    if (result.removedCount > 0) {
      this.updateStateWithValidatedImages(
        result.validImages,
        result.removedCount,
        result.repairTriggered
      );

      const message = result.repairTriggered
        ? `Validated ${images.length} images. Removed ${result.removedCount} broken references and repaired database.`
        : `Validated ${images.length} images. Removed ${result.removedCount} broken references.`;

      this.notificationService.info(
        result.repairTriggered ? 'Validation & Repair Complete' : 'Image Validation Complete',
        message
      );
    } else {
      // Mark as validated
      this.setState({
        imagesValidated: true,
        lastValidationTime: Date.now(),
      });

      this.notificationService.success(
        'All Images Valid',
        'All uploaded images are accessible and valid.'
      );
    }
  }
}
