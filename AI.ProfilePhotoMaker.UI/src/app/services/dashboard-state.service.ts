import { Injectable } from '@angular/core';
import { BehaviorSubject, forkJoin } from 'rxjs';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { UserProfile, ProfileService } from './profile.service';
import { CreditsInfo, ReplicateService } from './replicate.service';
import { UserCreditStatus, CreditService } from './credit.service';
import { FileUploadService } from './file-upload.service';
import { StyleService } from './style.service';
import { NotificationService } from './notification.service';
import { ConfigService } from './config.service';
import { AuthService } from './auth.service';

export interface UploadedImageThumbnail {
  id: number;
  url: string;
  fileName: string;
}

export interface DashboardState {
  userProfile: UserProfile | null;
  creditsInfo: CreditsInfo | null;
  userCreditStatus: UserCreditStatus | null;
  uploadedImages: number;
  uploadedImageThumbnails: UploadedImageThumbnail[];
  generatedPhotosCount: number;
  modelStatus: string;
  isPremiumWorkflow: boolean;
  isLoading: boolean;
  latestTrainedModel?: any; // Model data from ModelCreationRequest
}

@Injectable({
  providedIn: 'root'
})
export class DashboardStateService {
  private readonly initialState: DashboardState = {
    userProfile: null,
    creditsInfo: null,
    userCreditStatus: null,
    uploadedImages: 0,
    uploadedImageThumbnails: [],
    generatedPhotosCount: 0,
    modelStatus: 'Not Started',
    isPremiumWorkflow: false,
    isLoading: true,
    latestTrainedModel: null
  };

  private readonly _state = new BehaviorSubject<DashboardState>(this.initialState);
  readonly state$ = this._state.asObservable();
  private lastLoadTime = 0;
  private readonly LOAD_DEBOUNCE_MS = 1000; // Prevent rapid reloads
  private cacheExpiry = 0;
  private readonly CACHE_DURATION_MS = 30000; // 30 seconds cache
  private fallbackOperationsRun = {
    filesystemCheck: false,
    modelDiscovery: false,
    lastReset: 0
  };

