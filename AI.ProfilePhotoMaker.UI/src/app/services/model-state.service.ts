import { Injectable } from '@angular/core';
import { forkJoin, firstValueFrom } from 'rxjs';
import { ProfileService } from './profile.service';
import { FileUploadService } from './file-upload.service';
import { IModelStateService } from '../interfaces/service.interfaces';
import { ModelStatusService } from './model-status.service';
import { ModelStatus, ModelStatusHelper } from '../models/workspace.types';

@Injectable({
  providedIn: 'root',
})
export class ModelStateService implements IModelStateService {
  constructor(
    private profileService: ProfileService,
    private fileUploadService: FileUploadService,
    private modelStatus: ModelStatusService
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
      // Check if the "trained" model was actually deleted by user
      const all = Array.isArray(modelRequestsData?.allRequests)
        ? [...modelRequestsData.allRequests]
        : [];
      const wasDeleted = all.some(
        (req: any) => req.status === 'failed' && this.isModelUnavailableReason(req.errorMessage)
      );

      if (wasDeleted) {
        // Model was deleted, don't show as ready
        modelStatus = 'Ready for training';
        hasTrainedModel = false;
        latestTrainedModel = null;
        return { modelStatus, hasTrainedModel, latestTrainedModel };
      }

      hasTrainedModel = true;
      modelStatus = 'ModelReady'; // Use unified status code
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
      // Normalize server message indicating trained model to unified display
      // e.g. "Model trained - ready for generation"
      if (
        normalized.startsWith('Model trained') ||
        normalized.toLowerCase().includes('ready for generation')
      ) {
        modelStatus = 'ModelReady'; // Use unified status code
        // We can't guarantee latestTrainedModel details from this endpoint
        // but we can safely reflect readiness in the workspace
        hasTrainedModel = true;
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
      modelStatus = 'Training'; // Use unified status code
    } else if (
      // Only treat as Model Ready if we have usable model data from a 'ready' request
      all.some((req: any) => req.status === 'ready')
    ) {
      const latestReady = all.find(
        (req: any) => req.status === 'ready' && (req.trainedModelVersion || req.replicateModelId)
      );
      if (latestReady) {
        modelStatus = 'ModelReady'; // Use unified status code
        hasTrainedModel = true;
        latestTrainedModel = {
          requestId: latestReady.requestId,
          modelName: latestReady.modelName,
          replicateModelId: latestReady.replicateModelId,
          trainedModelVersion: latestReady.trainedModelVersion,
          completedAt: latestReady.completedAt,
        };
      } else {
        // No usable model details; keep a conservative state (prefer server training status if any)
        modelStatus = tsStatus || 'Ready for training';
      }
    } else if (
      // If a model was deleted (by user or externally), surface an actionable state
      all.some(
        (req: any) => req.status === 'failed' && this.isModelUnavailableReason(req.errorMessage)
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
   * Generate semantic status from legacy string status
   */
  getSemanticStatus(legacyStatus: string, progress?: number, error?: string): ModelStatus {
    return ModelStatusHelper.fromLegacyStatus(legacyStatus, progress, error);
  }

  /**
   * Enhanced model status that includes both legacy and semantic status
   */
  getEnhancedModelStatusFromData(
    modelRequestsData: any,
    trainingStatus: any
  ): {
    modelStatus: string;
    modelStatusSemantic: ModelStatus;
    hasTrainedModel: boolean;
    latestTrainedModel: any;
  } {
    const legacyResult = this.getModelStatusFromData(modelRequestsData, trainingStatus);
    const semanticStatus = this.getSemanticStatus(legacyResult.modelStatus);

    return {
      ...legacyResult,
      modelStatusSemantic: semanticStatus,
    };
  }

  /**
   * Notify workspace state service of model status updates
   * This would be implemented to communicate with WorkspaceStateService
   */
  private notifyModelStatusUpdate(_modelStatus: string, _latestTrainedModel: any): void {
    // This could emit an event or call a callback
    // For now, just log the update
  }

  private mapUnifiedStatusToDisplay(statusCode: string, reason?: string | null): string {
    // Delegate to centralized status service for consistent display logic
    if (statusCode === 'Failed' && this.isModelUnavailableReason(reason)) {
      return 'Ready for training'; // Special case for deleted models
    }
    return this.modelStatus.getStatusInfo(statusCode).displayText;
  }

  private isModelUnavailableReason(reason?: string | null): boolean {
    const normalized = reason?.toLowerCase().trim() || '';
    if (!normalized) {
      return false;
    }

    return (
      normalized.includes('deleted') ||
      normalized.includes('expired') ||
      normalized.includes('retention policy') ||
      normalized.includes('no longer exists')
    );
  }
}
