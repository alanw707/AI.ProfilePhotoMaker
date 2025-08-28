import { Injectable } from '@angular/core';
import { BehaviorSubject, combineLatest, forkJoin, Observable, of, firstValueFrom } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ProfileService, UserProfile } from './profile.service';
import { ModelStateService } from './model-state.service';
import { FallbackOperationsService } from './fallback-operations.service';
import { CacheManagerService } from './cache-manager.service';
import { NotificationService } from './notification.service';
import { ImageStateService } from './image-state.service';
import { SubscriptionStateService } from './subscription-state.service';
import { FileUploadService } from './file-upload.service';
import { DashboardState, IDashboardStateService } from '../interfaces/service.interfaces';

/**
 * Orchestrates multiple specialized services to provide a unified dashboard interface
 * Replaces the monolithic DashboardStateService with a coordination-focused approach
 */
@Injectable({
  providedIn: 'root',
})
export class DashboardCoordinatorService implements IDashboardStateService {
  private readonly CACHE_KEY = 'dashboard_coordinator_data';

  // Core state for coordination
  private readonly _coordinatorState = new BehaviorSubject<{
    userProfile: UserProfile | null;
    modelStatus: string;
    latestTrainedModel: unknown;
    isLoading: boolean;
  }>({
    userProfile: null,
    modelStatus: 'Not Started',
    latestTrainedModel: null,
    isLoading: false,
  });

  // Combined dashboard state observable
  public readonly state$: Observable<DashboardState>;

  constructor(
    private _profileService: ProfileService,
    private _modelState: ModelStateService,
    private _fallbackOps: FallbackOperationsService,
    private _cacheManager: CacheManagerService,
    private _notificationService: NotificationService,
    private _imageState: ImageStateService,
    private _subscriptionState: SubscriptionStateService,
    private _fileUploadService: FileUploadService
  ) {
    // Combine states from specialized services
    this.state$ = combineLatest([
      this._coordinatorState.asObservable(),
      this._imageState.state$,
      this._subscriptionState.state$,
    ]).pipe(
      map(([coordinator, images, subscription]) => ({
        // Profile and model data
        userProfile: coordinator.userProfile,
        modelStatus: coordinator.modelStatus,
        latestTrainedModel: coordinator.latestTrainedModel,

        // Image data from ImageStateService
        uploadedImages: images.uploadedImages,
        uploadedImageThumbnails: images.uploadedImageThumbnails,
        generatedPhotosCount: images.generatedPhotosCount,
        imagesValidated: images.imagesValidated,
        lastValidationTime: images.lastValidationTime,

        // Subscription data from SubscriptionStateService
        userCreditStatus: subscription.userCreditStatus,
        creditsInfo: subscription.creditsInfo,
        totalCredits: subscription.totalCredits,
        isPremiumWorkflow: subscription.isPremiumWorkflow,

        // Loading state (true if any service is loading)
        isLoading: coordinator.isLoading || images.isLoading || subscription.isLoading,
      }))
    );
  }

  /**
   * Get current combined dashboard state
   */
  getState(): DashboardState {
    const coordinator = this._coordinatorState.getValue();
    const images = this._imageState.getState();
    const subscription = this._subscriptionState.getState();

    return {
      userProfile: coordinator.userProfile,
      modelStatus: coordinator.modelStatus,
      latestTrainedModel: coordinator.latestTrainedModel,
      uploadedImages: images.uploadedImages,
      uploadedImageThumbnails: images.uploadedImageThumbnails,
      generatedPhotosCount: images.generatedPhotosCount,
      imagesValidated: images.imagesValidated,
      lastValidationTime: images.lastValidationTime,
      userCreditStatus: subscription.userCreditStatus,
      creditsInfo: subscription.creditsInfo,
      totalCredits: subscription.totalCredits,
      isPremiumWorkflow: subscription.isPremiumWorkflow,
      isLoading: coordinator.isLoading || images.isLoading || subscription.isLoading,
    };
  }

