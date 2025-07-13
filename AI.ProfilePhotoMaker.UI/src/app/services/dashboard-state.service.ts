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
    private imageValidation: ImageValidationService
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

  loadInitialDashboardData() {
    const CACHE_KEY = 'dashboard_data';

    // Check cache first
    const cachedData = this.cacheManager.getCachedData<DashboardState>(CACHE_KEY);
    if (cachedData?.creditsInfo) {
      console.log('💾 Using cached dashboard data');
      this.setState(cachedData);

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

    console.log('🚀 Starting dashboard data load...');

    // Load critical data, handling all API failures gracefully
    forkJoin({
      profile: this.profileService.getCurrentUserProfile().pipe(
        catchError(error => {
          console.warn('⚠️ Profile API failed:', error);
          return of({ success: false, data: null, error: error });
        })
      ),
      creditStatus: this.creditService.getCreditStatus().pipe(
        catchError(error => {
          console.warn('⚠️ Credit Status API failed:', error);
          return of({ success: false, data: null, error: error });
        })
      ),
      userImages: this.fileUploadService.getUserImages().pipe(
        catchError(error => {
          console.warn('⚠️ User Images API failed:', error);
          return of({ success: false, data: null, error: error });
        })
      ),
      credits: this.replicateService.getCredits().pipe(
        catchError(error => {
          console.warn('⚠️ Credits API failed (TestController disabled):', error);
          return of({ success: false, data: null, error: error });
        })
      ),
    }).subscribe({
      next: ({ profile, creditStatus, userImages, credits }) => {
        console.log('📦 Dashboard API responses:', {
          profileSuccess: profile?.success ?? false,
          creditStatusSuccess: creditStatus?.success ?? false,
          userImagesSuccess: userImages?.success ?? false,
          creditsSuccess: credits?.success ?? false,
          creditStatusData: creditStatus?.data ?? null,
          creditsData: credits?.data ?? null,
          creditsFailureHandled: !(credits?.success ?? false),
        });

        const userProfile = profile?.success ? profile.data : null;
        const userCreditStatus = creditStatus?.success ? creditStatus.data : null;
        const creditsInfo = credits?.success ? credits.data : null;

        // Process uploaded images into thumbnails format
        const userImagesData = userImages?.success ? userImages.data : null;

        console.log('🔍 Debug userImagesData:', {
          success: userImages?.success ?? false,
          hasData: !!userImagesData,
          imagesCount: userImagesData?.images?.length || 0,
          originalUploads: userImagesData?.images?.filter(img => img.isOriginalUpload)?.length || 0,
          withUrls:
            userImagesData?.images?.filter(img => img.isOriginalUpload && img.originalImageUrl)
              ?.length || 0,
          sampleImages: userImagesData?.images?.slice(0, 2),
        });

        // Process and validate uploaded images with robust filtering
        const rawImageThumbnails: UploadedImageThumbnail[] =
          userImagesData?.images
            ?.filter(img => {
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

              // Robust filtering: Image is considered uploaded if:
              // 1. Has IsOriginalUpload flag true, OR
              // 2. Has "Original" style (even if flag is corrupted), AND
              // 3. Has a valid URL
              const isUploadedImage = (isOriginalByFlag || isOriginalByStyle) && hasUrl;

              // Only log uploaded images (much cleaner console)
              console.log(
                `🔍 Uploaded Image ${img.id}: byFlag=${isOriginalByFlag}, byStyle=${isOriginalByStyle}, hasUrl=${hasUrl}, style=${img.style}, result=${isUploadedImage}`
              );

              if (isOriginalByFlag !== isOriginalByStyle) {
                console.warn(
                  `⚠️ Image ${img.id} has flag/style mismatch: isOriginalUpload=${isOriginalByFlag}, style=${img.style} - possible database corruption`
                );
              }

              return isUploadedImage;
            })
            ?.map(img => ({
              id: img.id,
              url: img.originalImageUrl,
              fileName: `Image ${img.id}`, // Use a default filename since it's not in ProcessedImage
            })) || [];

        console.log('📸 Raw uploadedImageThumbnails (before validation):', rawImageThumbnails);

        // Clean URLs immediately to prevent browser caching issues
        const uploadedImageThumbnails = rawImageThumbnails.map(thumb => ({
          ...thumb,
          url: this.cleanImageUrl(thumb.url),
        }));

        // Validate images asynchronously after state is set with clean URLs
        if (uploadedImageThumbnails.length > 0) {
          this.validateAndCleanupImages(uploadedImageThumbnails, false).then(result => {
            if (result.removedCount > 0) {
              console.log(`🧹 Cleaned up ${result.removedCount} broken images from fresh data`);
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
          userImagesData?.images?.filter(img => img.isGenerated)?.length ||
          0;

        console.log(`📊 Generated Photos Count: ${generatedPhotosCount}`);

        // Check if we need immediate filesystem repair
        if (generatedPhotosCount === 0 && uploadedImageThumbnails.length > 0) {
          console.log(
            '⚠️ Found 0 generated photos but have uploaded images - may need filesystem check'
          );
        }

        // Calculate total credits for reactive display, handling null creditsInfo gracefully
        const totalCredits = this.creditService.getTotalAvailableCredits(
          userCreditStatus,
          creditsInfo || null
        );

        // Show info notification if credits API failed but other data loaded successfully
        if (
          !credits?.success &&
          (profile?.success || creditStatus?.success || userImages?.success)
        ) {
          console.log('ℹ️ Dashboard loaded without credits API (TestController disabled)');
          // Don't show notification to user since this is expected during development
        }

        // Set initial state with critical data for fast render
        const newState = {
          userProfile,
          userCreditStatus,
          creditsInfo,
          uploadedImages: uploadedImageThumbnails.length,
          uploadedImageThumbnails,
          generatedPhotosCount,
          modelStatus: 'Loading...', // Temporary status
          isPremiumWorkflow: (userCreditStatus?.purchasedCredits || 0) > 0,
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
        console.log(`⚡ Dashboard loaded in ${loadTime.toFixed(2)}ms`);

        // Load remaining data asynchronously (non-blocking)
        this.loadRemainingDataAsync();

        // Set a timeout to avoid infinite "Loading..." state
        setTimeout(() => {
          const currentState = this.getState();
          if (currentState.modelStatus === 'Loading...') {
            console.warn('⚠️ Model status still loading after 10s, setting fallback status');
            this.setState({ modelStatus: 'Not Started' });
          }
        }, 10000);
      },
      error: error => {
        console.error('❌ Dashboard API call failed:', error);
        console.error('Error details:', {
          message: error.message,
          status: error.status,
          statusText: error.statusText,
          url: error.url,
          error: error.error,
        });
        this.notificationService.error(
          'Dashboard Load Failed',
          'Could not load dashboard data. Please try again.'
        );
        this.setState({
          isLoading: false,
          modelStatus: 'Error', // Set error status instead of leaving as "Loading..."
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
    console.log('🔄 Force refreshing dashboard data...');
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

        console.log(
          `🔄 Post-repair refresh: Found ${uploadedImageThumbnails.length} valid uploaded images`
        );

        // Update state with accurate counts - force validation of fresh data
        this.setState({
          uploadedImageThumbnails,
          uploadedImages: uploadedImageThumbnails.length,
          imagesValidated: false, // Force validation to run on fresh data
          lastValidationTime: 0,
        });

        console.log(`✅ Count synchronized: UI now shows ${uploadedImageThumbnails.length} images`);
      }
    } catch (error) {
      console.error('❌ Failed to refresh after repair:', error);
    }
  }

  // Sync user images cache with current state
  invalidateAndRefreshImages() {
    console.log('🔄 Invalidating image caches and refreshing data');
    this.fileUploadService.invalidateUserImagesCache();
    this.refreshGeneratedPhotosCount();
  }

  // Load remaining data asynchronously after initial render
  private loadRemainingDataAsync() {
    // Load non-critical data in parallel but don't block rendering
    forkJoin({
      trainingStatus: this.fileUploadService.getTrainingStatus().pipe(
        catchError(error => {
          console.warn('⚠️ Training Status API failed:', error);
          return of(null); // Return null for failed training status
        })
      ),
      modelRequests: this.fileUploadService.getUserModelRequests().pipe(
        catchError(error => {
          console.warn('⚠️ Model Requests API failed:', error);
          return of({ success: false, data: null, error: error });
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
            error: error => console.error('Filesystem check failed:', error),
          });
        }

        if (fallbackCheck.shouldDiscoverModels) {
          this.modelState.runAsyncModelDiscovery();
        }

        console.log('✅ Dashboard secondary data loaded successfully');
      },
      error: error => {
        console.error('Failed to load additional dashboard data:', error);
        // Set fallback model status on async load failure
        const currentState = this.getState();
        if (currentState.modelStatus === 'Loading...') {
          console.warn('⚠️ Async load failed, setting fallback model status');
          this.setState({ modelStatus: 'Not Started' });
        }
        // Don't show error to user since initial data loaded successfully
      },
    });
  }

  // Removed sync workarounds - ModelCreationRequest is now the single source of truth

  // Refresh only the photos count after generation completion
  refreshGeneratedPhotosCount() {
    console.log('🔄 Refreshing generated photos count...');
    this.fileUploadService
      .getUserImages()
      .pipe(
        catchError(error => {
          console.warn('⚠️ User Images refresh failed:', error);
          return of({ success: false, data: null, error: error });
        })
      )
      .subscribe({
        next: userImages => {
          const userImagesData = userImages?.success ? userImages.data : null;
          const generatedPhotosCount =
            userImagesData?.generatedImages ||
            userImagesData?.images?.filter(img => img.isGenerated)?.length ||
            0;

          console.log('📊 Refresh Photos Count Debug:', {
            apiGeneratedImages: userImagesData?.generatedImages,
            filteredGeneratedImages:
              userImagesData?.images?.filter(img => img.isGenerated)?.length || 0,
            finalGeneratedPhotosCount: generatedPhotosCount,
            totalImages: userImagesData?.images?.length || 0,
          });

          this.setState({ generatedPhotosCount });
        },
        error: error => {
          console.error('Failed to refresh generated photos count:', error);
        },
      });
  }

  // Make debug methods globally accessible
  enableGlobalDebug() {
    // Enable debug methods from each specialized service
    this.modelState.enableGlobalDebug();
    this.cacheManager.enableGlobalDebug();
    this.fallbackOps.enableGlobalDebug();

    // Dashboard-specific debug methods
    (window as any).forceRefresh = () => this.forceRefresh();
    (window as any).invalidateImages = () => this.invalidateAndRefreshImages();
    (window as any).dashboardState = () => this.getState();

    console.log('🔍 Dashboard debug enabled! Available commands:');
    console.log('  - forceRefresh() - Force refresh dashboard data (clears cache)');
    console.log('  - invalidateImages() - Invalidate image caches and refresh');
    console.log('  - dashboardState() - View current dashboard state');
    console.log('  - validateImages() - Run image validation on current thumbnails');
    console.log('  + Model, Cache, and Fallback debug commands from specialized services');

    // Add image validation debug method
    (window as any).validateImages = () => this.validateCurrentImages();
  }

  // Validate cached images and update state if needed
  private async validateCachedImages(images: UploadedImageThumbnail[]): Promise<void> {
    // Check if validation is recent enough (5 minutes)
    const currentState = this.getState();
    const validationAge = Date.now() - (currentState.lastValidationTime || 0);
    const VALIDATION_TTL = 5 * 60 * 1000; // 5 minutes

    if (currentState.imagesValidated && validationAge < VALIDATION_TTL) {
      console.log('📸 Images recently validated, skipping re-validation');
      return;
    }

    console.log('🔍 Validating cached images...');
    const result = await this.validateAndCleanupImages(images, true);

    if (result.removedCount > 0) {
      console.log(`🧹 Cleaned up ${result.removedCount} broken images from cache`);
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
    console.log(
      `🔍 Validating ${images.length} uploaded images ${isFromCache ? '(from cache)' : '(fresh)'}...`
    );

    const validation = await this.imageValidation.filterValidImages(images);

    if (validation.removedCount > 0) {
      console.log(`🧹 Image validation results:`, {
        source: isFromCache ? 'cache' : 'fresh',
        total: images.length,
        valid: validation.validImages.length,
        removed: validation.removedCount,
        repairSuggested: validation.repairSuggested,
        notFoundCount: validation.notFoundCount,
        invalidImages: validation.invalidImages.map(img => ({ id: img.id, url: img.url })),
      });

      // Trigger repair if 404s were found (orphaned database records)
      if (validation.repairSuggested && validation.notFoundCount > 0) {
        console.log(
          `🔧 Found ${validation.notFoundCount} 404 errors, triggering database repair...`
        );
        try {
          const repairResult = await this.fileUploadService.repairImageDatabase().toPromise();
          if (repairResult?.success) {
            console.log('✅ Database repair completed successfully');

            // Force refresh from server to get accurate counts after repair
            console.log('🔄 Force refreshing data after repair to sync counts...');
            await this.forceRefreshAfterRepair();

            return {
              validImages: validation.validImages,
              removedCount: validation.removedCount,
              repairTriggered: true,
            };
          }
        } catch (repairError) {
          console.error('🔧 Database repair failed:', repairError);
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
      console.warn('Failed to clean image URL:', url, error);
      return url;
    }
  }

  // Public method for manual validation (debug purposes)
  async validateCurrentImages(): Promise<void> {
    const currentState = this.getState();
    const images = currentState.uploadedImageThumbnails;

    if (images.length === 0) {
      console.log('📸 No images to validate');
      return;
    }

    console.log(`🔍 Manually validating ${images.length} current images...`);
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