  constructor(
    private profileService: ProfileService,
    private replicateService: ReplicateService,
    private creditService: CreditService,
    private fileUploadService: FileUploadService,
    private styleService: StyleService,
    private notificationService: NotificationService,
    private http: HttpClient,
    private config: ConfigService,
    private authService: AuthService
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
    const now = Date.now();
    
    // Check cache first
    if (now < this.cacheExpiry && !this.getState().isLoading) {
      console.log('💾 Using cached dashboard data - cache still valid');
      return;
    }
    
    // Debounce rapid reloads
    if (now - this.lastLoadTime < this.LOAD_DEBOUNCE_MS) {
      console.log('🚫 Skipping dashboard reload - too soon after last load');
      return;
    }
    this.lastLoadTime = now;

    this.setState({ isLoading: true });
    const loadStartTime = performance.now();

    // Load only critical data first for faster initial render
    forkJoin({
      profile: this.profileService.getCurrentUserProfile(),
      creditStatus: this.creditService.getCreditStatus(),
      userImages: this.fileUploadService.getUserImages()
    }).subscribe({
      next: ({ profile, creditStatus, userImages }) => {
        const userProfile = profile.success ? profile.data : null;
        const userCreditStatus = creditStatus.success ? creditStatus.data : null;
        
        // Process uploaded images into thumbnails format
        const uploadedImageThumbnails: UploadedImageThumbnail[] = userImages.images
          .filter(img => img.isOriginalUpload && img.fileExists)
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
        this.setState({
          userProfile,
          userCreditStatus,
          uploadedImages: uploadedImageThumbnails.length,
          uploadedImageThumbnails,
          generatedPhotosCount,
          modelStatus: 'Loading...', // Temporary status
          isPremiumWorkflow: (userCreditStatus?.purchasedCredits || 0) > 0,
          isLoading: false
        });

        // Set cache expiry for successful load
        this.cacheExpiry = Date.now() + this.CACHE_DURATION_MS;
        const loadTime = performance.now() - loadStartTime;
        console.log(`⚡ Dashboard loaded in ${loadTime.toFixed(2)}ms`);
        console.log('💾 Dashboard data cached for 30 seconds');

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
    this.cacheExpiry = 0; // Clear cache on reset
  }

  // Force refresh by clearing cache and reloading
  forceRefresh() {
    console.log('🔄 Force refreshing dashboard data...');
    this.cacheExpiry = 0;
    this.lastLoadTime = 0;
    this.fallbackOperationsRun = { filesystemCheck: false, modelDiscovery: false, lastReset: Date.now() };
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
      credits: this.replicateService.getCredits(),
      trainingStatus: this.fileUploadService.getTrainingStatus(),
      modelRequests: this.fileUploadService.getUserModelRequests()
    }).subscribe({
      next: ({ credits, trainingStatus, modelRequests }) => {
        const currentState = this.getState();
        const creditsInfo = credits.success ? credits.data : null;
        const modelRequestsData = modelRequests.success ? modelRequests.data : null;
        
        // Determine model status from ModelCreationRequest (single source of truth)
        let modelStatus = 'Not Started';
        let hasTrainedModel = false;

        // Use ModelCreationRequest as single source of truth
        if (modelRequestsData?.hasTrainedModel && modelRequestsData?.latestTrainedModel) {
          hasTrainedModel = true;
          modelStatus = 'Model Ready';
        }
        // Check if we have pending/in-progress training
        else if (modelRequestsData?.allRequests?.some((req: any) => req.status === 'creating' || req.status === 'pending')) {
          modelStatus = 'training';
        }
        // Default to training status or initial state
        else {
          modelStatus = trainingStatus.status || 'Not Started';
        }
        
        // Update state with additional data
        this.setState({
          creditsInfo,
          uploadedImages: trainingStatus.totalUploadedImages || currentState.uploadedImages,
          modelStatus,
          latestTrainedModel: modelRequestsData?.latestTrainedModel || null
        });

        // Reset fallback tracking every 5 minutes to allow fresh attempts
        const now = Date.now();
        if (now - this.fallbackOperationsRun.lastReset > 300000) { // 5 minutes
          this.fallbackOperationsRun = { filesystemCheck: false, modelDiscovery: false, lastReset: now };
          console.log('🔄 Reset fallback operation tracking');
        }

        // Check filesystem if needed - but only once per session unless reset
        if (currentState.generatedPhotosCount === 0 && 
            modelRequestsData?.hasTrainedModel && 
            !this.fallbackOperationsRun.filesystemCheck) {
          console.log('📁 Database shows 0 generated images but model exists - checking filesystem fallback');
          this.fallbackOperationsRun.filesystemCheck = true;
          this.checkGeneratedImagesFromFilesystem();
        }

        // Run model discovery if needed - but only once per session unless reset
        if ((!hasTrainedModel || modelStatus === 'Not Started') && 
            !this.fallbackOperationsRun.modelDiscovery) {
          console.log('🔍 Running model discovery...');
          this.fallbackOperationsRun.modelDiscovery = true;
          this.runAsyncModelDiscovery();
        }
        
        console.log('✅ Dashboard secondary data loaded successfully');
      },
      error: (error) => {
        console.error('Failed to load additional dashboard data:', error);
        // Don't show error to user since initial data loaded successfully
      }
    });
  }

  // Run model discovery asynchronously without blocking the UI
  private runAsyncModelDiscovery() {
    this.profileService.discoverModels().subscribe({
      next: (discoveryResult) => {
        if (discoveryResult?.success && discoveryResult?.data?.ModelsAdded > 0) {
          // Update just the model status without reloading everything
          this.updateModelStatus();
        }
      },
      error: (error) => {
        console.error('Async model discovery failed:', error);
        // Don't show error to user - this is a background operation
      }
    });
  }

  // Update only the model status after discovery
  private updateModelStatus() {
    forkJoin({
      trainingStatus: this.fileUploadService.getTrainingStatus(),
      modelRequests: this.fileUploadService.getUserModelRequests()
    }).subscribe({
      next: ({ trainingStatus, modelRequests }) => {
        const currentState = this.getState();
        const modelRequestsData = modelRequests.success ? modelRequests.data : null;
        
        let modelStatus = currentState.modelStatus;
        
        // Re-check model status with updated data
        if (modelRequestsData?.hasTrainedModel && modelRequestsData?.latestTrainedModel) {
          modelStatus = 'Model Ready';
          this.setState({ 
            modelStatus,
            latestTrainedModel: modelRequestsData.latestTrainedModel
          });
        } else {
          this.setState({ modelStatus });
        }
      }
    });
  }

  // Debug methods for console testing
  async debugModelStatus() {
    console.log('🔍 Starting comprehensive model status debug...');
    
    try {
      const debugResult = await this.fileUploadService.getDebugModelStatus().toPromise();
      console.log('🔍 Debug API Result:', debugResult);
      
      const testResult = await this.fileUploadService.testModelCreationEndpoint().toPromise();
      console.log('🔍 Test Model Creation Endpoint Result:', testResult);
      
      const discoverResult = await this.fileUploadService.discoverUserModels().toPromise();
      console.log('🔍 Direct Replicate API Discovery Result:', discoverResult);
      
      const specificModelTest = await this.fileUploadService.testSpecificModel().toPromise();
      console.log('🔍 Specific Model Test Result:', specificModelTest);
      
      return {
        debug: debugResult,
        test: testResult,
        discover: discoverResult,
        specificModel: specificModelTest,
        currentState: this.getState()
      };
    } catch (error) {
      console.error('🚨 Debug failed:', error);
      return { error };
    }
  }

  // Manual model discovery method
  async triggerModelDiscovery() {
    console.log('🔍 Manually triggering model discovery...');
    
    try {
      const discoveryResult = await this.profileService.discoverModels().toPromise();
      console.log('🔍 Manual Model Discovery Result:', discoveryResult);
      
      if (discoveryResult?.success && discoveryResult?.data?.ModelsAdded > 0) {
        console.log('🎉 Models found and synced! Reloading dashboard...');
        this.loadInitialDashboardData();
      } else {
        console.log('ℹ️ No new models found to sync');
      }
      
      return discoveryResult;
    } catch (error) {
      console.error('🚨 Manual model discovery failed:', error);
      return { success: false, error };
    }
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

  // Filesystem fallback when database is empty but images should exist
  private checkGeneratedImagesFromFilesystem() {
    const token = this.authService.getToken();
    if (!token) {
      console.error('❌ No authentication token available for fix endpoint');
      return;
    }

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });

    this.http.post(this.config.getFullUrl('/test/fix-generated-images'), {}, { headers }).subscribe({
      next: (response: any) => {
        if (response.success && response.data?.addedCount > 0) {
          console.log(`✅ Auto-fixed: Added ${response.data.addedCount} generated images to database`);
          // Instead of just adding, let's refresh the actual count from the API
          this.fileUploadService.invalidateUserImagesCache();
          this.cacheExpiry = 0;
          
          // Get fresh data to ensure we have the correct total count
          this.fileUploadService.getUserImages(true).subscribe({
            next: (freshData) => {
              const actualGeneratedCount = freshData.generatedImages || 
                freshData.images.filter(img => img.isGenerated).length;
              
              this.setState({
                generatedPhotosCount: actualGeneratedCount
              });
              
              console.log(`📊 Refreshed generated photos count: ${actualGeneratedCount} (was ${response.data.addedCount} added)`);
            },
            error: (error) => {
              console.error('Failed to refresh generated photos count after fix:', error);
            }
          });
        } else {
          console.log('ℹ️ No missing generated images found to fix');
        }
      },
      error: (error) => {
        console.error('❌ Failed to check/fix generated images from filesystem:', error);
        console.error('Error details:', error);
      }
    });
  }