  /**
   * Update partial state (delegates to appropriate service)
   */
  setState(newState: Partial<DashboardState>): void {
    // Update coordinator state if relevant fields are present
    const coordinatorFields = ['userProfile', 'modelStatus', 'latestTrainedModel', 'isLoading'];
    const hasCoordinatorFields = coordinatorFields.some(field => field in newState);

    if (hasCoordinatorFields) {
      const currentCoordinator = this._coordinatorState.getValue();
      this._coordinatorState.next({
        ...currentCoordinator,
        ...(newState.userProfile !== undefined && { userProfile: newState.userProfile }),
        ...(newState.modelStatus !== undefined && { modelStatus: newState.modelStatus }),
        ...(newState.latestTrainedModel !== undefined && {
          latestTrainedModel: newState.latestTrainedModel,
        }),
        ...(newState.isLoading !== undefined && { isLoading: newState.isLoading }),
      });
    }

    // Delegate image-related updates to ImageStateService
    const imageFields = [
      'uploadedImages',
      'uploadedImageThumbnails',
      'generatedPhotosCount',
      'imagesValidated',
      'lastValidationTime',
    ];
    const imageUpdate = imageFields.reduce(
      (acc, field) => {
        if (field in newState) {
          acc[field] = newState[field as keyof DashboardState];
        }
        return acc;
      },
      {} as Record<string, unknown>
    );

    if (Object.keys(imageUpdate).length > 0) {
      this._imageState.setState(imageUpdate);
    }

    // Delegate subscription-related updates to SubscriptionStateService
    const subscriptionFields = [
      'userCreditStatus',
      'creditsInfo',
      'totalCredits',
      'isPremiumWorkflow',
    ];
    const subscriptionUpdate = subscriptionFields.reduce(
      (acc, field) => {
        if (field in newState) {
          acc[field] = newState[field as keyof DashboardState];
        }
        return acc;
      },
      {} as Record<string, unknown>
    );

    if (Object.keys(subscriptionUpdate).length > 0) {
      this._subscriptionState.setState(subscriptionUpdate);
    }
  }

  /**
   * Load complete dashboard data by orchestrating specialized services
   */
  async loadInitialDashboardData(): Promise<void> {
    const unusedStartTime = performance.now();

    // Check if we should debounce this load
    if (
      this._cacheManager.shouldDebounceRequest(
        'dashboard_coordinator_load',
        CacheManagerService.LOAD_DEBOUNCE_MS
      )
    ) {
      return;
    }

    this.setCoordinatorLoading(true);

    try {
      // Load profile data (coordinator responsibility)
      const profilePromise = this.loadProfileData();

      // Load specialized service data in parallel
      const imagePromise = this._imageState.loadUserImages();
      const subscriptionPromise = this._subscriptionState.loadFullSubscriptionData();

      // Wait for core data to load with better error handling
      const results = await Promise.allSettled([profilePromise, imagePromise, subscriptionPromise]);

      // Check results and handle partial failures gracefully
      let failedLoads = 0;
      results.forEach((result, index) => {
        if (result.status === 'rejected') {
          const serviceNames = ['Profile', 'Images', 'Subscription'];
          console.warn(`⚠️ ${serviceNames[index]} data load failed:`, result.reason);
          failedLoads++;
        }
      });

      // Only show error if all services failed
      if (failedLoads === results.length) {
        throw new Error('All dashboard services failed to load');
      } else if (failedLoads > 0) {
        console.log(
          `✅ Dashboard loaded with ${results.length - failedLoads}/${results.length} services successful`
        );
      }

      // Load remaining data asynchronously (non-blocking)
      this.loadRemainingDataAsync();
    } catch (error) {
      console.error('❌ Dashboard coordination failed:', error);
      // Try to load cached data as fallback
      this.loadCachedDataAsFallback();
      this._notificationService.error(
        'Dashboard Load Failed',
        'Could not load dashboard data. Please refresh the page or try again.'
      );
    } finally {
      this.setCoordinatorLoading(false);
    }
  }

