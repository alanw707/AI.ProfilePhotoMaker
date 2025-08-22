import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';
import { ConfigService } from './config.service';
import { FileUploadService } from './file-upload.service';
import {
  DashboardStateForFallback,
  DataDiscrepancyResult,
  FallbackCheckResult,
  FallbackTracker,
  IFallbackOperationsService,
} from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root',
})
export class FallbackOperationsService implements IFallbackOperationsService {
  private fallbackOperationsRun: FallbackTracker = {
    filesystemCheck: false,
    modelDiscovery: false,
    lastReset: 0,
  };

  private readonly FALLBACK_RESET_INTERVAL_MS = 300000; // 5 minutes

  constructor(
    private _http: HttpClient,
    private _authService: AuthService,
    private _config: ConfigService,
    private _fileUploadService: FileUploadService
  ) {}

  /**
   * Check filesystem for missing generated images and sync to database
   */
  checkGeneratedImagesFromFilesystem(): Observable<any> {
    const token = this._authService.getToken();
    if (!token) {
      throw new Error('No authentication token available for fix endpoint');
    }

    const headers = new HttpHeaders({
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    });

    return new Observable(observer => {
      this._http
        .post(this._config.getFullUrl('/test/fix-generated-images'), {}, { headers })
        .subscribe({
          next: (response: any) => {
            if (response.success && response.data?.addedCount > 0) {
              // Invalidate user images cache for fresh data
              this._fileUploadService.invalidateUserImagesCache();

              // Get fresh data to ensure we have the correct total count
              this._fileUploadService.getUserImages(true).subscribe({
                next: (freshData: any) => {
                  const userData = freshData.success ? freshData.data : null;
                  const actualGeneratedCount =
                    userData?.generatedImages ||
                    userData?.images?.filter((img: any) => img.isGenerated)?.length ||
                    0;

                  observer.next({
                    success: true,
                    addedCount: response.data.addedCount,
                    actualGeneratedCount,
                  });
                  observer.complete();
                },
                error: (error: any) => {
                  console.error('Failed to refresh generated photos count after fix:', error);
                  observer.error(error);
                },
              });
            } else {
              observer.next({ success: true, addedCount: 0 });
              observer.complete();
            }
          },
          error: error => {
            console.error('❌ Failed to check/fix generated images from filesystem:', error);
            observer.error(error);
          },
        });
    });
  }

  /**
   * Check if filesystem fallback is needed
   */
  isFilesystemCheckNeeded(
    generatedPhotosCount: number,
    hasTrainedModel: boolean,
    userProfile: any
  ): boolean {
    return (
      generatedPhotosCount === 0 &&
      (hasTrainedModel || userProfile?.latestTrainedModel) &&
      userProfile && // User has been active
      !this.fallbackOperationsRun.filesystemCheck
    );
  }

  /**
   * Check if model discovery is needed
   */
  isModelDiscoveryNeeded(
    modelStatus: string,
    uploadedImages: number,
    hasTrainedModel: boolean
  ): boolean {
    return (
      modelStatus === 'Not Started' &&
      uploadedImages > 0 &&
      !hasTrainedModel &&
      !this.fallbackOperationsRun.modelDiscovery
    );
  }

  /**
   * Intelligent fallback operations - only run when actually needed
   */
  checkIfFallbackNeeded(state: DashboardStateForFallback): FallbackCheckResult {
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
      this.fallbackOperationsRun.filesystemCheck = true;
    }

    if (shouldDiscoverModels) {
      this.fallbackOperationsRun.modelDiscovery = true;
    }

    if (!shouldCheckFilesystem && !shouldDiscoverModels) {
    }

    return { shouldCheckFilesystem, shouldDiscoverModels };
  }

  /**
   * Debug method to check actual data vs displayed data
   */
  async debugDataDiscrepancy(): Promise<DataDiscrepancyResult> {
    try {
      const freshData = await firstValueFrom(this._fileUploadService.getUserImages(true));
      const userData = freshData?.success ? freshData.data : null;

      const result: DataDiscrepancyResult = {
        dashboardCount: 0, // This would be passed from dashboard state
        apiGeneratedField: userData?.generatedImages || 0,
        filteredCount: userData?.images?.filter((img: any) => img.isGenerated)?.length || 0,
        totalImages: userData?.totalImages || 0,
        allImages: userData?.images || [],
      };

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
        lastReset: now,
      };
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
}