  // Intelligent fallback operations - only run when actually needed
  checkIfFallbackNeeded() {
    const state = this.getState();
    
    console.log('🔍 Checking if fallback needed with state:', {
      generatedPhotosCount: state.generatedPhotosCount,
      modelStatus: state.modelStatus,
      hasLatestTrainedModel: !!state.latestTrainedModel,
      uploadedImages: state.uploadedImages,
      hasUserProfile: !!state.userProfile
    });
    
    // Check filesystem if we have 0 photos but evidence of a trained model
    const shouldCheckFilesystem = 
      state.generatedPhotosCount === 0 && 
      (state.latestTrainedModel || state.modelStatus === 'Model Ready') &&
      state.userProfile; // User has been active
      
    // Discover models if we're missing model data but have uploads
    const shouldDiscoverModels = 
      state.modelStatus === 'Not Started' && 
      state.uploadedImages > 0 &&
      !state.latestTrainedModel;
    
    if (shouldCheckFilesystem) {
      console.log('🔍 Data inconsistency detected - checking filesystem for missing images');
      this.checkGeneratedImagesFromFilesystem();
    }
    
    if (shouldDiscoverModels) {
      console.log('🔍 Missing model data detected - running model discovery');
      this.runAsyncModelDiscovery();
    }
    
    if (!shouldCheckFilesystem && !shouldDiscoverModels) {
      console.log('✅ No fallback operations needed - data appears consistent');
    }
  }