  /**
   * Load basic data for settings page (counts only, no validation)
   */
  async loadBasicDataForSettings(): Promise<void> {
    try {
      // Load profile data
      await this.loadProfileData();

      // Load only internal credits (faster)
      await this._subscriptionState.loadCreditsOnly();

      // Load user images without validation
      await this._imageState.loadUserImages();
    } catch (error) {
      console.error('❌ Settings data load failed:', error);
    }
  }

  /**
   * Load only internal credits
   */
  async loadCreditsOnly(): Promise<void> {
    await this._subscriptionState.loadCreditsOnly();
  }

  /**
   * Load profile data (coordinator responsibility)
   */
  private async loadProfileData(): Promise<void> {
    try {
      const initial = await firstValueFrom(
        this._profileService.getCurrentUserProfile().pipe(
          catchError(error => {
            console.warn('⚠️ Profile API failed:', error);
            return of({ success: false, data: null as any, error });
          })
        )
      );

      let userProfile = initial?.success ? initial.data : null;

      // Auto-create profile on first login if none exists (backend returns 404)
      if (!userProfile) {
        try {
          const created = await firstValueFrom(
            this._profileService.createProfile({}).pipe(
              catchError(error => {
                // If another parallel call already created it, ignore 400s
                console.warn('⚠️ Create profile failed:', error);
                return of({ success: false, data: null as any, error });
              })
            )
          );

          if (created?.success && created.data) {
            userProfile = created.data;
          } else {
            // Retry fetch once in case profile was created by another request
            const refetch = await firstValueFrom(
              this._profileService.getCurrentUserProfile().pipe(
                catchError(error => {
                  console.warn('⚠️ Profile re-fetch failed:', error);
                  return of({ success: false, data: null as any, error });
                })
              )
            );
            userProfile = refetch?.success ? refetch.data : null;
          }
        } catch (creationError) {
          console.warn('⚠️ Profile auto-create attempt threw:', creationError);
        }
      }

      const currentState = this._coordinatorState.getValue();
      this._coordinatorState.next({
        ...currentState,
        userProfile,
      });
    } catch (error) {
      console.error('Failed to load profile data:', error);
    }
  }

  /**
   * Load cached data as fallback when API calls fail
   */
  private loadCachedDataAsFallback(): void {
    try {
      const cachedData = this._cacheManager.getCachedData<any>(this.CACHE_KEY);
      if (cachedData) {
        console.log('🔄 Loading cached dashboard data as fallback');
        // Apply cached data to current state if available
        const currentState = this._coordinatorState.getValue();
        this._coordinatorState.next({
          ...currentState,
          ...cachedData,
        });
      }
    } catch (error) {
      console.warn('⚠️ Could not load cached dashboard data:', error);
    }
  }

