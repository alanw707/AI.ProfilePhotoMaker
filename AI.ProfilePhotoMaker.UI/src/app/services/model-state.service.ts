import { Injectable } from '@angular/core';
import { Observable, forkJoin } from 'rxjs';
import { ProfileService } from './profile.service';
import { FileUploadService } from './file-upload.service';
import { IModelStateService, ModelStatusInfo } from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root'
})
export class ModelStateService implements IModelStateService {

  constructor(
    private profileService: ProfileService,
    private fileUploadService: FileUploadService
  ) {}

  /**
   * Run model discovery asynchronously without blocking the UI
   */
  runAsyncModelDiscovery(): void {
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

  /**
   * Update only the model status after discovery
   */
  updateModelStatus(): void {
    forkJoin({
      trainingStatus: this.fileUploadService.getTrainingStatus(),
      modelRequests: this.fileUploadService.getUserModelRequests()
    }).subscribe({
      next: ({ trainingStatus, modelRequests }) => {
        const modelRequestsData = modelRequests.success ? modelRequests.data : null;
        
        let modelStatus = 'Not Started';
        
        // Re-check model status with updated data
        if (modelRequestsData?.hasTrainedModel && modelRequestsData?.latestTrainedModel) {
          modelStatus = 'Model Ready';
          // Emit this update to dashboard state service if needed
          this.notifyModelStatusUpdate(modelStatus, modelRequestsData.latestTrainedModel);
        } else {
          this.notifyModelStatusUpdate(modelStatus, null);
        }
      },
      error: (error) => {
        console.error('Failed to update model status:', error);
      }
    });
  }

  /**
   * Debug methods for console testing
   */
  async debugModelStatus(): Promise<any> {
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
        specificModel: specificModelTest
      };
    } catch (error) {
      console.error('🚨 Debug failed:', error);
      return { error };
    }
  }

  /**
   * Manual model discovery method
   */
  async triggerModelDiscovery(): Promise<any> {
    console.log('🔍 Manually triggering model discovery...');
    
    try {
      const discoveryResult = await this.profileService.discoverModels().toPromise();
      console.log('🔍 Manual Model Discovery Result:', discoveryResult);
      
      if (discoveryResult?.success && discoveryResult?.data?.ModelsAdded > 0) {
        console.log('🎉 Models found and synced! Updating model status...');
        this.updateModelStatus();
      } else {
        console.log('ℹ️ No new models found to sync');
      }
      
      return discoveryResult;
    } catch (error) {
      console.error('🚨 Manual model discovery failed:', error);
      return { success: false, error };
    }
  }

  /**
   * Check if model discovery is needed based on current state
   */
  isModelDiscoveryNeeded(hasTrainedModel: boolean, modelStatus: string, uploadedImages: number): boolean {
    return modelStatus === 'Not Started' && 
           uploadedImages > 0 &&
           !hasTrainedModel;
  }

  /**
   * Get model status from model requests data
   */
  getModelStatusFromData(modelRequestsData: any, trainingStatus: any): { 
    modelStatus: string; 
    hasTrainedModel: boolean; 
    latestTrainedModel: any 
  } {
    let modelStatus = 'Not Started';
    let hasTrainedModel = false;
    let latestTrainedModel = null;

    // Use ModelCreationRequest as single source of truth
    if (modelRequestsData?.hasTrainedModel && modelRequestsData?.latestTrainedModel) {
      hasTrainedModel = true;
      modelStatus = 'Model Ready';
      latestTrainedModel = modelRequestsData.latestTrainedModel;
    }
    // Check if we have pending/in-progress training
    else if (modelRequestsData?.allRequests?.some((req: any) => req.status === 'creating' || req.status === 'pending')) {
      modelStatus = 'training';
    }
    // Default to training status or initial state
    else {
      modelStatus = trainingStatus?.status || 'Not Started';
    }

    return { modelStatus, hasTrainedModel, latestTrainedModel };
  }

  /**
   * Notify dashboard state service of model status updates
   * This would be implemented to communicate with DashboardStateService
   */
  private notifyModelStatusUpdate(modelStatus: string, latestTrainedModel: any): void {
    // This could emit an event or call a callback
    // For now, just log the update
    console.log('🔄 Model status updated:', { modelStatus, latestTrainedModel });
  }

  /**
   * Enable global debug methods for console access
   */
  enableGlobalDebug(): void {
    (window as any).debugModelStatus = () => this.debugModelStatus();
    (window as any).discoverModels = () => this.triggerModelDiscovery();
    console.log('🔍 Model debug enabled! Available commands:');
    console.log('  - debugModelStatus() - Run comprehensive model debug');
    console.log('  - discoverModels() - Manually trigger model discovery');
  }
}