  // Debug method to check actual data vs displayed data
  async debugDataDiscrepancy() {
    console.log('🔍 Debugging data discrepancy...');
    
    try {
      const freshData = await this.fileUploadService.getUserImages(true).toPromise();
      const currentState = this.getState();
      
      console.log('📊 Data Discrepancy Analysis:');
      console.log('  Dashboard shows:', currentState.generatedPhotosCount);
      console.log('  API generatedImages field:', freshData?.generatedImages);
      console.log('  Filtered generated count:', freshData?.images.filter(img => img.isGenerated).length);
      console.log('  Total images:', freshData?.totalImages);
      console.log('  All images:', freshData?.images.map(img => ({
        id: img.id,
        isGenerated: img.isGenerated,
        style: img.style,
        fileExists: img.fileExists
      })));
      
      return {
        dashboardCount: currentState.generatedPhotosCount,
        apiGeneratedField: freshData?.generatedImages,
        filteredCount: freshData?.images.filter(img => img.isGenerated).length,
        totalImages: freshData?.totalImages,
        allImages: freshData?.images
      };
    } catch (error) {
      console.error('Failed to debug data discrepancy:', error);
      return { error };
    }
  }

  // Make debug methods globally accessible
  enableGlobalDebug() {
    (window as any).debugDashboard = () => this.debugModelStatus();
    (window as any).discoverModels = () => this.triggerModelDiscovery();
    (window as any).fixGeneratedImages = () => this.checkGeneratedImagesFromFilesystem();
    (window as any).checkFallback = () => this.checkIfFallbackNeeded();
    (window as any).forceRefresh = () => this.forceRefresh();
    (window as any).invalidateImages = () => this.invalidateAndRefreshImages();
    (window as any).debugData = () => this.debugDataDiscrepancy();
    console.log('🔍 Debug enabled! Available commands:');
    console.log('  - debugDashboard() - Run comprehensive model debug');
    console.log('  - discoverModels() - Manually trigger model discovery');
    console.log('  - fixGeneratedImages() - Fix missing generated images from filesystem');
    console.log('  - checkFallback() - Check if fallback operations are needed');
    console.log('  - forceRefresh() - Force refresh dashboard data (clears cache)');
    console.log('  - invalidateImages() - Invalidate image caches and refresh');
    console.log('  - debugData() - Check data discrepancy between dashboard and API');
  }
}