  /**
   * Load remaining data asynchronously after initial render
   */
  private loadRemainingDataAsync(): void {
    forkJoin({
      unifiedStatus: this._fileUploadService.getUnifiedModelStatus().pipe(
        catchError(error => {
          console.warn('⚠️ Unified Model Status API failed:', error);
          return of(null);
        })
      ),
      trainingStatus: this._fileUploadService.getTrainingStatus().pipe(
        catchError(error => {
          console.warn('⚠️ Training Status API failed:', error);
          return of(null);
        })
      ),
      modelRequests: this._fileUploadService.getUserModelRequests().pipe(
        catchError(error => {
          console.warn('⚠️ Model Requests API failed:', error);
          return of({ success: false, data: null, error });
        })
      ),
    }).subscribe({
      next: ({ unifiedStatus, trainingStatus, modelRequests }) => {
        const modelRequestsData = modelRequests?.success ? modelRequests.data : null;
        const trainingStatusData = trainingStatus;

        // Prefer unified server status if available
        let modelInfo: { modelStatus: string; latestTrainedModel: any } = {
          modelStatus: 'Not Started',
          latestTrainedModel: null,
        };
        if (unifiedStatus) {
          const display = this._modelState['mapUnifiedStatusToDisplay']
            ? (this._modelState as any).mapUnifiedStatusToDisplay(
                unifiedStatus.statusCode,
                unifiedStatus.reason
              )
            : ((): string => {
                switch (unifiedStatus.statusCode) {
                  case 'ModelReady':
                    return 'Model Ready';
                  case 'Training':
                    return 'training';
                  case 'ReadyForTraining':
                    return 'Ready for training';
                  case 'Failed':
                    return unifiedStatus.reason && unifiedStatus.reason.includes('deleted')
                      ? 'Ready for training'
                      : 'Training failed';
                  case 'NotStarted':
                  default:
                    return 'Not Started';
                }
              })();
          modelInfo = {
            modelStatus: display,
            latestTrainedModel: unifiedStatus.hasTrainedModel
              ? {
                  trainedModelVersion: unifiedStatus.trainedModelVersion,
                  replicateModelId: unifiedStatus.trainedModelId,
                }
              : null,
          };
        } else {
          // Fallback to legacy combination of endpoints
          modelInfo = this._modelState.getModelStatusFromData(
            modelRequestsData,
            trainingStatusData
          );
        }

        const currentState = this._coordinatorState.getValue();
        this._coordinatorState.next({
          ...currentState,
          modelStatus: modelInfo.modelStatus,
          latestTrainedModel: modelInfo.latestTrainedModel,
        });

        // Reset fallback tracking periodically
        this._fallbackOps.resetFallbackTracking();

        // Check if fallback operations are needed
        const dashboardState = this.getState();
        const fallbackCheck = this._fallbackOps.checkIfFallbackNeeded({
          generatedPhotosCount: dashboardState.generatedPhotosCount,
          modelStatus: modelInfo.modelStatus,
          hasLatestTrainedModel: !!modelInfo.latestTrainedModel,
          uploadedImages: dashboardState.uploadedImages,
          hasUserProfile: !!dashboardState.userProfile,
          latestTrainedModel: modelInfo.latestTrainedModel,
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
      error: error => {
        console.error('Failed to load additional dashboard data:', error);
        const currentState = this._coordinatorState.getValue();
        if (currentState.modelStatus === 'Loading...') {
          this._coordinatorState.next({
            ...currentState,
            modelStatus: 'Not Started',
          });
        }
      },
    });
  }

  /**
   * Set coordinator loading state
   */
  private setCoordinatorLoading(isLoading: boolean): void {
    const currentState = this._coordinatorState.getValue();
    this._coordinatorState.next({
      ...currentState,
      isLoading,
    });
  }

  /**
   * Reset state for all services
   */
  resetState(): void {
    this._coordinatorState.next({
      userProfile: null,
      modelStatus: 'Not Started',
      latestTrainedModel: null,
      isLoading: false,
    });

    this._imageState.resetState();
    this._subscriptionState.resetState();
    this._cacheManager.invalidateCache(this.CACHE_KEY);
  }

  /**
   * Force refresh all services
   */
  forceRefresh(): void {
    this._cacheManager.forceRefresh(this.CACHE_KEY);
    this._fallbackOps.resetFallbackTracking();

    // Force refresh specialized services
    this._imageState.forceRefresh();
    this._subscriptionState.forceRefresh();

    // Reload dashboard data
    this.loadInitialDashboardData();
  }

  /**
   * Invalidate and refresh images (delegate to ImageStateService)
   */
  invalidateAndRefreshImages(): void {
    this._imageState.invalidateAndRefresh();
  }

  /**
   * Refresh generated photos count (delegate to ImageStateService)
   */
  refreshGeneratedPhotosCount(): void {
    this._imageState.refreshGeneratedPhotosCount();
  }
}
