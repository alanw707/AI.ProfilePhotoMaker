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
    this.setState({ isLoading: true });

    // Load critical data first (non-blocking)
    forkJoin({
      profile: this.profileService.getCurrentUserProfile(),
      credits: this.replicateService.getCredits(),
      creditStatus: this.creditService.getCreditStatus(),
      trainingStatus: this.fileUploadService.getTrainingStatus(),
      userImages: this.fileUploadService.getUserImages(),
      modelRequests: this.fileUploadService.getUserModelRequests()
    }).subscribe({
      next: ({ profile, credits, creditStatus, trainingStatus, userImages, modelRequests }) => {
        const userProfile = profile.success ? profile.data : null;
        const creditsInfo = credits.success ? credits.data : null;
        const userCreditStatus = creditStatus.success ? creditStatus.data : null;
        const modelRequestsData = modelRequests.success ? modelRequests.data : null;
        
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
        
        // Hybrid approach: If database shows 0 generated images but we know they should exist,
        // try to get count from filesystem as fallback
        if (generatedPhotosCount === 0 && modelRequestsData?.hasTrainedModel) {
          console.log('📁 Database shows 0 generated images but model exists - checking filesystem fallback');
          this.checkGeneratedImagesFromFilesystem();
        }
        
        console.log(`📊 Generated Photos Count: ${generatedPhotosCount}${generatedPhotosCount === 0 && modelRequestsData?.hasTrainedModel ? ' (checking for missing images...)' : ''}`);
        
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
        
        this.setState({
          userProfile,
          creditsInfo,
          userCreditStatus,
          uploadedImages: trainingStatus.totalUploadedImages || 0,
          uploadedImageThumbnails,
          generatedPhotosCount,
          modelStatus,
          isPremiumWorkflow: (userCreditStatus?.purchasedCredits || 0) > 0,
          isLoading: false,
          latestTrainedModel: modelRequestsData?.latestTrainedModel || null
        });

        // Now run model discovery asynchronously (non-blocking)
        // Only if model status suggests we might need it
        if (!hasTrainedModel || modelStatus === 'Not Started') {
          this.runAsyncModelDiscovery();
        }
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
          // Refresh the dashboard data to reflect the newly added images
          this.loadInitialDashboardData();
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

  // Make debug methods globally accessible
  enableGlobalDebug() {
    (window as any).debugDashboard = () => this.debugModelStatus();
    (window as any).discoverModels = () => this.triggerModelDiscovery();
    (window as any).fixGeneratedImages = () => this.checkGeneratedImagesFromFilesystem();
    console.log('🔍 Debug enabled! Available commands:');
    console.log('  - debugDashboard() - Run comprehensive model debug');
    console.log('  - discoverModels() - Manually trigger model discovery');
    console.log('  - fixGeneratedImages() - Fix missing generated images from filesystem');
  }
}
