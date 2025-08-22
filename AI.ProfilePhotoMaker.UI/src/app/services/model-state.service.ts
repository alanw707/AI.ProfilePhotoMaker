import { Injectable } from '@angular/core';
import { forkJoin, firstValueFrom } from 'rxjs';
import { ProfileService } from './profile.service';
import { FileUploadService } from './file-upload.service';
import { IModelStateService } from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root',
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
      next: discoveryResult => {
        if (discoveryResult?.success && discoveryResult?.data?.ModelsAdded > 0) {
          // Update just the model status without reloading everything
          this.updateModelStatus();
        }
      },
      error: error => {
        console.error('Async model discovery failed:', error);
        // Don't show error to user - this is a background operation
      },
    });
  }

  /**
   * Update only the model status after discovery
   */
  updateModelStatus(): void {
    // Prefer unified model status endpoint when available
    this.fileUploadService.getUnifiedModelStatus().subscribe({
      next: unified => {
        const display = this.mapUnifiedStatusToDisplay(unified.statusCode, unified.reason);
        const latestTrainedModel = unified.hasTrainedModel
          ? {
              trainedModelVersion: unified.trainedModelVersion,
              replicateModelId: unified.trainedModelId,
            }
          : null;
        this.notifyModelStatusUpdate(display, latestTrainedModel);
      },
      error: _ => {
        // Fallback to legacy combination of endpoints
        forkJoin({
          trainingStatus: this.fileUploadService.getTrainingStatus(),
          modelRequests: this.fileUploadService.getUserModelRequests(),
        }).subscribe({
          next: ({ modelRequests }) => {
            const modelRequestsData = modelRequests.success ? modelRequests.data : null;
            let modelStatus = 'Not Started';
            if (modelRequestsData?.hasTrainedModel && modelRequestsData?.latestTrainedModel) {
              modelStatus = 'Model Ready';
              this.notifyModelStatusUpdate(modelStatus, modelRequestsData.latestTrainedModel);
            } else {
              this.notifyModelStatusUpdate(modelStatus, null);
            }
          },
          error: error => {
            console.error('Failed to update model status:', error);
          },
        });
      },
    });
  }

  /**
   * Manual model discovery method
   */
  async triggerModelDiscovery(): Promise<any> {
    try {
      const discoveryResult = await firstValueFrom(this.profileService.discoverModels());

      if (discoveryResult?.success && discoveryResult?.data?.ModelsAdded > 0) {
        this.updateModelStatus();
      } else {
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
  isModelDiscoveryNeeded(
    hasTrainedModel: boolean,
    modelStatus: string,
    uploadedImages: number
  ): boolean {
    return modelStatus === 'Not Started' && uploadedImages > 0 && !hasTrainedModel;
  }

  /**
   * Get model status from model requests data
   */
  getModelStatusFromData(
    modelRequestsData: any,
    trainingStatus: any
  ): {
    modelStatus: string;
    hasTrainedModel: boolean;
    latestTrainedModel: any;
  } {
    let modelStatus = 'Not Started';
    let hasTrainedModel = false;
    let latestTrainedModel = null;

    // Use ModelCreationRequest as single source of truth when a trained model exists
    if (modelRequestsData?.hasTrainedModel && modelRequestsData?.latestTrainedModel) {
      hasTrainedModel = true;
      modelStatus = 'Model Ready';
      latestTrainedModel = modelRequestsData.latestTrainedModel;
      return { modelStatus, hasTrainedModel, latestTrainedModel };
    }

    // Prefer explicit training status from API when it communicates actionable states
    // This helps when old pending/creating requests linger but no active training exists
    const ts = trainingStatus ?? {};
    const tsStatus = (ts.status ?? ts.Status ?? '').toString();
    if (tsStatus) {
      const normalized = tsStatus.trim();
      // Trust these server-computed messages over inferred states
      if (
        normalized.startsWith('Ready for training') ||
        normalized.startsWith('No images uploaded') ||
        normalized.startsWith('Need at least')
      ) {
        modelStatus = normalized;
        return { modelStatus, hasTrainedModel, latestTrainedModel };
      }
    }

    // If no trained model yet, look at the most recent request first to avoid stale entries influencing status
    const all = Array.isArray(modelRequestsData?.allRequests)
      ? [...modelRequestsData.allRequests]
      : [];

    // Sort descending by createdAt if available
    all.sort((a: any, b: any) => {
      const ad = new Date(a.createdAt ?? 0).getTime();
      const bd = new Date(b.createdAt ?? 0).getTime();
      return bd - ad;
    });

    const latest = all[0];

    if (latest?.status === 'creating' || latest?.status === 'pending') {
      modelStatus = 'training';
    } else if (
      // If DB shows a 'ready' request but no trained version exists (edge-case), treat as ready to (re)train
      all.some((req: any) => req.status === 'ready') &&
      !modelRequestsData?.hasTrainedModel
    ) {
      modelStatus = 'Ready for training';
    } else if (
      // If a model was deleted from Replicate, surface an actionable state
      all.some(
        (req: any) =>
          req.status === 'failed' && req.errorMessage?.includes('deleted from Replicate')
      )
    ) {
      modelStatus = 'Ready for training';
    } else {
      // Default to server-reported training status or initial state
      modelStatus = tsStatus || 'Not Started';
    }

    return { modelStatus, hasTrainedModel, latestTrainedModel };
  }

  /**
   * Notify dashboard state service of model status updates
   * This would be implemented to communicate with DashboardStateService
   */
  private notifyModelStatusUpdate(_modelStatus: string, _latestTrainedModel: any): void {
    // This could emit an event or call a callback
    // For now, just log the update
  }

  private mapUnifiedStatusToDisplay(statusCode: string, reason?: string | null): string {
    switch (statusCode) {
      case 'ModelReady':
        return 'Model Ready';
      case 'Training':
        return 'training';
      case 'ReadyForTraining':
        return 'Ready for training';
      case 'Failed':
        return reason && reason.includes('deleted') ? 'Ready for training' : 'Training failed';
      case 'NotStarted':
      default:
        return 'Not Started';
    }
  }
}
