import { Injectable } from '@angular/core';
import { BehaviorSubject, forkJoin } from 'rxjs';
import { UserProfile, ProfileService } from './profile.service';
import { CreditsInfo, ReplicateService } from './replicate.service';
import { UserCreditStatus, CreditService } from './credit.service';
import { FileUploadService } from './file-upload.service';
import { StyleService } from './style.service';
import { NotificationService } from './notification.service';
import { CacheManagerService } from './cache-manager.service';
import { ModelStateService } from './model-state.service';
import { FallbackOperationsService } from './fallback-operations.service';
import { IDashboardStateService, DashboardState, UploadedImageThumbnail } from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root'
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
    latestTrainedModel: null
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
    private fallbackOps: FallbackOperationsService
  ) { }

  getState(): DashboardState {
    return this._state.getValue();
  }

  setState(newState: Partial<DashboardState>) {
    this._state.next({
      ...this.getState(),
      ...newState
    });
  }

  loadInitialDashboardData() {
    const CACHE_KEY = 'dashboard_data';
    
    // Check cache first
    const cachedData = this.cacheManager.getCachedData<DashboardState>(CACHE_KEY);
    if (cachedData && cachedData.creditsInfo) {
      console.log('💾 Using cached dashboard data');
      this.setState(cachedData);
      return;
    }
    
    // Debounce rapid reloads
    if (this.cacheManager.shouldDebounceRequest('dashboard_load', CacheManagerService.LOAD_DEBOUNCE_MS)) {
      return;
    }

    this.setState({ isLoading: true });
    const loadStartTime = performance.now();

    // Load critical data including credits for immediate display
    forkJoin({
      profile: this.profileService.getCurrentUserProfile(),
      creditStatus: this.creditService.getCreditStatus(),
      userImages: this.fileUploadService.getUserImages(),
      credits: this.replicateService.getCredits()
    }).subscribe({
      next: ({ profile, creditStatus, userImages, credits }) => {
        const userProfile = profile.success ? profile.data : null;
        const userCreditStatus = creditStatus.success ? creditStatus.data : null;
        const creditsInfo = credits.success ? credits.data : null;
        
        // Process uploaded images into thumbnails format
        const uploadedImageThumbnails: UploadedImageThumbnail[] = userImages.images
          .filter(img => img.isOriginalUpload)
          .map(img => ({
            id: img.id,
            url: img.originalImageUrl,
            fileName: `Image ${img.id}` // Use a default filename since it's not in ProcessedImage
          }));
        
        // Count generated photos (use API count or filter generated images)
        let generatedPhotosCount = userImages.generatedImages || 
          userImages.images.filter(img => img.isGenerated).length;
        
        console.log(`📊 Generated Photos Count: ${generatedPhotosCount}`);
        
        // Check if we need immediate filesystem repair
        if (generatedPhotosCount === 0 && uploadedImageThumbnails.length > 0) {
          console.log('⚠️ Found 0 generated photos but have uploaded images - may need filesystem check');
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
          isLoading: false
        };
        
        this.setState(newState);

        // Cache the loaded data
        this.cacheManager.setCachedData('dashboard_data', this.getState(), CacheManagerService.DASHBOARD_CACHE_DURATION_MS);
        const loadTime = performance.now() - loadStartTime;
        console.log(`⚡ Dashboard loaded in ${loadTime.toFixed(2)}ms`);

        // Load remaining data asynchronously (non-blocking)
        this.loadRemainingDataAsync();
      },
      error: (error) => {
        console.error('Dashboard API call failed:', error);
        this.notificationService.error('Dashboard Load Failed', 'Could not load dashboard data. Please try again.');
        this.setState({ isLoading: false });
      }
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
    this.loadInitialDashboardData();
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
      trainingStatus: this.fileUploadService.getTrainingStatus(),
      modelRequests: this.fileUploadService.getUserModelRequests()
    }).subscribe({
      next: ({ trainingStatus, modelRequests }) => {
        const currentState = this.getState();
        const modelRequestsData = modelRequests.success ? modelRequests.data : null;
        
        // Get model status using ModelStateService
        const modelInfo = this.modelState.getModelStatusFromData(modelRequestsData, trainingStatus);
        const { modelStatus, hasTrainedModel, latestTrainedModel } = modelInfo;
        
        // Update state with additional data
        this.setState({
          uploadedImages: trainingStatus.totalUploadedImages || currentState.uploadedImages,
          modelStatus,
          latestTrainedModel
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
          latestTrainedModel
        });

        // Execute fallback operations if needed
        if (fallbackCheck.shouldCheckFilesystem) {
          this.fallbackOps.checkGeneratedImagesFromFilesystem().subscribe({
            next: (result) => {
              if (result.actualGeneratedCount) {
                this.setState({ generatedPhotosCount: result.actualGeneratedCount });
              }
            },
            error: (error) => console.error('Filesystem check failed:', error)
          });
        }

        if (fallbackCheck.shouldDiscoverModels) {
          this.modelState.runAsyncModelDiscovery();
        }
        
        console.log('✅ Dashboard secondary data loaded successfully');
      },
      error: (error) => {
        console.error('Failed to load additional dashboard data:', error);
        // Don't show error to user since initial data loaded successfully
      }
    });
  }


  // Removed sync workarounds - ModelCreationRequest is now the single source of truth

  // Refresh only the photos count after generation completion
  refreshGeneratedPhotosCount() {
    console.log('🔄 Refreshing generated photos count...');
    this.fileUploadService.getUserImages().subscribe({
      next: (userImages) => {
        const generatedPhotosCount = userImages.generatedImages || 
          userImages.images.filter(img => img.isGenerated).length;
        
        console.log('📊 Refresh Photos Count Debug:', {
          apiGeneratedImages: userImages.generatedImages,
          filteredGeneratedImages: userImages.images.filter(img => img.isGenerated).length,
          finalGeneratedPhotosCount: generatedPhotosCount,
          totalImages: userImages.images.length
        });
        
        this.setState({ generatedPhotosCount });
      },
      error: (error) => {
        console.error('Failed to refresh generated photos count:', error);
      }
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
    console.log('  + Model, Cache, and Fallback debug commands from specialized services');
  }
}
