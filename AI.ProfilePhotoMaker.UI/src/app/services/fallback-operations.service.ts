import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { ConfigService } from './config.service';
import { FileUploadService } from './file-upload.service';
import { 
  DashboardStateForFallback, 
  DataDiscrepancyResult, 
  FallbackCheckResult,
  FallbackTracker,
  IFallbackOperationsService
} from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root'
})
export class FallbackOperationsService implements IFallbackOperationsService {
  private fallbackOperationsRun: FallbackTracker = {
    filesystemCheck: false,
    modelDiscovery: false,
    lastReset: 0
  };

  private readonly FALLBACK_RESET_INTERVAL_MS = 300000; // 5 minutes

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private config: ConfigService,
    private fileUploadService: FileUploadService
  ) {}

  /**
   * Check filesystem for missing generated images and sync to database
   */
  checkGeneratedImagesFromFilesystem(): Observable<any> {
    const token = this.authService.getToken();
    if (!token) {
      throw new Error('No authentication token available for fix endpoint');
    }

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });

    console.log('📁 Checking filesystem for missing generated images...');
    
    return new Observable(observer => {
      this.http.post(this.config.getFullUrl('/test/fix-generated-images'), {}, { headers }).subscribe({
        next: (response: any) => {
          if (response.success && response.data?.addedCount > 0) {
            console.log(`✅ Auto-fixed: Added ${response.data.addedCount} generated images to database`);
            
            // Invalidate user images cache for fresh data
            this.fileUploadService.invalidateUserImagesCache();
            
            // Get fresh data to ensure we have the correct total count
            this.fileUploadService.getUserImages(true).subscribe({
              next: (freshData) => {
                const userData = freshData.success ? freshData.data : null;
                const actualGeneratedCount = userData?.generatedImages || 
                  userData?.images?.filter(img => img.isGenerated)?.length || 0;
                
                console.log(`📊 Refreshed generated photos count: ${actualGeneratedCount} (was ${response.data.addedCount} added)`);
                
                observer.next({
                  success: true,
                  addedCount: response.data.addedCount,
                  actualGeneratedCount
                });
                observer.complete();
              },
              error: (error) => {
                console.error('Failed to refresh generated photos count after fix:', error);
                observer.error(error);
              }
            });
          } else {
            console.log('ℹ️ No missing generated images found to fix');
            observer.next({ success: true, addedCount: 0 });
            observer.complete();
          }
        },
        error: (error) => {
          console.error('❌ Failed to check/fix generated images from filesystem:', error);
          observer.error(error);
        }
      });
    });
  }

  /**
   * Check if filesystem fallback is needed
   */
  isFilesystemCheckNeeded(generatedPhotosCount: number, hasTrainedModel: boolean, userProfile: any): boolean {
    return generatedPhotosCount === 0 && 
           (hasTrainedModel || userProfile?.latestTrainedModel) &&
           userProfile && // User has been active
           !this.fallbackOperationsRun.filesystemCheck;
  }

  /**
   * Check if model discovery is needed
   */
  isModelDiscoveryNeeded(modelStatus: string, uploadedImages: number, hasTrainedModel: boolean): boolean {
    return modelStatus === 'Not Started' && 
           uploadedImages > 0 &&
           !hasTrainedModel &&
           !this.fallbackOperationsRun.modelDiscovery;
  }

  /**
   * Intelligent fallback operations - only run when actually needed
   */
  checkIfFallbackNeeded(state: DashboardStateForFallback): FallbackCheckResult {
    
    console.log('🔍 Checking if fallback needed with state:', {
      generatedPhotosCount: state.generatedPhotosCount,
      modelStatus: state.modelStatus,
      hasLatestTrainedModel: state.hasLatestTrainedModel,
      uploadedImages: state.uploadedImages,
      hasUserProfile: state.hasUserProfile
    });
    
    // Check filesystem if we have 0 photos but evidence of a trained model
    const shouldCheckFilesystem = 
      state.generatedPhotosCount === 0 && 
      (state.latestTrainedModel || state.modelStatus === 'Model Ready') &&
      state.hasUserProfile && // User has been active
      !this.fallbackOperationsRun.filesystemCheck;
      
    // Discover models if we're missing model data but have uploads
    const shouldDiscoverModels = 
      state.modelStatus === 'Not Started' && 
      state.uploadedImages > 0 &&
      !state.latestTrainedModel &&
      !this.fallbackOperationsRun.modelDiscovery;
    
    if (shouldCheckFilesystem) {
      console.log('🔍 Data inconsistency detected - filesystem check needed');
      this.fallbackOperationsRun.filesystemCheck = true;
    }
    
    if (shouldDiscoverModels) {
      console.log('🔍 Missing model data detected - model discovery needed');
      this.fallbackOperationsRun.modelDiscovery = true;
    }
    
    if (!shouldCheckFilesystem && !shouldDiscoverModels) {
      console.log('✅ No fallback operations needed - data appears consistent');
    }
    
    return { shouldCheckFilesystem, shouldDiscoverModels };
  }

  /**
   * Debug method to check actual data vs displayed data
   */
  async debugDataDiscrepancy(): Promise<DataDiscrepancyResult> {
    console.log('🔍 Debugging data discrepancy...');
    
    try {
      const freshData = await this.fileUploadService.getUserImages(true).toPromise();
      const userData = freshData?.success ? freshData.data : null;
      
      const result: DataDiscrepancyResult = {
        dashboardCount: 0, // This would be passed from dashboard state
        apiGeneratedField: userData?.generatedImages || 0,
        filteredCount: userData?.images?.filter(img => img.isGenerated)?.length || 0,
        totalImages: userData?.totalImages || 0,
        allImages: userData?.images || []
      };
      
      console.log('📊 Data Discrepancy Analysis:');
      console.log('  API generatedImages field:', result.apiGeneratedField);
      console.log('  Filtered generated count:', result.filteredCount);
      console.log('  Total images:', result.totalImages);
      
      return result;
    } catch (error) {
      console.error('Failed to debug data discrepancy:', error);
      throw error;
    }
  }

  /**
   * Reset fallback tracking to allow fresh attempts
   */
  resetFallbackTracking(): void {
    const now = Date.now();
    
    // Reset fallback tracking every 5 minutes to allow fresh attempts
    if (now - this.fallbackOperationsRun.lastReset > this.FALLBACK_RESET_INTERVAL_MS) {
      this.fallbackOperationsRun = { 
        filesystemCheck: false, 
        modelDiscovery: false, 
        lastReset: now 
      };
      console.log('🔄 Reset fallback operation tracking');
    }
  }

  /**
   * Get current fallback tracker state
   */
  getFallbackTracker(): FallbackTracker {
    return { ...this.fallbackOperationsRun };
  }

  /**
   * Mark filesystem check as completed
   */
  markFilesystemCheckCompleted(): void {
    this.fallbackOperationsRun.filesystemCheck = true;
  }

  /**
   * Mark model discovery as completed
   */
  markModelDiscoveryCompleted(): void {
    this.fallbackOperationsRun.modelDiscovery = true;
  }

  /**
   * Enable global debug methods for fallback operations
   */
  enableGlobalDebug(): void {
    (window as any).checkFallback = (state: any) => this.checkIfFallbackNeeded(state);
    (window as any).fixGeneratedImages = () => this.checkGeneratedImagesFromFilesystem();
    (window as any).debugData = () => this.debugDataDiscrepancy();
    (window as any).resetFallback = () => {
      this.fallbackOperationsRun = { filesystemCheck: false, modelDiscovery: false, lastReset: Date.now() };
      console.log('🔄 Fallback tracking manually reset');
    };
    
    console.log('🔍 Fallback debug enabled! Available commands:');
    console.log('  - checkFallback(state) - Check if fallback operations are needed');
    console.log('  - fixGeneratedImages() - Fix missing generated images from filesystem');
    console.log('  - debugData() - Check data discrepancy between dashboard and API');
    console.log('  - resetFallback() - Manually reset fallback tracking');
  }
}